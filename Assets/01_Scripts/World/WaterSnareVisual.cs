using UnityEngine;
using UnityEngine.Rendering;
namespace Nexoterra.World
{
 public sealed class WaterSnareVisual : MonoBehaviour
 {
  static Material material;LineRenderer[] ripples;Mesh disc;float age;
  void Awake()
  {
   if(!material){material=new Material(Shader.Find("Universal Render Pipeline/Unlit"));material.SetColor("_BaseColor",new Color(.08f,.55f,.9f,.32f));material.SetFloat("_Surface",1);material.SetFloat("_Blend",0);material.SetFloat("_SrcBlend",5);material.SetFloat("_DstBlend",10);material.SetFloat("_ZWrite",0);material.SetFloat("_Cull",0);material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");material.renderQueue=3000;}
   var fill=new GameObject("Agua acumulada");fill.transform.SetParent(transform,false);disc=new Mesh();var vertices=new Vector3[49];var triangles=new int[48*3];for(int i=0;i<48;i++){float a=i*Mathf.PI*2/48;vertices[i+1]=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*1.1f;triangles[i*3]=0;triangles[i*3+1]=(i+1)%48+1;triangles[i*3+2]=i+1;}disc.vertices=vertices;disc.triangles=triangles;disc.RecalculateNormals();fill.AddComponent<MeshFilter>().sharedMesh=disc;fill.AddComponent<MeshRenderer>().sharedMaterial=material;
   ripples=new LineRenderer[3];for(int j=0;j<3;j++){var line=new GameObject("Onda").AddComponent<LineRenderer>();line.transform.SetParent(transform,false);line.useWorldSpace=false;line.loop=true;line.positionCount=48;line.widthMultiplier=.035f;line.sharedMaterial=SlugFlightEffects.RibbonMaterial;line.startColor=line.endColor=new Color(.25f,.85f,1,.65f);line.shadowCastingMode=ShadowCastingMode.Off;ripples[j]=line;}
  }
  void LateUpdate()
  {
   age+=Time.deltaTime;var actor=transform.parent;Vector3 floor=actor.position;
   float best=float.PositiveInfinity;foreach(var hit in Physics.RaycastAll(actor.position+Vector3.up*.6f,Vector3.down,2,1,QueryTriggerInteraction.Ignore))if(!hit.transform.IsChildOf(actor)&&hit.normal.y>.5f&&hit.distance<best){best=hit.distance;floor=hit.point;}
   transform.position=floor+Vector3.up*.055f;transform.rotation=Quaternion.identity;
   for(int j=0;j<3;j++){float t=Mathf.Repeat(age*.55f+j/3f,1),radius=.35f+t*.95f;var color=new Color(.25f,.85f,1,(1-t)*.7f);ripples[j].startColor=ripples[j].endColor=color;for(int i=0;i<48;i++){float a=i*Mathf.PI*2/48;ripples[j].SetPosition(i,new Vector3(Mathf.Cos(a)*radius,.02f+Mathf.Sin(a*4+age*3)*.012f,Mathf.Sin(a)*radius));}}
  }
  void OnDestroy(){if(disc)Destroy(disc);}
 }
}
