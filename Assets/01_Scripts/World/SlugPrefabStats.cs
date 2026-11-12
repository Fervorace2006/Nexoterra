using UnityEngine;
namespace Nexoterra.World
{
 // Shared by the catalog and every projectile: edit the normal slug prefab.
 public sealed class SlugPrefabStats:MonoBehaviour
 {
  public SlugCombatProfile combat=new SlugCombatProfile();
  [Min(.01f)] public float hopHeight=.32f,hopDuration=.55f,hopDistance=.85f;
 }
}
