using UnityEngine;
using UnityEngine.InputSystem;
namespace Nexoterra.World
{
 public sealed class NativePlayerAnimation : MonoBehaviour
 {
  public Animator male,female;
  public float walkReferenceSpeed=2.8f,runReferenceSpeed=5.8f,rollClipDuration=2.1f;
  public Transform weapon;
  public Vector3 maleGrip,femaleGrip;
  public Quaternion maleGripRotation=Quaternion.identity,femaleGripRotation=Quaternion.identity;
  public float gripHeightCorrection=.09f;
  public float AimWeight {get;private set;}
  Vector3 weaponScale;
  NexoterraThirdPerson player;CharacterController motor;PlayerHealth health;PlayerAbilities abilities;
  string state;Animator current;
  void Awake(){player=GetComponent<NexoterraThirdPerson>();motor=GetComponent<CharacterController>();health=GetComponent<PlayerHealth>();abilities=GetComponent<PlayerAbilities>();if(weapon)weaponScale=weapon.lossyScale;}
  void Update()
  {
   var selected=player.female.activeSelf?female:male;if(!selected)return;
   if(current!=selected){current=selected;state=null;if(weapon){var hand=current.GetBoneTransform(HumanBodyBones.RightHand);if(hand){weapon.SetParent(hand,false);weapon.localPosition=current==male?maleGrip:femaleGrip;weapon.localRotation=current==male?maleGripRotation:femaleGripRotation;var scale=hand.lossyScale;weapon.localPosition+=Vector3.Scale(weapon.localRotation*(Vector3.down*gripHeightCorrection+Vector3.left*.01f),new Vector3(1/scale.x,1/scale.y,1/scale.z));weapon.localScale=new Vector3(weaponScale.x/scale.x,weaponScale.y/scale.y,weaponScale.z/scale.z);}}}
   float speed=new Vector2(motor.velocity.x,motor.velocity.z).magnitude;
   bool roll=abilities&&abilities.IsDodging;
   string next=health.IsDead?"Death":roll?(abilities.DodgeLeft?"RollLeft":"RollRight"):speed>.15f?(speed>3.4f?"Run":"Walk"):"Idle";
   bool aim=!GameSession.Blocked&&!health.IsDead&&!roll&&Mouse.current!=null&&Mouse.current.rightButton.isPressed&&Cursor.lockState==CursorLockMode.Locked&&!(GetComponent<SlugInventory>()&&GetComponent<SlugInventory>().IsOpen);
   AimWeight=Mathf.MoveTowards(AimWeight,aim?1:0,Time.deltaTime/ .2f);
   // Rolls and death retain their original full-body motion, with no upper-body override.
   if(roll||health.IsDead)AimWeight=0;
   int layer=current.GetLayerIndex("AimUpperBody");if(layer>=0)current.SetLayerWeight(layer,AimWeight);
   if(next!=state){current.CrossFadeInFixedTime(next,next.StartsWith("Roll")?.05f:.18f,0);state=next;}
   current.speed=GameSession.Blocked?0:next=="Run"?Mathf.Clamp(speed/Mathf.Max(.1f,runReferenceSpeed),.35f,1.6f):next=="Walk"?Mathf.Clamp(speed/Mathf.Max(.1f,walkReferenceSpeed),.35f,1.6f):next.StartsWith("Roll")?rollClipDuration/abilities.dodgeDuration:1;
  }
 }
}
