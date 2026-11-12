using UnityEngine;
namespace Nexoterra.World
{
 public sealed class NativeEnemyAnimation : MonoBehaviour
 {
  public float walkReferenceSpeed=2.2f;float aimWeight;
  public Animator animator;public Vector3 grip;public Quaternion gripRotation=Quaternion.identity;public Vector3 weaponSocketOffset=new Vector3(0,-.025f,.04f);
  public bool copyPlayerGrip=true;
  Transform weaponSocket,hips,spine;
  bool backpedalling;
  float strideYaw;
  EnemyShooter ai;EnemyCombatant health;CharacterController motor;string state;float dodge,ready,reloadRemaining;Vector3 direction;
  public bool IsDodging=>dodge>0;
  void Start()
  {
   ai=GetComponent<EnemyShooter>();health=GetComponent<EnemyCombatant>();motor=GetComponent<CharacterController>();
   if(animator&&animator.isHuman){hips=animator.GetBoneTransform(HumanBodyBones.Hips);spine=animator.GetBoneTransform(HumanBodyBones.Spine);}
   AttachWeapon();
  }
  public void Reload(){reloadRemaining=1.67f;if(!animator)return;int layer=animator.GetLayerIndex("AimUpperBody");if(layer>=0){animator.SetLayerWeight(layer,1);animator.CrossFadeInFixedTime("ReloadUpper",.09f,layer);}}
  void Update()
  {
   if(!animator||GameSession.Blocked)return;
   if(!health.CanMove)dodge=0;
   if(reloadRemaining>0){reloadRemaining=Mathf.Max(0,reloadRemaining-Time.deltaTime);if(reloadRemaining==0){int upper=animator.GetLayerIndex("AimUpperBody");if(upper>=0)animator.CrossFadeInFixedTime("AimPose",.12f,upper);}}
   // Only the boss may evade; regular soldiers never enter a roll state.
   if(ai.boss&&health.CanMove&&Time.time>=ready&&dodge<=0)
   {
    var player=FindAnyObjectByType<NexoterraThirdPerson>();
    if(player&&Vector3.Distance(player.transform.position,transform.position)<22){direction=transform.right*(Random.value>.5f?1:-1);dodge=PlayerAbilities.QuickRollDuration;ready=Time.time+9;}
   }
   if(dodge>0){motor.Move(direction*(3.675f*Mathf.Min(Time.deltaTime,dodge)/PlayerAbilities.QuickRollDuration));dodge=Mathf.Max(0,dodge-Time.deltaTime);}
   bool incapacitated=health.IsKnockedDown||health.IsStunned;
   bool resistingWind=health.Controls&&health.Controls.WindRemaining>0&&ai.IsPursuing;
   string movement=resistingWind?"Run":ai.MotionSpeed<=.1f?"Idle":ai.IsPursuing && health.SpeedMultiplier>=.99f?"Run":"Walk";
   float desiredYaw=0;
   if(ai.IsPursuing && !resistingWind && ai.MotionSpeed>.1f)
   {
    Vector3 local=transform.InverseTransformDirection(ai.MotionVelocity.normalized);
    if(local.z<-.32f)backpedalling=true;
    else if(local.z>-.08f)backpedalling=false;
    if(backpedalling){movement="WalkBack";local=-local;}
    desiredYaw=Mathf.Atan2(local.x,local.z)*Mathf.Rad2Deg;
   }
   else backpedalling=false;
   string knockdown=health.Controls?health.Controls.KnockdownPose:null;
   string next=health.IsDead?"Death":knockdown!=null?knockdown:incapacitated?"Idle":IsDodging?(Vector3.Dot(direction,transform.right)<0?"RollLeft":"RollRight"):movement;
   bool stepping=next=="Walk"||next=="WalkBack"||next=="Run";
   strideYaw=ai.IsPursuing&&stepping?Mathf.MoveTowardsAngle(strideYaw,desiredYaw,540f*Time.deltaTime):0;
   aimWeight=Mathf.MoveTowards(aimWeight,ai.IsAiming&&!health.IsDead&&!incapacitated&&!IsDodging?1:0,Time.deltaTime/.2f);int aimLayer=animator.GetLayerIndex("AimUpperBody");if(aimLayer>=0)animator.SetLayerWeight(aimLayer,incapacitated?0:Mathf.Max(aimWeight,reloadRemaining>0?1:0));
   if(next!=state){animator.CrossFadeInFixedTime(next,.12f);state=next;}
   animator.speed=knockdown!=null?1:health.Controls&&health.Controls.FreezeRemaining>0?0:next.StartsWith("Roll")?(40f/30f)/PlayerAbilities.QuickRollDuration:reloadRemaining>0?1:(next=="Walk"||next=="WalkBack")?Mathf.Clamp(ai.MotionSpeed/Mathf.Max(.1f,walkReferenceSpeed),.25f,1.8f):1;
  }
  void LateUpdate()
  {
   // Turn the animated stride into the travel direction while retaining the aiming pose.
   if(hips&&spine&&Mathf.Abs(strideYaw)>.01f&&!GameSession.Blocked&&animator.speed>0)
   {
    Quaternion upperPose=spine.rotation;
    hips.rotation=Quaternion.AngleAxis(strideYaw,Vector3.up)*hips.rotation;
    spine.rotation=upperPose;
   }
   if(!animator||!ai||!ai.weapon)return;var hand=animator.GetBoneTransform(HumanBodyBones.RightHand);if(!hand)return;
   if(!weaponSocket||weaponSocket.parent!=hand)AttachWeapon();if(!weaponSocket)return;
  }
  void AttachWeapon()
  {
   if(!animator||!ai||!ai.weapon)return;var hand=animator.GetBoneTransform(HumanBodyBones.RightHand);if(!hand)return;
   Vector3 scale=ai.weapon.lossyScale;weaponSocket=hand.Find("BlasterSocket");
   if(!weaponSocket){var socket=new GameObject("BlasterSocket");weaponSocket=socket.transform;weaponSocket.SetParent(hand,false);}
   weaponSocket.localScale=Vector3.one;
   weaponSocket.localPosition=grip;
   weaponSocket.localRotation=gripRotation;
   var player=FindAnyObjectByType<NativePlayerAnimation>();
   if(copyPlayerGrip && player && player.maleMuzzle && player.maleGrip.sqrMagnitude>.000001f)
   {
    // The installer calibrates every wrist against the same aim pose and offset.
    // Transfer the edited weapon transform between those wrist coordinate systems.
    var reference=player.maleMuzzle.parent;
    Quaternion wristBasis=gripRotation*Quaternion.Inverse(player.maleGripRotation);
    float wristScale=grip.magnitude/player.maleGrip.magnitude;
    weaponSocket.localPosition=wristBasis*(reference.localPosition*wristScale);
    weaponSocket.localRotation=wristBasis*reference.localRotation;
   }
   ai.weapon.SetParent(weaponSocket,false);ai.weapon.localPosition=Vector3.zero;ai.weapon.localRotation=Quaternion.identity;
   Vector3 s=weaponSocket.lossyScale;ai.weapon.localScale=new Vector3(scale.x/Mathf.Max(.001f,s.x),scale.y/Mathf.Max(.001f,s.y),scale.z/Mathf.Max(.001f,s.z));
  }
 }
}

