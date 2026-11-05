using UnityEngine;
using UnityEngine.InputSystem;
namespace Nexoterra.World
{
 [RequireComponent(typeof(NexoterraThirdPerson))]
 public sealed class PlayerAnimationDriver : MonoBehaviour
 {
  public HumanoidPose male,female;
  public bool IsReloading=>reload>0;
  NexoterraThirdPerson player;CharacterController motor;Transform weapon;
  float recoil,reload,smoothedSpeed;
  void Start(){player=GetComponent<NexoterraThirdPerson>();motor=GetComponent<CharacterController>();weapon=player.muzzle.parent;weapon.localPosition=new Vector3(.18f,1.28f,.23f);if(male)male.Pose(0,false,0,weapon,0,0);if(female)female.Pose(0,false,0,weapon,0,0);}
  public void Shot(){recoil=1;}
  public void Reload(){if(reload<=0){reload=1.1f;if(player)player.Notify("Recargando blaster");}}
  void LateUpdate()
  {
   if(GameSession.Blocked||!player||!weapon)return;recoil=Mathf.MoveTowards(recoil,0,Time.deltaTime*7);reload=Mathf.Max(0,reload-Time.deltaTime);
   bool aim=Mouse.current!=null&&Mouse.current.rightButton.isPressed&&Cursor.lockState==CursorLockMode.Locked;
   smoothedSpeed=Mathf.Lerp(smoothedSpeed,new Vector2(motor.velocity.x,motor.velocity.z).magnitude,1-Mathf.Exp(-10*Time.deltaTime));float speed=smoothedSpeed;bool run=speed>7;
   float pitch=aim?Mathf.DeltaAngle(0,player.view.transform.eulerAngles.x):0;
   weapon.localPosition=Vector3.Lerp(weapon.localPosition,new Vector3(.18f,run?1.14f:1.28f,run?.18f:.23f)+Vector3.back*recoil*.07f,1-Mathf.Exp(-15*Time.deltaTime));
   weapon.localRotation=Quaternion.Euler(pitch-recoil*5+Mathf.Sin(reload/1.1f*Mathf.PI)*18,0,0);
   var rig=player.female.activeSelf?female:male;if(rig)rig.Pose(speed,run,pitch,weapon,recoil,reload/1.1f);
  }
 }
}
