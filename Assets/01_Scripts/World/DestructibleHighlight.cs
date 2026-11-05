using UnityEngine;
namespace Nexoterra.World
{
 public sealed class DestructibleHighlight : MonoBehaviour
 {
  Renderer[] renderers;WorldDestructible prop;MaterialPropertyBlock block;float next;
  void Awake(){renderers=GetComponentsInChildren<Renderer>();prop=GetComponent<WorldDestructible>();block=new MaterialPropertyBlock();}
  void Update()
  {
   if(Time.time<next)return;next=Time.time+.05f;
   float pulse=prop&&(prop.IsDormant||prop.IsFalling)?0:.15f+.3f*(.5f+.5f*Mathf.Sin(Time.time*2.4f+transform.position.x));
   block.SetColor("_EmissionColor",new Color(1,.78f,.18f)*pulse);
   foreach(var r in renderers)if(r)r.SetPropertyBlock(block);
  }
 }
}
