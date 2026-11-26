using UnityEngine;
using System.Collections.Generic;
namespace Nexoterra.World
{
 // Perfect-dodge shield: a sphere of translucent cyan tech panels (geodesic, small gaps between panels)
 // around a character for 5 s. While it is up the character ignores damage and slug effects.
 // Used by the player (dodging an incoming enemy slug at the last moment) and by Sir Black 1/2.
 public sealed class TechShield : MonoBehaviour
 {
  public const float Seconds=5f;
  static readonly Dictionary<Transform,TechShield> active=new Dictionary<Transform,TechShield>();
  static Mesh panels;
  float remaining,radius=1.25f;Transform owner;Renderer rend;Material mat;Color baseColor;
  public static bool Protects(Transform t)=>t&&active.TryGetValue(t,out var s)&&s&&s.remaining>0;
  public static void Activate(Transform target,float radius=1.25f,float height=1.0f)
  {
   if(!target)return;
   if(active.TryGetValue(target,out var s)&&s){s.remaining=Seconds;return;}
   var go=new GameObject("Escudo tecnológico");go.transform.SetParent(target,false);go.transform.localPosition=Vector3.up*height;
   go.transform.localScale=Vector3.one*radius;
   s=go.AddComponent<TechShield>();s.radius=radius;s.owner=target;s.remaining=Seconds;active[target]=s;
   go.AddComponent<MeshFilter>().sharedMesh=Panels();
   s.rend=go.AddComponent<MeshRenderer>();s.rend.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
   var src=Resources.Load<Material>("Nexoterra/Materials/Escudo_Tecnologico");
   if(src){s.mat=new Material(src);s.baseColor=s.mat.color;s.rend.sharedMaterial=s.mat;}
   NexoAudio.Play("ui_click",.9f);
  }
  void Update()
  {
   remaining-=Time.deltaTime;
   transform.Rotate(0,40*Time.deltaTime,0,Space.World);
   float appear=Mathf.Clamp01((Seconds-remaining)/.18f);
   float fade=remaining<1?(Mathf.Repeat(remaining*8,1)<.5f?.35f:1f):1f;   // blinks during the last second
   transform.localScale=Vector3.one*radius*Mathf.Lerp(.6f,1,appear)*(1+.02f*Mathf.Sin(Time.time*6));
   // Fade further when the camera is close to or inside the sphere, so it never blocks the view.
   float near=1;var cam=Camera.main;
   if(cam){float d=Vector3.Distance(cam.transform.position,transform.position)/Mathf.Max(.1f,radius);near=Mathf.Lerp(.3f,1,Mathf.InverseLerp(1.2f,3.5f,d));}
   if(mat){var c=baseColor;c.a*=appear*fade*near;mat.color=c;}
   if(remaining<=0){if(owner)active.Remove(owner);Destroy(gameObject);}
  }
  void OnDestroy(){if(mat)Destroy(mat);if(owner&&active.TryGetValue(owner,out var s)&&s==this)active.Remove(owner);}

  // Subdivided icosahedron; every triangle is its own panel, shrunk toward its centre to leave seams.
  public static Mesh Panels()
  {
   if(panels)return panels;
   float t=(1+Mathf.Sqrt(5))/2;
   var v=new List<Vector3>{new Vector3(-1,t,0),new Vector3(1,t,0),new Vector3(-1,-t,0),new Vector3(1,-t,0),new Vector3(0,-1,t),new Vector3(0,1,t),new Vector3(0,-1,-t),new Vector3(0,1,-t),new Vector3(t,0,-1),new Vector3(t,0,1),new Vector3(-t,0,-1),new Vector3(-t,0,1)};
   for(int i=0;i<v.Count;i++)v[i]=v[i].normalized;
   var f=new List<int[]>{new[]{0,11,5},new[]{0,5,1},new[]{0,1,7},new[]{0,7,10},new[]{0,10,11},new[]{1,5,9},new[]{5,11,4},new[]{11,10,2},new[]{10,7,6},new[]{7,1,8},new[]{3,9,4},new[]{3,4,2},new[]{3,2,6},new[]{3,6,8},new[]{3,8,9},new[]{4,9,5},new[]{2,4,11},new[]{6,2,10},new[]{8,6,7},new[]{9,8,1}};
   for(int level=0;level<2;level++){var next=new List<int[]>();var cache=new Dictionary<long,int>();
    int Mid(int a,int b){long key=a<b?((long)a<<32)+b:((long)b<<32)+a;if(cache.TryGetValue(key,out int m))return m;v.Add(((v[a]+v[b])*.5f).normalized);cache[key]=v.Count-1;return v.Count-1;}
    foreach(var tri in f){int a=Mid(tri[0],tri[1]),b=Mid(tri[1],tri[2]),c=Mid(tri[2],tri[0]);next.Add(new[]{tri[0],a,c});next.Add(new[]{tri[1],b,a});next.Add(new[]{tri[2],c,b});next.Add(new[]{a,b,c});}
    f=next;}
   var verts=new List<Vector3>();var tris=new List<int>();
   foreach(var tri in f){var c=(v[tri[0]]+v[tri[1]]+v[tri[2]])/3;int n=verts.Count;for(int k=0;k<3;k++)verts.Add(Vector3.Lerp(c,v[tri[k]],.86f));
    tris.AddRange(new[]{n,n+1,n+2});}                 // the material renders both sides (cull off)
   panels=new Mesh{name="Paneles del escudo"};panels.SetVertices(verts);panels.SetTriangles(tris,0);panels.RecalculateNormals();panels.RecalculateBounds();
   return panels;
  }
 }
}
