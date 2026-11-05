using UnityEngine;
using UnityEngine.InputSystem;

namespace Nexoterra.World
{
 [DefaultExecutionOrder(100)]
 public sealed class NativePlayerAnimation : MonoBehaviour
 {
  public Animator male,female;
  public RuntimeAnimatorController playerController;
  public float walkReferenceSpeed=2.8f,runReferenceSpeed=3.8f,rollClipDuration=2.1f;
  public Transform weapon;
  public Vector3 maleGrip,femaleGrip;
  public Quaternion maleGripRotation=Quaternion.identity,femaleGripRotation=Quaternion.identity;
  public float gripHeightCorrection=.09f;
  public WaterAnimationSensor waterSensor;
  public float reloadDuration=1.1f,shootDuration=.32f;
  public Vector3 weaponSocketOffset=new Vector3(0,-.025f,.04f);
  public float AimWeight {get;private set;}
  public bool IsReloading=>reloadRemaining>0;
  public bool IsLoaded {get;private set;}
  Vector3 weaponScale;Transform weaponSocket;
  NexoterraThirdPerson player;CharacterController motor;PlayerHealth health;PlayerAbilities abilities;
  string state,upperState;Animator current;float reloadRemaining,shootRemaining;
  void Awake(){player=GetComponent<NexoterraThirdPerson>();motor=GetComponent<CharacterController>();health=GetComponent<PlayerHealth>();abilities=GetComponent<PlayerAbilities>();if(!waterSensor)waterSensor=GetComponent<WaterAnimationSensor>();if(weapon)weaponScale=weapon.lossyScale;}
  public void BeginReload(float duration){IsLoaded=false;reloadRemaining=Mathf.Max(.1f,duration);shootRemaining=0;}
  public void PlayShot(){IsLoaded=false;shootRemaining=shootDuration;reloadRemaining=0;}
  void Update()
  {
   if(!player||!player.male||!player.female)return;
   var selected=player.female.activeSelf?female:male;if(!selected)return;
   if(!selected.runtimeAnimatorController&&playerController){selected.runtimeAnimatorController=playerController;selected.Rebind();selected.Update(0);state=null;upperState=null;}
   if(!selected.runtimeAnimatorController)return;
   if(current!=selected){current=selected;state=null;upperState=null;AttachWeapon();}
   float speed=new Vector2(motor.velocity.x,motor.velocity.z).magnitude;
   bool roll=abilities&&abilities.IsDodging;
   bool dead=health&&health.IsDead;
   string next=dead?"Death":roll?(abilities.DodgeLeft?"RollLeft":"RollRight"):speed>.15f?(speed>3.4f?"Run":"Walk"):"Idle";
   bool aim=!GameSession.Blocked&&!dead&&!roll&&Mouse.current!=null&&Mouse.current.rightButton.isPressed&&Cursor.lockState==CursorLockMode.Locked&&!(GetComponent<SlugInventory>()&&GetComponent<SlugInventory>().IsOpen);
   if(reloadRemaining>0 && reloadRemaining<=Time.deltaTime)IsLoaded=true;
   reloadRemaining=Mathf.Max(0,reloadRemaining-Time.deltaTime);shootRemaining=Mathf.Max(0,shootRemaining-Time.deltaTime);
   AimWeight=Mathf.MoveTowards(AimWeight,aim?1:0,Time.deltaTime/.2f);
   if(roll||dead){AimWeight=0;reloadRemaining=shootRemaining=0;}
   int layer=current.GetLayerIndex("AimUpperBody");
   if(layer>=0){bool overridePose=reloadRemaining>0||shootRemaining>0;current.SetLayerWeight(layer,Mathf.Max(AimWeight,overridePose?1:0));
    string upper=reloadRemaining>0?"ReloadUpper":shootRemaining>0?"ShootUpper":aim?"AimPose":null;
    if(upper!=upperState&&upper!=null){current.CrossFadeInFixedTime(upper,.09f,layer);upperState=upper;}
    if(upper==null)upperState=null;
   }
   if(next!=state){current.CrossFadeInFixedTime(next,next.StartsWith("Roll")?.05f:.18f,0);state=next;}
   current.speed=GameSession.Blocked?0:IsReloading?1:next=="Run"?Mathf.Clamp(speed/Mathf.Max(.1f,runReferenceSpeed),.35f,1.6f):next=="Walk"?Mathf.Clamp(speed/Mathf.Max(.1f,walkReferenceSpeed),.35f,1.6f):next.StartsWith("Roll")?rollClipDuration/Mathf.Max(.1f,abilities.dodgeDuration):1;
  }
  void LateUpdate(){PositionWeapon();}
  void PositionWeapon(){
   if(!weapon||!current||!player||!player.visuals)return;
   var hand=current.GetBoneTransform(HumanBodyBones.RightHand);if(!hand)return;
   Vector3 facing=player.visuals.forward;
   Quaternion bodyRotation=Quaternion.LookRotation(facing,Vector3.up);
   Quaternion restingRotation=bodyRotation*Quaternion.Euler(38,0,0);
   Quaternion aimRotation=player.view?Quaternion.Slerp(restingRotation,player.view.transform.rotation,AimWeight):restingRotation;
   if(!weaponSocket||weaponSocket.parent!=hand)AttachWeapon();
   if(weaponSocket){weaponSocket.localPosition=current==female?femaleGrip:maleGrip;weaponSocket.localRotation=current==female?femaleGripRotation:maleGripRotation;}
  }
  void AttachWeapon(){
   if(!weapon||!current)return;
   var hand=current.GetBoneTransform(HumanBodyBones.RightHand);if(!hand)return;
   weaponSocket=hand.Find("BlasterSocket");
   if(!weaponSocket){var socket=new GameObject("BlasterSocket");weaponSocket=socket.transform;weaponSocket.SetParent(hand,false);}
   weaponSocket.localPosition=Vector3.zero;weaponSocket.localRotation=Quaternion.identity;weaponSocket.localScale=Vector3.one;
   weapon.SetParent(weaponSocket,false);weapon.localPosition=Vector3.zero;weapon.localRotation=Quaternion.identity;
   Vector3 scale=weaponSocket.lossyScale;
   weapon.localScale=new Vector3(weaponScale.x/Mathf.Max(.001f,scale.x),weaponScale.y/Mathf.Max(.001f,scale.y),weaponScale.z/Mathf.Max(.001f,scale.z));
  }
 }
}
