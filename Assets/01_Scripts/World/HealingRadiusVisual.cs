using UnityEngine;
namespace Nexoterra.World {
 public static class HealingRadiusVisual {
  public static LineRenderer Ring(Transform parent,float radius,Color color){var go=new GameObject("Radio de babosas");go.layer=2;go.transform.SetParent(parent,false);var line=go.AddComponent<LineRenderer>();line.sharedMaterial=SlugFlightEffects.RibbonMaterial;line.useWorldSpace=false;line.loop=true;line.positionCount=80;line.widthMultiplier=.045f;line.startColor=line.endColor=color;Resize(line,radius);return line;}
  public static void Resize(LineRenderer line,float radius){for(int i=0;i<80;i++){float angle=i*Mathf.PI*2/80;line.SetPosition(i,new Vector3(Mathf.Cos(angle)*radius,.08f,Mathf.Sin(angle)*radius));}}
  public static void Burst(Vector3 position,float radius){var ring=Ring(null,radius,new Color(.4f,1,.65f,.9f));ring.transform.position=position;Object.Destroy(ring.gameObject,2);}
 }
}
