using UnityEngine;
namespace Nexoterra.World
{
 [RequireComponent(typeof(Animator))]
 public sealed class HumanoidWeaponIK : MonoBehaviour
 {
  public Vector3 supportOffset;public Quaternion supportRotation=Quaternion.identity;public bool weaponHandIK;
  static readonly AvatarIKGoal[] goals={AvatarIKGoal.LeftHand,AvatarIKGoal.RightHand,AvatarIKGoal.LeftFoot,AvatarIKGoal.RightFoot};
  Animator animator;PlayerAbilities abilities;EnemyCombatant enemy;CharacterController motor;NexoterraThirdPerson player;NativePlayerAnimation driver;
  void Awake(){animator=GetComponent<Animator>();abilities=GetComponentInParent<PlayerAbilities>();enemy=GetComponentInParent<EnemyCombatant>();motor=GetComponentInParent<CharacterController>();player=GetComponentInParent<NexoterraThirdPerson>();driver=GetComponentInParent<NativePlayerAnimation>();}
  void OnAnimatorIK(int layer)
  {
   if(!animator.isHuman)return;
   foreach(var goal in goals){animator.SetIKPositionWeight(goal,0);animator.SetIKRotationWeight(goal,0);}
   animator.SetLookAtWeight(0);
   var state=animator.GetCurrentAnimatorStateInfo(0);
   if(state.IsName("CaidaAtras")||state.IsName("levantarsedecaida"))return;
   if((abilities&&abilities.IsDodging)||state.IsName("RollLeft")||state.IsName("RollRight")||state.IsName("Death")||(enemy&&(enemy.IsDead||enemy.IsKnockedDown)))return;
   float weight=driver?driver.AimWeight:enemy?animator.GetLayerWeight(Mathf.Max(0,animator.GetLayerIndex("AimUpperBody"))):0;
   var right=animator.GetBoneTransform(HumanBodyBones.RightHand);
   if(weaponHandIK&&right&&weight>.001f){
    float pitch=player&&player.view?Mathf.Clamp(Mathf.DeltaAngle(0,player.view.transform.eulerAngles.x),-45,60):0;
    var chest=animator.GetBoneTransform(HumanBodyBones.Chest);Vector3 pivot=chest?chest.position:transform.position+Vector3.up*1.35f;
    var aim=Quaternion.AngleAxis(pitch*weight,transform.right);
    animator.SetIKPositionWeight(AvatarIKGoal.RightHand,weight);animator.SetIKRotationWeight(AvatarIKGoal.RightHand,weight);
    animator.SetIKPosition(AvatarIKGoal.RightHand,pivot+aim*(right.position-pivot));animator.SetIKRotation(AvatarIKGoal.RightHand,aim*right.rotation);
    if(supportOffset.sqrMagnitude>.001f){animator.SetIKPositionWeight(AvatarIKGoal.LeftHand,weight);animator.SetIKRotationWeight(AvatarIKGoal.LeftHand,weight);animator.SetIKPosition(AvatarIKGoal.LeftHand,pivot+aim*(right.TransformPoint(supportOffset)-pivot));animator.SetIKRotation(AvatarIKGoal.LeftHand,aim*right.rotation*supportRotation);}
   }
   if(motor&&motor.isGrounded){Foot(AvatarIKGoal.LeftFoot,HumanBodyBones.LeftFoot);Foot(AvatarIKGoal.RightFoot,HumanBodyBones.RightFoot);}
  }
  void Foot(AvatarIKGoal goal,HumanBodyBones bone){
   var foot=animator.GetBoneTransform(bone);if(!foot)return;
   if(!Physics.Raycast(foot.position+Vector3.up*.25f,Vector3.down,out var hit,.5f,1,QueryTriggerInteraction.Ignore)||hit.normal.y<.65f||hit.transform.IsChildOf(motor.transform))return;
   float sole=goal==AvatarIKGoal.LeftFoot?animator.leftFeetBottomHeight:animator.rightFeetBottomHeight;
   float delta=hit.point.y+sole-foot.position.y;
   if(delta<-.09f||delta>.15f)return;
   float weight=.65f*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.02f,.09f,Mathf.Max(0,-delta))));
   animator.SetIKPositionWeight(goal,weight);animator.SetIKRotationWeight(goal,weight*.5f);
   animator.SetIKPosition(goal,new Vector3(foot.position.x,hit.point.y+sole,foot.position.z));animator.SetIKRotation(goal,Quaternion.FromToRotation(Vector3.up,hit.normal)*foot.rotation);
  }
 }
}
