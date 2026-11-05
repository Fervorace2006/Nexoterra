using UnityEngine;
namespace Nexoterra.World
{
 public sealed class CompanionIdle : MonoBehaviour
 {
  public HumanoidPose pose;
  void LateUpdate(){if(pose)pose.Relax();}
 }
}
