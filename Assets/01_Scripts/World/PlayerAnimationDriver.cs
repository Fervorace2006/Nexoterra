using UnityEngine;

namespace Nexoterra.World
{
 [RequireComponent(typeof(NexoterraThirdPerson))]
 public sealed class PlayerAnimationDriver : MonoBehaviour
 {
  // Kept as serialized compatibility fields for existing editor setup tools.
  public HumanoidPose male,female;
  public float reloadDuration=1.1f;
  public bool IsReloading=>native&&native.IsReloading;
  NativePlayerAnimation native;NexoterraThirdPerson player;
  void Awake(){player=GetComponent<NexoterraThirdPerson>();native=GetComponent<NativePlayerAnimation>();}
  public void Shot(){if(native)native.PlayShot();}
  public void Reload(){if(IsReloading)return;if(native)native.BeginReload(reloadDuration);if(player)player.Notify("Recargando blaster");}
 }
}
