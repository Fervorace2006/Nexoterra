using System.Collections.Generic;
using UnityEngine;
namespace Nexoterra.World {
 [RequireComponent(typeof(CharacterController))]
 public sealed class WaterAnimationSensor:MonoBehaviour {
  public NexoterraThirdPerson player; readonly List<Collider> water=new List<Collider>(); public bool Swimming{get;private set;}
  public void Register(Collider c){if(c&&!water.Contains(c))water.Add(c);}
  void Update(){if(!player)return;var point=player.transform.position+Vector3.up*.55f;bool inside=false;for(int i=water.Count-1;i>=0;i--){var c=water[i];if(!c){water.RemoveAt(i);continue;}if(c.bounds.Contains(point)){inside=true;break;}}Swimming=inside;}
 }
}
