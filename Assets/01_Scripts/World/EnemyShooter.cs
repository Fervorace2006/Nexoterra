using UnityEngine;
using System;
using System.Linq;
using Random=UnityEngine.Random;
namespace Nexoterra.World
{
 [RequireComponent(typeof(EnemyCombatant),typeof(CharacterController))]
 public sealed class EnemyShooter : MonoBehaviour
 {
  public SlugCatalog catalog;
  public bool boss,campaignMember;
  public string equippedElemental;
  public string[] slugSlots=Array.Empty<string>();
  public bool dropSlugsOnDeath;
  readonly SlugProjectile[] travelling=new SlugProjectile[3];
  bool dropped;
  public bool IsDead=>GetComponent<EnemyCombatant>().IsDead;
  public bool IsSlotBusy(int slot)=>slot>=0&&slot<3&&travelling[slot];
  public SlugProjectile ProjectileInSlot(int slot)=>slot>=0&&slot<3?travelling[slot]:null;
  public void RecoverSlug(int slot,SlugProjectile shot){if(slot>=0&&slot<3&&travelling[slot]==shot)travelling[slot]=null;}
  public float aggroRange=28,attackRange=18,speed=2.2f,shotCooldown=3.5f,respawnSeconds=300;
  public Transform model,weapon;
  public HumanoidPose pose;
  public Vector3 MotionVelocity {get;private set;}
  public float MotionSpeed=>MotionVelocity.magnitude;
  EnemyCombatant health;CharacterController motor;PlayerHealth target;
  int volley;float reloadUntil;bool loaded;
  static readonly string[] commonLoadout={"Infierno","Arana","ice","Carnero","tazerling","Armashelt"};
  static readonly string[] bossLoadout={"Infierno","ice","tazerling","Arana","Tornado"};
  public string NextSpecies(){EnsureSlots();int slot=ReadySlot();return slot<0?null:SlugCatalog.BaseSpecies(slugSlots[slot]);}
  void EnsureSlots(){
   if(slugSlots==null||slugSlots.Length==0){var defaults=boss?bossLoadout:commonLoadout;slugSlots=defaults.Take(3).ToArray();if(!string.IsNullOrEmpty(equippedElemental))slugSlots[0]=equippedElemental;}
   slugSlots=slugSlots.Take(3).Where(s=>!string.IsNullOrEmpty(s)).Select(s=>SlugCatalog.BaseSpecies(s)=="Elemental_Energia"?"Elemental_Energia":"Malvada_"+SlugCatalog.BaseSpecies(s)).ToArray();
  }
  int ReadySlot(){for(int i=0;i<slugSlots.Length;i++){int slot=(volley+i)%slugSlots.Length;if(!travelling[slot])return slot;}return -1;}
  int patrolPoint;
  Vector3 home,modelOrigin;float fireAt,vertical,deadTime,recoil;
  float flankSide,nextFlankChange;
  public float Recoil=>recoil;
  public bool IsPursuing {get;private set;}
  public bool IsAiming {get;private set;}
  public Vector3 AimDirection=>target&&weapon?(target.transform.position+Vector3.up-weapon.position).normalized:transform.forward;
  void Start(){health=GetComponent<EnemyCombatant>();EnsureSlots();health.onDeath.AddListener(DropSlugs);motor=GetComponent<CharacterController>();target=FindAnyObjectByType<PlayerHealth>();home=transform.position;if(model)modelOrigin=model.localPosition;volley=Mathf.Abs(Mathf.RoundToInt(home.x*3+home.z*7))%commonLoadout.Length;patrolPoint=volley%4;flankSide=(volley%2==0)?1:-1;fireAt=Time.time+2;}
  void Update()
  {
   IsPursuing=false;IsAiming=false;if(GameSession.Blocked)return;
   if(health.IsDead){deadTime+=Time.deltaTime;if(!boss&&!campaignMember&&deadTime>=respawnSeconds){motor.enabled=false;transform.position=home;motor.enabled=true;health.Revive();deadTime=0;}return;}
   Vector3 move=Vector3.zero;var native=GetComponent<NativeEnemyAnimation>();bool evading=native&&native.IsDodging;
   if(target&&health.CanAct&&!target.IsDead)
   {
    float distance=Vector3.Distance(transform.position,target.transform.position);
    bool engaged=distance<aggroRange&&Vector3.Distance(transform.position,home)<35;
    IsPursuing=engaged;
    IsAiming=engaged&&distance<attackRange&&health.CanShoot;
    if(engaged)
    {
     Vector3 forward=Vector3.ProjectOnPlane(target.transform.position-transform.position,Vector3.up);
     if(forward.sqrMagnitude>.05f)transform.rotation=Quaternion.Slerp(transform.rotation,Quaternion.LookRotation(forward),1-Mathf.Exp(-5*Time.deltaTime));
     if(!evading&&health.CanMove)move=CombatDirection(forward.normalized,distance)*speed*1.8f*health.SpeedMultiplier;
    }
    else if(!evading&&health.CanMove)move=PatrolVelocity();
    if(!loaded&&reloadUntil==0&&health.CanShoot&&engaged){reloadUntil=Time.time+1.67f;if(native)native.Reload();}
    if(reloadUntil>0&&Time.time>=reloadUntil){loaded=true;reloadUntil=0;}
    if(loaded&&!evading&&health.CanShoot&&engaged&&distance<attackRange&&Time.time>=fireAt)
    {
     Vector3 origin=weapon?weapon.position+weapon.forward*.65f:transform.position+Vector3.up*1.4f+transform.forward*.85f,aim=target.transform.position+Vector3.up;
     bool blocked=false;foreach(var hit in Physics.RaycastAll(origin,(aim-origin).normalized,Vector3.Distance(origin,aim),~0,QueryTriggerInteraction.Ignore))if(!hit.transform.IsChildOf(transform)&&hit.collider.GetComponentInParent<PlayerHealth>()!=target){blocked=true;break;}
     if(!blocked&&TryLaunch(origin,aim)){loaded=false;fireAt=Time.time+shotCooldown;}
     else fireAt=Time.time+.2f;
    }
   }
   if(motor.isGrounded&&vertical<0)vertical=-2;vertical+=Physics.gravity.y*Time.deltaTime;Vector3 before=transform.position;
   motor.Move((move+Vector3.up*vertical)*Time.deltaTime);
   MotionVelocity=move.sqrMagnitude>0 && Time.deltaTime>0?Vector3.ProjectOnPlane(transform.position-before,Vector3.up)/Time.deltaTime:Vector3.zero;
   recoil=Mathf.MoveTowards(recoil,0,Time.deltaTime*3);if(model&&pose){model.localPosition=modelOrigin+Vector3.up*(Mathf.Sin(Time.time*10)*.035f*Mathf.Clamp01(MotionSpeed))+Vector3.back*(recoil*.12f);}
  }
  Vector3 PatrolVelocity()
  {
   Vector3 goal=home+Quaternion.Euler(0,patrolPoint*90f,0)*Vector3.forward*5f;
   Vector3 offset=Vector3.ProjectOnPlane(goal-transform.position,Vector3.up);
   if(offset.sqrMagnitude<.16f){patrolPoint=(patrolPoint+1)%4;return Vector3.zero;}
   Quaternion facing=Quaternion.LookRotation(offset);
   transform.rotation=Quaternion.RotateTowards(transform.rotation,facing,240f*Time.deltaTime);
   if(Quaternion.Angle(transform.rotation,facing)>2f)return Vector3.zero;
   if(!CanStep(transform.forward)){patrolPoint=(patrolPoint+1)%4;return Vector3.zero;}
   return transform.forward*Mathf.Min(speed*health.SpeedMultiplier,offset.magnitude/Mathf.Max(.001f,Time.deltaTime));
  }

  Vector3 CombatDirection(Vector3 toward,float distance)
  {
   // Close quickly, then circle at firing distance or give ground if rushed.
   if(Time.time>=nextFlankChange)
   {
    flankSide=-flankSide;
    nextFlankChange=Time.time+Random.Range(1.8f,3.2f);
   }
   float advance=Mathf.Clamp((distance-attackRange*.62f)/(attackRange*.25f),-1,1);
   Vector3 lateral=Vector3.Cross(Vector3.up,toward)*flankSide;
   float strafe=Mathf.Clamp01(1-Mathf.Abs(advance));
   Vector3 desired=(toward*advance+lateral*strafe).normalized;
   if(CanStep(desired))return desired;
   if(CanStep(lateral))return lateral;
   if(CanStep(-lateral)){flankSide=-flankSide;return -lateral;}
   return Vector3.zero;
  }

  bool CanStep(Vector3 direction)
  {
   Vector3 ahead=transform.position+direction*(motor.radius+.8f);
   if(Physics.SphereCast(transform.position+Vector3.up*.9f,motor.radius*.8f,direction,out _,.8f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))return false;
   return Physics.Raycast(ahead+Vector3.up*.7f,Vector3.down,out var floor,1.7f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore)
       &&Vector3.Angle(floor.normal,Vector3.up)<=motor.slopeLimit;
  }
  void LateUpdate(){if(!GetComponent<NativeEnemyAnimation>()&&pose&&health&&!health.IsDead&&!health.IsKnockedDown)pose.Pose(MotionSpeed,false,0,weapon,recoil,0);}
  public bool TryLaunch(Vector3 origin,Vector3 aim)
  {
   if(!catalog||IsDead)return false;EnsureSlots();int slot=ReadySlot();if(slot<0)return false;var baseSlug=catalog.Find(slugSlots[slot]);if(baseSlug==null)return false;
   SlugDefinition slug=baseSlug;
   if(boss&&string.IsNullOrEmpty(equippedElemental)){var combat=SlugCombatProfile.Default(SlugCatalog.BaseSpecies(baseSlug.id));combat.impactDamage*=1.35f;combat.burnDamagePerSecond*=1.35f;slug=new SlugDefinition{id="Malvada_"+SlugCatalog.BaseSpecies(baseSlug.id),normal=baseSlug.normal,transformed=baseSlug.transformed,combat=combat,combatConfigured=true,usePrefabCombat=false};}
   var shot=new GameObject(boss?"Babosa malvada":"Babosa enemiga").AddComponent<SlugProjectile>();shot.transform.position=origin;shot.speed=boss?22:18;shot.transformDistance=5;shot.maxDistance=60;travelling[slot]=shot;shot.LaunchEnemy(slug,(aim-origin).normalized,this,slot);volley=(slot+1)%slugSlots.Length;recoil=1;return true;
  }
  void DropSlugs(){
   foreach(var shot in travelling)if(shot)Destroy(shot.gameObject);
   if(dropped||!dropSlugsOnDeath||!catalog)return;dropped=true;
   var campaign=CampaignLevel.Current;int enemyIndex=campaign?Array.IndexOf(campaign.enemies,GetComponent<EnemyCombatant>()):-1;
   for(int i=0;i<slugSlots.Length;i++){
    string species=slugSlots[i];
    if(campaign&&campaign.guardian==GetComponent<EnemyCombatant>()&&SlugCatalog.BaseSpecies(species)==CampaignLevel.Rewards[campaign.index])continue;
    string key=enemyIndex+":"+i;if(campaign&&campaign.LootCollected(key))continue;
    var definition=catalog.Find(species);if(definition==null)continue;
    var position=transform.position+Quaternion.Euler(0,i*120,0)*Vector3.forward*.8f;
    var wild=EnemySlugLoot.Spawn(definition,position,key);
   }
  }
  void OnDestroy(){foreach(var shot in travelling)if(shot)Destroy(shot.gameObject);}
 }
}

