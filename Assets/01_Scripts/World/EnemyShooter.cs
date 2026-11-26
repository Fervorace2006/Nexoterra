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
  readonly RaycastHit[] spawnGroundHits=new RaycastHit[24];
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
  EnemyCombatant health;CharacterController motor;PlayerHealth target;NativeEnemyAnimation native;
  int volley;float reloadUntil;bool loaded;
  static readonly string[] commonLoadout={"Infierno","Arana","ice","Carnero","tazerling","Armashelt"};
  static readonly string[] bossLoadout={"Infierno","ice","tazerling","Arana","Tornado"};
  public string NextSpecies(){EnsureSlots();int slot=ReadySlot();return slot<0?null:SlugCatalog.BaseSpecies(slugSlots[slot]);}
  bool slotsInitialized;
  void EnsureSlots(){
   if(slotsInitialized)return;slotsInitialized=true;
   if(slugSlots==null||slugSlots.Length==0){var defaults=boss?bossLoadout:commonLoadout;slugSlots=defaults.Take(3).ToArray();if(!string.IsNullOrEmpty(equippedElemental))slugSlots[0]=equippedElemental;}
   slugSlots=slugSlots.Take(3).Where(s=>!string.IsNullOrEmpty(s)).Select(s=>SlugCatalog.BaseSpecies(s)=="Elemental_Energia"?"Elemental_Energia":"Malvada_"+SlugCatalog.BaseSpecies(s)).ToArray();
  }
  int ReadySlot(){for(int i=0;i<slugSlots.Length;i++){int slot=(volley+i)%slugSlots.Length;if(!travelling[slot])return slot;}return -1;}
  int patrolPoint;
  Vector3 home,modelOrigin;float fireAt,vertical,deadTime,recoil;
  float flankSide,nextFlankChange,stuckTime,groundCheckAt,movementBlockedUntil;
  public float Recoil=>recoil;
  public bool IsPursuing {get;private set;}
  public bool IsAiming {get;private set;}
  public Vector3 AimDirection=>target&&weapon?(target.transform.position+Vector3.up-weapon.position).normalized:transform.forward;
  void Start(){health=GetComponent<EnemyCombatant>();EnsureSlots();health.onDeath.AddListener(DropSlugs);motor=GetComponent<CharacterController>();native=GetComponent<NativeEnemyAnimation>();target=FindAnyObjectByType<PlayerHealth>();
   Vector3 spawn=FindFreeSpawn(transform.position);motor.enabled=false;transform.position=spawn;motor.enabled=true;home=spawn;if(model)modelOrigin=model.localPosition;volley=Mathf.Abs(Mathf.RoundToInt(home.x*3+home.z*7))%commonLoadout.Length;patrolPoint=volley%4;flankSide=(volley%2==0)?1:-1;fireAt=Time.time+2;SnapToGround();}
  void Update()
  {
   IsPursuing=false;IsAiming=false;if(GameSession.Blocked)return;
   if(Time.time>=groundCheckAt){groundCheckAt=Time.time+.25f;SnapToGround();}
   if(health.IsDead){deadTime+=Time.deltaTime;if(!boss&&!campaignMember&&deadTime>=respawnSeconds){Vector3 spawn=FindFreeSpawn(home);motor.enabled=false;transform.position=spawn;motor.enabled=true;home=spawn;health.Revive();deadTime=0;SnapToGround();}return;}
   Vector3 move=Vector3.zero;bool evading=native&&native.IsDodging;
   if(target&&health.CanAct&&!target.IsDead)
   {
    float distance=Vector3.Distance(transform.position,target.transform.position);
    bool engaged=distance<aggroRange&&Vector3.Distance(transform.position,home)<35;
    Vector3 forward=Vector3.ProjectOnPlane(target.transform.position-transform.position,Vector3.up);
    IsPursuing=engaged;
    IsAiming=engaged&&distance<attackRange&&health.CanShoot;
    if(engaged)
    {
     if(forward.sqrMagnitude>.05f)transform.rotation=Quaternion.RotateTowards(transform.rotation,Quaternion.LookRotation(forward),Mathf.Lerp(360f,900f,IsAiming?1:0)*Time.deltaTime);
     if(!evading&&health.CanMove)move=CombatDirection(forward.normalized,distance)*speed*(tactic==Tactic.Retreat?1.5f:1.8f)*health.SpeedMultiplier;
    }
    else if(!evading&&health.CanMove)move=PatrolVelocity();
    if(!loaded&&reloadUntil==0&&health.CanShoot&&engaged){reloadUntil=Time.time+1.67f;if(native)native.Reload();}
    if(reloadUntil>0&&!health.CanShoot)reloadUntil+=Time.deltaTime;   // frozen/shocked: reload paused
    if(reloadUntil>0&&Time.time>=reloadUntil){loaded=true;reloadUntil=0;}
    if(loaded&&!evading&&health.CanShoot&&engaged&&distance<attackRange&&forward.sqrMagnitude>.05f&&Quaternion.Angle(transform.rotation,Quaternion.LookRotation(forward))<8f&&Time.time>=fireAt)
    {
     Vector3 origin=weapon?weapon.position+weapon.forward*.65f:transform.position+Vector3.up*1.4f+transform.forward*.85f,aim=target.transform.position+Vector3.up;
     // Lead the target a little (where it will be when the slug arrives).
     var targetMotor=target.GetComponent<CharacterController>();if(targetMotor)aim+=Vector3.ProjectOnPlane(targetMotor.velocity,Vector3.up)*Mathf.Clamp(distance/24f,0,.9f)*.7f;
     bool blocked=Physics.Linecast(origin,aim,out var obstruction,~0,QueryTriggerInteraction.Ignore)&&!obstruction.transform.IsChildOf(transform)&&obstruction.collider.GetComponentInParent<PlayerHealth>()!=target;
     if(!blocked&&TryLaunch(origin,aim)){loaded=false;fireAt=Time.time+shotCooldown;}
     else fireAt=Time.time+.2f;
    }
   }
   if(motor.isGrounded&&vertical<0)vertical=-2;vertical+=Physics.gravity.y*Time.deltaTime;Vector3 before=transform.position;
   motor.Move((move+Vector3.up*vertical)*Time.deltaTime);
   // Unstick: if trying to move but not going anywhere, change tactic; if sunk below the ground, pop back up.
   float wanted=new Vector2(move.x,move.z).magnitude*Time.deltaTime,got=Vector3.ProjectOnPlane(transform.position-before,Vector3.up).magnitude;
   stuckTime=wanted>.01f&&got<wanted*.15f?stuckTime+Time.deltaTime:0;
   if(stuckTime>.6f){stuckTime=0;tacticUntil=Time.time+.45f;movementBlockedUntil=Time.time+.3f;flankSide=-flankSide;}
   if(Time.time>=groundCheckAt){groundCheckAt=Time.time+.25f;SnapToGround();}
   MotionVelocity=move.sqrMagnitude>0 && Time.deltaTime>0?Vector3.ProjectOnPlane(transform.position-before,Vector3.up)/Time.deltaTime:Vector3.zero;
   recoil=Mathf.MoveTowards(recoil,0,Time.deltaTime*3);if(model&&pose){model.localPosition=modelOrigin+Vector3.up*(Mathf.Sin(Time.time*10)*.035f*Mathf.Clamp01(MotionSpeed))+Vector3.back*(recoil*.12f);}
  }
  Vector3 FindFreeSpawn(Vector3 requested)
  {
   Vector3 basePosition=GroundedPosition(requested);if(!SpawnBlocked(basePosition))return basePosition;
   for(int ring=1;ring<=8;ring++)for(int i=0;i<12;i++)
   {
    float angle=(i+ring*.37f)*Mathf.PI/6;Vector3 candidate=requested+new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*(ring*1.5f);
    candidate=GroundedPosition(candidate);if(Mathf.Abs(candidate.y-basePosition.y)>2f)continue;
    if(!SpawnBlocked(candidate))return candidate;
   }
   return basePosition;
  }
  Vector3 GroundedPosition(Vector3 position)
  {
   if(!motor)return position;
   int count=Physics.RaycastNonAlloc(position+Vector3.up*2.5f,Vector3.down,spawnGroundHits,7f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
   RaycastHit floor=default;float nearest=float.PositiveInfinity;
   for(int i=0;i<count;i++)
   {
    var hit=spawnGroundHits[i];var collider=hit.collider;
    if(!collider||collider.transform==transform||collider.transform.IsChildOf(transform)||collider.GetComponentInParent<EnemyCombatant>()||collider.GetComponentInParent<PlayerHealth>()||hit.normal.y<.55f)continue;
    if(hit.distance<nearest){nearest=hit.distance;floor=hit;}
   }
   if(float.IsPositiveInfinity(nearest))return position;
   position.y=floor.point.y+motor.skinWidth+.02f-(motor.center.y-motor.height*.5f);return position;
  }
  bool SpawnBlocked(Vector3 position)
  {
   if(!motor)return false;float radius=Mathf.Max(.12f,motor.radius*.82f),half=Mathf.Max(0,motor.height*.5f-radius);
   Vector3 center=position+motor.center;var overlaps=Physics.OverlapCapsule(center+Vector3.up*half,center-Vector3.up*half,radius,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
   foreach(var c in overlaps)
   {
    if(!c||c.transform==transform||c.transform.IsChildOf(transform)||c.GetComponentInParent<EnemyCombatant>()||c.GetComponentInParent<PlayerHealth>())continue;
    return true;
   }
   return false;
  }
  void SnapToGround()
  {
   if(!motor||!motor.enabled||!health)return;
   float feet=transform.position.y+motor.center.y-motor.height*.5f;
   if(!Physics.Raycast(new Vector3(transform.position.x,feet+1.2f,transform.position.z),Vector3.down,out var floor,3.5f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore)||floor.transform.IsChildOf(transform)||floor.collider.GetComponentInParent<EnemyCombatant>()||floor.normal.y<.45f)return;
   float targetFeet=floor.point.y+motor.skinWidth+.02f,delta=targetFeet-feet;
   if(Mathf.Abs(delta)<.08f||delta<0&&!health.IsDead&&!motor.isGrounded)return;
   // Correct the feet in place without toggling the CharacterController. Disabling it here
   // resets grounded/contact state every few frames and makes both movement and walk animation hitch.
   motor.Move(Vector3.up*delta);vertical=0;
  }
  Vector3 PatrolVelocity()
  {
   if(Time.time<movementBlockedUntil)return Vector3.zero;
   Vector3 goal=home+Quaternion.Euler(0,patrolPoint*90f,0)*Vector3.forward*5f;
   Vector3 offset=Vector3.ProjectOnPlane(goal-transform.position,Vector3.up);
   if(offset.sqrMagnitude<.16f){patrolPoint=(patrolPoint+1)%4;return Vector3.zero;}
   Quaternion facing=Quaternion.LookRotation(offset);
   transform.rotation=Quaternion.RotateTowards(transform.rotation,facing,240f*Time.deltaTime);
   if(Quaternion.Angle(transform.rotation,facing)>2f)return Vector3.zero;
   if(!CanStep(transform.forward)){patrolPoint=(patrolPoint+1)%4;movementBlockedUntil=Time.time+.35f;return Vector3.zero;}
   return transform.forward*Mathf.Min(speed*health.SpeedMultiplier,offset.magnitude/Mathf.Max(.001f,Time.deltaTime));
  }

  // Combat tactics: advance to range, strafe, take a new angle, hold still to aim, back off when rushed,
  // and chase around obstacles when there is no line of sight. Picks a new tactic every 1-2.5 s.
  enum Tactic{Advance,Strafe,Reposition,Hold,Retreat,Chase}
  Tactic tactic;float tacticUntil;Vector3 repositionGoal;
  bool HasLineOfSight()
  {
   if(!target)return false;Vector3 from=transform.position+Vector3.up*1.4f,to=target.transform.position+Vector3.up;
   return !(Physics.Linecast(from,to,out var hit,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore)&&!hit.transform.IsChildOf(transform)&&!hit.collider.GetComponentInParent<PlayerHealth>());
  }
  void PickTactic(Vector3 toward,float distance)
  {
   float preferred=attackRange*.6f;bool los=HasLineOfSight();float roll=Random.value;
   if(!los)tactic=Tactic.Chase;
   else if(distance>attackRange*.95f)tactic=Tactic.Advance;
   else if(distance<preferred*.5f||health.Health<health.maxHealth*.3f&&roll<.5f)tactic=Tactic.Retreat;
   else tactic=roll<.4f?Tactic.Strafe:roll<.75f?Tactic.Reposition:Tactic.Hold;
   flankSide=Random.value<.5f?-1:1;
   tacticUntil=Time.time+(tactic==Tactic.Hold?Random.Range(.7f,1.3f):Random.Range(1.2f,2.6f));
   if(tactic==Tactic.Reposition)repositionGoal=target.transform.position+Quaternion.Euler(0,Random.Range(-75f,75f),0)*(-toward)*preferred;
  }
  Vector3 CombatDirection(Vector3 toward,float distance)
  {
   if(Time.time<movementBlockedUntil)return Vector3.zero;
   if(Time.time>=tacticUntil)PickTactic(toward,distance);
   Vector3 lateral=Vector3.Cross(Vector3.up,toward)*flankSide,desired;
   float preferred=attackRange*.6f;
   switch(tactic)
   {
    case Tactic.Advance:desired=toward+lateral*.3f;break;
    case Tactic.Chase:desired=toward+lateral*.6f;break;
    case Tactic.Retreat:desired=-toward+lateral*.6f;break;
    case Tactic.Strafe:desired=lateral+toward*Mathf.Clamp((distance-preferred)/preferred,-.5f,.5f);break;
    case Tactic.Reposition:{var to=Vector3.ProjectOnPlane(repositionGoal-transform.position,Vector3.up);if(to.magnitude<1.5f){tacticUntil=Time.time+.35f;return Vector3.zero;}desired=to;break;}
    default:return Vector3.zero;   // Hold: plant feet and aim
   }
   desired=Vector3.ProjectOnPlane(desired,Vector3.up).normalized;
   foreach(float a in new[]{0f,35f,-35f,70f,-70f,110f,-110f}){var d=Quaternion.Euler(0,a,0)*desired;if(CanStep(d))return d;}
   tacticUntil=Time.time+.45f;movementBlockedUntil=Time.time+.3f;flankSide=-flankSide;return Vector3.zero;
  }

  bool CanStep(Vector3 direction)
  {
   Vector3 ahead=transform.position+direction*(motor.radius+.8f);
   if(Physics.SphereCast(transform.position+Vector3.up*.9f,motor.radius*.8f,direction,out _,.8f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))return false;
   return Physics.Raycast(ahead+Vector3.up*.7f,Vector3.down,out var floor,1.7f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore)
       &&Vector3.Angle(floor.normal,Vector3.up)<=motor.slopeLimit;
  }
  void LateUpdate(){if(!native&&pose&&health&&!health.IsDead&&!health.IsKnockedDown)pose.Pose(MotionSpeed,false,0,weapon,recoil,0);}
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
    string species=slugSlots[i];if(SlugCatalog.BaseSpecies(species)=="Goon")continue;
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

