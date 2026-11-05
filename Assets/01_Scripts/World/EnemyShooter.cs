using UnityEngine;
namespace Nexoterra.World
{
 [RequireComponent(typeof(EnemyCombatant),typeof(CharacterController))]
 public sealed class EnemyShooter : MonoBehaviour
 {
  public SlugCatalog catalog;
  public bool boss;
  public float aggroRange=28,attackRange=18,speed=2.2f,shotCooldown=3.5f,respawnSeconds=300;
  public Transform model,weapon;
  public HumanoidPose pose;
  public float MotionSpeed {get;private set;}
  EnemyCombatant health;CharacterController motor;PlayerHealth target;
  int volley;float reloadUntil;bool loaded;
  static readonly string[] commonLoadout={"Infierno","Arana","ice","Carnero","tazerling","Armashelt"};
  static readonly string[] bossLoadout={"Infierno","ice","tazerling","Arana","Tornado"};
  public string NextSpecies(){var choices=boss?bossLoadout:commonLoadout;return choices[volley++%choices.Length];}
  Vector3 home,modelOrigin;float fireAt,vertical,deadTime,recoil;
  public float Recoil=>recoil;
  public bool IsAiming {get;private set;}
  public Vector3 AimDirection=>target&&weapon?(target.transform.position+Vector3.up-weapon.position).normalized:transform.forward;
  void Start(){health=GetComponent<EnemyCombatant>();motor=GetComponent<CharacterController>();target=FindAnyObjectByType<PlayerHealth>();home=transform.position;if(model)modelOrigin=model.localPosition;volley=Mathf.Abs(Mathf.RoundToInt(home.x*3+home.z*7))%commonLoadout.Length;fireAt=Time.time+2;}
  void Update()
  {
   IsAiming=false;if(GameSession.Blocked)return;
   if(health.IsDead){deadTime+=Time.deltaTime;if(!boss&&deadTime>=respawnSeconds){motor.enabled=false;transform.position=home;motor.enabled=true;health.Revive();deadTime=0;}return;}
   Vector3 move=Vector3.zero;var native=GetComponent<NativeEnemyAnimation>();bool evading=native&&native.IsDodging;
   if(target&&health.CanAct&&!target.IsDead)
   {
    float distance=Vector3.Distance(transform.position,target.transform.position);
    bool engaged=distance<aggroRange&&Vector3.Distance(transform.position,home)<35;
    IsAiming=engaged&&distance<attackRange&&health.CanShoot;
    Vector3 goal=engaged?target.transform.position:home+new Vector3(Mathf.Sin(Time.time*.2f)*5,0,Mathf.Cos(Time.time*.2f)*5);
    Vector3 forward=goal-transform.position;forward.y=0;
    if(forward.sqrMagnitude>.05f)transform.rotation=Quaternion.Slerp(transform.rotation,Quaternion.LookRotation(forward),1-Mathf.Exp(-5*Time.deltaTime));
    if(!evading&&health.CanMove&&(!engaged||distance>attackRange*.7f))move=forward.normalized*speed*health.SpeedMultiplier;
    if(!loaded&&reloadUntil==0&&health.CanShoot&&engaged){reloadUntil=Time.time+1.67f;if(native)native.Reload();}
    if(reloadUntil>0&&Time.time>=reloadUntil){loaded=true;reloadUntil=0;}
    if(loaded&&!evading&&health.CanShoot&&engaged&&distance<attackRange&&Time.time>=fireAt)
    {
     Vector3 origin=weapon?weapon.position+weapon.forward*.65f:transform.position+Vector3.up*1.4f+transform.forward*.85f,aim=target.transform.position+Vector3.up;
     bool blocked=false;foreach(var hit in Physics.RaycastAll(origin,(aim-origin).normalized,Vector3.Distance(origin,aim),~0,QueryTriggerInteraction.Ignore))if(!hit.transform.IsChildOf(transform)&&hit.collider.GetComponentInParent<PlayerHealth>()!=target){blocked=true;break;}
     if(!blocked){Shoot(origin,aim);loaded=false;}fireAt=Time.time+shotCooldown;
    }
   }
   if(motor.isGrounded&&vertical<0)vertical=-2;vertical+=Physics.gravity.y*Time.deltaTime;motor.Move((move+Vector3.up*vertical)*Time.deltaTime);MotionSpeed=move.magnitude;
   recoil=Mathf.MoveTowards(recoil,0,Time.deltaTime*3);if(model&&pose){model.localPosition=modelOrigin+Vector3.up*(Mathf.Sin(Time.time*10)*.035f*Mathf.Clamp01(MotionSpeed))+Vector3.back*(recoil*.12f);}
  }
  void LateUpdate(){if(!GetComponent<NativeEnemyAnimation>()&&pose&&health&&!health.IsDead&&!health.IsKnockedDown)pose.Pose(MotionSpeed,false,0,weapon,recoil,0);}
  void Shoot(Vector3 origin,Vector3 aim)
  {
   if(!catalog)return;var baseSlug=catalog.Find(NextSpecies());if(baseSlug==null)return;
   SlugDefinition slug=baseSlug;
   if(boss){var combat=SlugCombatProfile.Default(baseSlug.id);combat.impactDamage*=1.35f;combat.burnDamagePerSecond*=1.35f;slug=new SlugDefinition{id="Malvada_"+baseSlug.id,normal=baseSlug.normal,transformed=baseSlug.transformed,combat=combat,combatConfigured=true};}
   var shot=new GameObject(boss?"Babosa malvada":"Babosa enemiga").AddComponent<SlugProjectile>();shot.transform.position=origin;shot.speed=boss?22:18;shot.transformDistance=5;shot.maxDistance=60;shot.LaunchHostile(slug,(aim-origin).normalized,transform);recoil=1;
  }
 }
}
