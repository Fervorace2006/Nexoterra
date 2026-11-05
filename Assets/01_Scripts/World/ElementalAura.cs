using UnityEngine;
using UnityEngine.Rendering;
namespace Nexoterra.World
{
 public sealed class ElementalAura : MonoBehaviour
 {
  Transform[] rings=new Transform[3];
  float elapsed;
  public static void Create(Transform parent,string id,float radius,Vector3 center)
  {
   if(!id.StartsWith("Elemental_"))return;
   Color color=id.Contains("Fuego")?new Color(1,.18f,.035f):id.Contains("Agua")?new Color(.1f,.65f,1):id.Contains("Energia")?new Color(.35f,1,.65f):id.Contains("Tierra")?new Color(.65f,.59f,.49f):new Color(.7f,.82f,.9f);
   var go=new GameObject("Anillos elementales");go.layer=2;go.transform.SetParent(parent,false);go.transform.localPosition=center;var aura=go.AddComponent<ElementalAura>();
   for(int j=0;j<3;j++){var line=new GameObject("Órbita "+j).AddComponent<LineRenderer>();line.gameObject.layer=2;line.transform.SetParent(go.transform,false);aura.rings[j]=line.transform;line.useWorldSpace=false;line.loop=true;line.positionCount=56;line.widthMultiplier=radius*.045f;line.sharedMaterial=SlugFlightEffects.RibbonMaterial;line.startColor=line.endColor=Color.Lerp(color,Color.white,j*.15f)*new Color(1,1,1,.65f);line.shadowCastingMode=ShadowCastingMode.Off;line.receiveShadows=false;for(int i=0;i<56;i++){float angle=i*Mathf.PI*2/56;line.SetPosition(i,new Vector3(Mathf.Cos(angle)*radius,0,Mathf.Sin(angle)*radius));}line.transform.localRotation=Quaternion.Euler(35+j*50,j*60,20+j*25);}
  }
  void Update(){elapsed+=Time.deltaTime;if(elapsed>=2){Destroy(gameObject);return;}transform.Rotate(Vector3.forward*(90*Time.deltaTime),Space.Self);for(int i=0;i<rings.Length;i++)if(rings[i])rings[i].Rotate(new Vector3(12+i*5,65+i*12,7-i*8)*Time.deltaTime,Space.Self);}
 }
}
