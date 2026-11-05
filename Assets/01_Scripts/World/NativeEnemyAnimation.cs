using UnityEngine;
namespace Nexoterra.World
{
 public sealed class NativeEnemyAnimation : MonoBehaviour
 {
  public float walkReferenceSpeed=2.2f;float aimWeight;
  public Animator animator;public Vector3 grip;public Quaternion gripRotation=Quaternion.identity;public Vector3 weaponSocketOffset=new Vector3(0,-.025f,.04f);
  Transform weaponSocket;
  EnemyShooter ai;EnemyCombatant health;CharacterController motor;string state;float dodge,ready,reloadRemaining;Vector3 direction;
  public bool IsDodging=>dodge>0;
  void Start()
  {
   ai=GetComponent<EnemyShooter>();health=GetComponent<EnemyCombatant>();motor=GetComponent<CharacterController>();
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
    if(player&&Vector3.Distance(player.transform.position,transform.position)<22){direction=transform.right*(Random.value>.5f?1:-1);dodge=1.05f;ready=Time.time+9;}
   }
   if(dodge>0){motor.Move(direction*(3.5f*Time.deltaTime));dodge=Mathf.Max(0,dodge-Time.deltaTime);}
   bool incapacitated=health.IsKnockedDown||health.IsStunned;
   string next=health.IsDead?"Death":incapacitated?"Idle":IsDodging?(Vector3.Dot(direction,transform.right)<0?"RollLeft":"RollRight"):ai.MotionSpeed>.1f?"Walk":"Idle";
   aimWeight=Mathf.MoveTowards(aimWeight,ai.IsAiming&&!health.IsDead&&!incapacitated&&!IsDodging?1:0,Time.deltaTime/.2f);int aimLayer=animator.GetLayerIndex("AimUpperBody");if(aimLayer>=0)animator.SetLayerWeight(aimLayer,incapacitated?0:Mathf.Max(aimWeight,reloadRemaining>0?1:0));
   if(next!=state){animator.CrossFadeInFixedTime(next,.12f);state=next;}
   animator.speed=health.Controls&&health.Controls.FreezeRemaining>0?0:next.StartsWith("Roll")?2:reloadRemaining>0?1:next=="Walk"?Mathf.Clamp(ai.MotionSpeed/Mathf.Max(.1f,walkReferenceSpeed),.25f,1.8f):1;
  }
  void LateUpdate()
  {
   if(!animator||!ai||!ai.weapon)return;var hand=animator.GetBoneTransform(HumanBodyBones.RightHand);if(!hand)return;
   if(!weaponSocket||weaponSocket.parent!=hand)AttachWeapon();if(!weaponSocket)return;
   Vector3 facing=transform.forward;if(facing.sqrMagnitude<.001f)facing=Vector3.forward;
   var bodyRotation=Quaternion.LookRotation(facing,Vector3.up);
   Vector3 aimDirection=ai.AimDirection;if(aimDirection.sqrMagnitude<.001f)aimDirection=facing;
   var rotation=ai.IsAiming?Quaternion.LookRotation(aimDirection,Vector3.up):bodyRotation*Quaternion.Euler(38,0,0);
   weaponSocket.localPosition=grip;weaponSocket.localRotation=gripRotation;
  }
  void AttachWeapon()
  {
   if(!animator||!ai||!ai.weapon)return;var hand=animator.GetBoneTransform(HumanBodyBones.RightHand);if(!hand)return;
   Vector3 scale=ai.weapon.lossyScale;weaponSocket=hand.Find("BlasterSocket");
   if(!weaponSocket){var socket=new GameObject("BlasterSocket");weaponSocket=socket.transform;weaponSocket.SetParent(hand,false);}
   weaponSocket.localPosition=Vector3.zero;weaponSocket.localRotation=Quaternion.identity;weaponSocket.localScale=Vector3.one;
   ai.weapon.SetParent(weaponSocket,false);ai.weapon.localPosition=Vector3.zero;ai.weapon.localRotation=Quaternion.identity;
   Vector3 s=weaponSocket.lossyScale;ai.weapon.localScale=new Vector3(scale.x/Mathf.Max(.001f,s.x),scale.y/Mathf.Max(.001f,s.y),scale.z/Mathf.Max(.001f,s.z));
  }
 }
}
