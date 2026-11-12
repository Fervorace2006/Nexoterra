using UnityEngine;
using UnityEngine.InputSystem;

namespace Nexoterra.World
{
 [DefaultExecutionOrder(100)]
 public sealed class NativePlayerAnimation : MonoBehaviour
 {
  public Animator male,female;
  public RuntimeAnimatorController playerController;
  public float walkReferenceSpeed=2.8f,runReferenceSpeed=3.8f,rollClipDuration=40f/30f;
  public Transform weapon;
  public Transform maleMuzzle,femaleMuzzle;
  public Vector3 maleGrip,femaleGrip;
  public Quaternion maleGripRotation=Quaternion.identity,femaleGripRotation=Quaternion.identity;
  public float gripHeightCorrection=.09f;
  public WaterAnimationSensor waterSensor;
  public float reloadDuration=1.1f,shootDuration=.32f;
  public Vector3 weaponSocketOffset=new Vector3(0,-.025f,.04f);
  public float AimWeight {get;private set;}
  public bool IsReloading=>reloadRemaining>0;
  bool loaded;string loadedSpecies;
  public bool IsLoaded {get{var inventory=GetComponent<SlugInventory>();var selected=inventory?inventory.SelectedDefinition():null;return loaded&&selected!=null&&selected.id==loadedSpecies&&!inventory.IsBusy(selected.id);}}
  Vector3 weaponScale;Transform weaponSocket;
  NexoterraThirdPerson player;CharacterController motor;PlayerHealth health;PlayerAbilities abilities;
  bool wasAirborne;float landRemaining,airTime,jumpWeight;string jumpState;
  string state,upperState;Animator current;float reloadRemaining,shootRemaining;
  void Awake(){player=GetComponent<NexoterraThirdPerson>();motor=GetComponent<CharacterController>();health=GetComponent<PlayerHealth>();abilities=GetComponent<PlayerAbilities>();if(!waterSensor)waterSensor=GetComponent<WaterAnimationSensor>();if(weapon)weaponScale=weapon.lossyScale;}
  public void BeginReload(float duration){if(IsReloading||IsLoaded)return;var inventory=GetComponent<SlugInventory>();var selected=inventory?inventory.SelectedDefinition():null;if(selected==null||inventory.IsBusy(selected.id))return;loadedSpecies=selected.id;loaded=false;reloadRemaining=Mathf.Max(.1f,duration);shootRemaining=0;}
  public void PlayShot(){loaded=false;shootRemaining=shootDuration;reloadRemaining=0;}
  void Update()
  {
   if(!player||!player.male||!player.female)return;
   var selected=player.female.activeSelf?female:male;if(!selected)return;
   if(!selected.runtimeAnimatorController&&playerController){selected.runtimeAnimatorController=playerController;selected.Rebind();selected.Update(0);state=null;upperState=null;}
   if(!selected.runtimeAnimatorController)return;
   if(current!=selected){current=selected;state=null;upperState=null;jumpState=null;AttachWeapon();}
   float speed=new Vector2(motor.velocity.x,motor.velocity.z).magnitude;
   bool roll=abilities&&abilities.IsDodging;
   bool dead=health&&health.IsDead;
   var effects=GetComponent<EnemyControlEffects>();
   string knockdown=effects?effects.KnockdownPose:null;
   // Jump/fall: ignore one-frame ground losses on steps and slopes; never while swimming.
   bool swimming=waterSensor&&waterSensor.Swimming;
   bool grounded=motor.isGrounded||swimming;
   airTime=grounded?0:airTime+Time.deltaTime;
   bool airborne=!grounded&&(motor.velocity.y>1f||airTime>.15f);
   if(airborne)wasAirborne=true;
   if(grounded&&wasAirborne){wasAirborne=false;if(!swimming)landRemaining=.18f;}
   if(!grounded)landRemaining=0;
   landRemaining=Mathf.Max(0,landRemaining-Time.deltaTime);
   string next=dead?"Death":knockdown!=null?knockdown:roll?(abilities.DodgeLeft?"RollLeft":"RollRight"):speed>.15f?(speed>3.4f?"Run":"Walk"):"Idle";
   // Jump clips play on a legs-only layer, so arms keep holding the pistol.
   int jumpLayer=current.GetLayerIndex("JumpLower");
   if(jumpLayer>=0){
    string jump=dead||roll||knockdown!=null?null:airborne?"JumpStart":landRemaining>0?"JumpLand":null;
    if(jump!=null&&jump!=jumpState)current.CrossFadeInFixedTime(jump,jump=="JumpLand"?.05f:.12f,jumpLayer);
    jumpState=jump;
    jumpWeight=Mathf.MoveTowards(jumpWeight,jump!=null?1:0,Time.deltaTime/(jump!=null?.08f:.12f));
    current.SetLayerWeight(jumpLayer,jumpWeight);
   }
   bool aim=knockdown==null&&!GameSession.Blocked&&!dead&&!roll&&Mouse.current!=null&&Mouse.current.rightButton.isPressed&&Cursor.lockState==CursorLockMode.Locked&&!(GetComponent<SlugInventory>()&&GetComponent<SlugInventory>().IsOpen);
   if(reloadRemaining>0 && reloadRemaining<=Time.deltaTime)loaded=true;
   reloadRemaining=Mathf.Max(0,reloadRemaining-Time.deltaTime);shootRemaining=Mathf.Max(0,shootRemaining-Time.deltaTime);
   AimWeight=Mathf.MoveTowards(AimWeight,aim?1:0,Time.deltaTime/.2f);
   if(roll||dead||knockdown!=null){AimWeight=0;reloadRemaining=shootRemaining=0;}
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
   if((maleMuzzle && femaleMuzzle)||!weapon||!current||!player||!player.visuals)return;
   var hand=current.GetBoneTransform(HumanBodyBones.RightHand);if(!hand)return;
   if(!weaponSocket||weaponSocket.parent!=hand)AttachWeapon();
  }

  // Center of the inclined black handle in the normalized 0.34 m blaster model.
  static readonly Vector3 HandleCenter=new Vector3(0f,.13f,-.20f);
  void AttachWeapon(){
   if(maleMuzzle && femaleMuzzle){
    player.muzzle=current==female?femaleMuzzle:maleMuzzle;
    weapon=player.muzzle.parent;
    return;
   }
   if(!weapon||!current)return;
   var hand=current.GetBoneTransform(HumanBodyBones.RightHand);if(!hand)return;
   weaponSocket=hand.Find("BlasterSocket");
   if(!weaponSocket){var socket=new GameObject("BlasterSocket");weaponSocket=socket.transform;weaponSocket.SetParent(hand,false);}
   var knuckle=current.GetBoneTransform(HumanBodyBones.RightMiddleProximal);
   var finger=current.GetBoneTransform(HumanBodyBones.RightMiddleIntermediate);
   // A curled middle finger and its knuckle straddle the grip. Cache the
   // contact in hand space so the weapon stays rigid through clip transitions.
   Vector3 palm=knuckle?Vector3.Lerp(hand.position,knuckle.position,.85f):hand.position;
   if(knuckle&&finger)palm=(palm+finger.position)*.5f;
   weaponSocket.localPosition=hand.InverseTransformPoint(palm);
   weaponSocket.localRotation=current==female?femaleGripRotation:maleGripRotation;
   weaponSocket.localScale=Vector3.one;
   weapon.SetParent(weaponSocket,false);weapon.localPosition=Vector3.zero;weapon.localRotation=Quaternion.identity;
   Vector3 scale=weaponSocket.lossyScale;
   weapon.localScale=new Vector3(weaponScale.x/Mathf.Max(.001f,scale.x),weaponScale.y/Mathf.Max(.001f,scale.y),weaponScale.z/Mathf.Max(.001f,scale.z));
   weapon.localPosition=-Vector3.Scale(HandleCenter,weapon.localScale);
  }
 }
}
