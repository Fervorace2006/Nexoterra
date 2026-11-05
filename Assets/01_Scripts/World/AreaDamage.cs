using System.Collections.Generic;
using UnityEngine;
namespace Nexoterra.World
{
 public interface IDamageReceiver { void ReceiveDamage(float amount); }
 public static class AreaDamage
 {
  public static void Apply(Vector3 center,float radius,float damage)
  {
   var seen=new HashSet<IDamageReceiver>();var barrels=new HashSet<WorldDestructible>();
   foreach(var c in Physics.OverlapSphere(center,radius,~0,QueryTriggerInteraction.Ignore))
   {
    var receiver=c.GetComponentInParent<IDamageReceiver>();
    if(receiver!=null&&seen.Add(receiver)){float t=Vector3.Distance(center,c.ClosestPoint(center))/radius;receiver.ReceiveDamage(damage*Mathf.Lerp(1,.4f,Mathf.Clamp01(t)));}
    var prop=c.GetComponentInParent<WorldDestructible>();if(prop&&prop.kind==WorldDestructible.Kind.Barrel)barrels.Add(prop);
   }
   foreach(var barrel in barrels)barrel.Detonate();
  }
 }
}
