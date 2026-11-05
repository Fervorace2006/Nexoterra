using UnityEngine;
namespace Nexoterra.World
{
 // Anchor the broad rim to the floor; turning to shoot must not spin the net.
 public sealed class WebSnareAnchor : MonoBehaviour
 {
  Transform victim;
  void Awake(){victim=transform.parent;}
  void LateUpdate()
  {
   if(!victim)return;
   Vector3 floor=victim.position;
   float nearest=float.PositiveInfinity;
   foreach(var h in Physics.RaycastAll(floor+Vector3.up*.65f,Vector3.down,2.5f,1,QueryTriggerInteraction.Ignore))
    if(!h.transform.IsChildOf(victim)&&h.normal.y>.5f&&h.distance<nearest){nearest=h.distance;floor.y=h.point.y;}
   transform.position=floor+Vector3.up*.035f;transform.rotation=Quaternion.identity;
  }
 }
}
