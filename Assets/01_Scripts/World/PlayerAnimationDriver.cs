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
  public void Reload(){if(IsReloading)return;if(native&&native.IsLoaded){if(player)player.Notify("Esta babosa ya está cargada");return;}var inventory=GetComponent<SlugInventory>();var slug=inventory?inventory.SelectedDefinition():null;if(slug==null){if(player)player.Notify("Selecciona una babosa para recargar");return;}if(inventory.IsBusy(slug.id)){if(player)player.Notify("Espera a que esta babosa regrese para recargar");return;}if(native)native.BeginReload(reloadDuration);if(player)player.Notify("Recargando blaster");}
 }
}
