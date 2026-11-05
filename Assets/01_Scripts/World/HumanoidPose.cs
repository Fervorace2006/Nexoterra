using UnityEngine;
namespace Nexoterra.World
{
 public sealed class HumanoidPose : MonoBehaviour
 {
  public Transform hips,spine,chest,head,leftArm,leftForearm,leftHand,rightArm,rightForearm,rightHand,leftThigh,leftShin,rightThigh,rightShin;
  public float height=1.9f;
  public bool Ready=>hips&&rightHand;
  Vector3 restHips;bool initialized;float phase,runBlend;
  public void Pose(float speed,bool running,float pitch,Transform weapon,float recoil,float reload)
  {
   if(!Ready)return;
   if(!initialized){restHips=hips.localPosition;initialized=true;}
   float amount=Mathf.Clamp01(speed/(running?9:4));
   runBlend=Mathf.Lerp(runBlend,running?1:0,1-Mathf.Exp(-10*Time.deltaTime));
   phase+=Time.deltaTime*Mathf.Lerp(9,13,runBlend)*Mathf.Clamp01(speed);float gait=Mathf.Sin(phase);
   hips.localPosition=restHips+Vector3.up*(Mathf.Cos(phase*2)*.006f*height*amount);
   hips.localRotation=Quaternion.Euler(0,gait*2*amount,0);
   leftThigh.localRotation=Quaternion.Euler(gait*Mathf.Lerp(22,32,runBlend)*amount,0,0);rightThigh.localRotation=Quaternion.Euler(-gait*Mathf.Lerp(22,32,runBlend)*amount,0,0);
   leftShin.localRotation=Quaternion.Euler(Mathf.Max(0,-gait)*40*amount,0,0);rightShin.localRotation=Quaternion.Euler(Mathf.Max(0,gait)*40*amount,0,0);
   spine.localRotation=Quaternion.Euler(Mathf.Lerp(2,7,runBlend)*amount+recoil*3,0,gait*1.5f*amount);chest.localRotation=Quaternion.Euler(pitch*.3f,0,0);head.localRotation=Quaternion.Euler(pitch*.4f,0,0);
   Vector3 rightTarget=weapon?weapon.TransformPoint(new Vector3(0,.085f,-.08f)):transform.TransformPoint(new Vector3(.12f,.68f,.24f)*height);
   Vector3 leftTarget=weapon?weapon.TransformPoint(new Vector3(-.035f,.14f,.13f)):transform.TransformPoint(new Vector3(-.08f,.68f,.35f)*height);
   if(reload>0)leftTarget+=transform.TransformDirection(new Vector3(-.12f,-.17f,.08f))*Mathf.Sin(reload*Mathf.PI);
   Arm(rightArm,rightForearm,rightHand,rightTarget,1);Arm(leftArm,leftForearm,leftHand,leftTarget,-1);
   if(weapon){Wrist(rightHand,weapon,1);Wrist(leftHand,weapon,-1);}
  }
  public void Relax()
  {
   if(!Ready)return;
   spine.localRotation=Quaternion.Euler(Mathf.Sin(Time.time*1.5f)*.4f,0,0);
   Arm(rightArm,rightForearm,rightHand,transform.TransformPoint(new Vector3(.19f,.49f,.035f)*height),1);
   Arm(leftArm,leftForearm,leftHand,transform.TransformPoint(new Vector3(-.19f,.49f,.035f)*height),-1);
  }
  void Wrist(Transform hand,Transform weapon,float sign)
  {
   // Align the palm with the barrel instead of extending the wrist along the bent forearm.
   var rotation=Quaternion.FromToRotation(transform.TransformDirection(Vector3.right*sign),weapon.forward)*transform.rotation;
   rotation=Quaternion.AngleAxis(sign>0?65:145,weapon.forward)*rotation;
   hand.rotation=Quaternion.Slerp(hand.rotation,rotation,.65f);
  }
  void Arm(Transform upper,Transform lower,Transform hand,Vector3 target,float sign)
  {
   float a=Vector3.Distance(upper.position,lower.position),b=Vector3.Distance(lower.position,hand.position);Vector3 delta=target-upper.position;
   float distance=Mathf.Clamp(delta.magnitude,.02f,(a+b)*.98f);Vector3 forward=delta.normalized;target=upper.position+forward*distance;
   Vector3 hint=transform.TransformDirection(new Vector3(sign*.45f,-1,-.2f));Vector3 bend=Vector3.ProjectOnPlane(hint,forward).normalized;
   float along=(a*a-b*b+distance*distance)/(2*distance);float away=Mathf.Sqrt(Mathf.Max(0,a*a-along*along));Vector3 elbow=upper.position+forward*along+bend*away;
   Vector3 axis=transform.TransformDirection(Vector3.right*sign);
   upper.rotation=Quaternion.FromToRotation(axis,elbow-upper.position)*transform.rotation;
   lower.rotation=Quaternion.FromToRotation(axis,target-lower.position)*transform.rotation;hand.rotation=lower.rotation;
  }
 }
}
