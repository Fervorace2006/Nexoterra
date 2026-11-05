using UnityEngine;
namespace Nexoterra.World
{
 public sealed class NativeEnemyAnimation : MonoBehaviour
 {
  public float walkReferenceSpeed=2.2f;float aimWeight;
  public Animator animator;public Vector3 grip;public Quaternion gripRotation=Quaternion.identity;
  EnemyShooter ai;EnemyCombatant health;CharacterController motor;string state;float dodge,ready;Vector3 direction;
  public bool IsDodging=>dodge>0;
  void Start()
  {
   ai=GetComponent<EnemyShooter>();health=GetComponent<EnemyCombatant>();motor=GetComponent<CharacterController>();
   if(animator&&ai.weapon){var scale=ai.weapon.lossyScale;var hand=animator.GetBoneTransform(HumanBodyBones.RightHand);if(hand){ai.weapon.SetParent(hand,false);ai.weapon.localPosition=grip;ai.weapon.localRotation=gripRotation;var s=hand.lossyScale;ai.weapon.localScale=new Vector3(scale.x/s.x,scale.y/s.y,scale.z/s.z);}}
  }
  void Update()
  {
   if(!animator||GameSession.Blocked)return;
   if(!health.CanMove)dodge=0;
   // Only the boss may evade; regular soldiers never enter a roll state.
   if(ai.boss&&health.CanMove&&Time.time>=ready&&dodge<=0)
   {
    var player=FindAnyObjectByType<NexoterraThirdPerson>();
    if(player&&Vector3.Distance(player.transform.position,transform.position)<22){direction=transform.right*(Random.value>.5f?1:-1);dodge=1.05f;ready=Time.time+9;}
   }
   if(dodge>0){motor.Move(direction*(3.5f*Time.deltaTime));dodge=Mathf.Max(0,dodge-Time.deltaTime);}
   string next=health.IsDead?"Death":IsDodging?(Vector3.Dot(direction,transform.right)<0?"RollLeft":"RollRight"):ai.MotionSpeed>.1f?"Walk":"Idle";
   aimWeight=Mathf.MoveTowards(aimWeight,ai.IsAiming&&!health.IsDead&&!IsDodging?1:0,Time.deltaTime/.2f);int aimLayer=animator.GetLayerIndex("AimUpperBody");if(aimLayer>=0)animator.SetLayerWeight(aimLayer,aimWeight);
   if(next!=state){animator.CrossFadeInFixedTime(next,.12f);state=next;}
   animator.speed=health.Controls&&health.Controls.FreezeRemaining>0?0:next.StartsWith("Roll")?2:next=="Walk"?Mathf.Clamp(ai.MotionSpeed/Mathf.Max(.1f,walkReferenceSpeed),.25f,1.8f):1;
  }
 }
}
