using System.Collections.Generic;
using UnityEngine;
namespace Nexoterra.World
{
 // Ice slug fired at the ground raises a curling wave of ice facing the shot.
 // Low-poly faceted mesh (built once in code): crescent footprint, leans forward and curls at the crest,
 // snow on the base flare and crest. Size fits the player (1.8 m): 3.6 m tall (2× the player), 2.8 m wide.
 // It blocks movement and shots, and shatters when any slug hits it.
 public sealed class IceWall : MonoBehaviour
 {
  public const float Height=3.6f,Width=2.8f;
  static IceWall current;static Mesh waveMesh;
  float grow;
  // Transparent ice for the freeze prison (assets in Resources so builds keep the transparent shader variant).
  public static Material Clear(bool corrupted)=>Resources.Load<Material>(corrupted?"Nexoterra/Materials/Hielo_Malvado":"Nexoterra/Materials/Hielo_Transparente");
  static Material WallIce(bool corrupted)=>Resources.Load<Material>(corrupted?"Nexoterra/Materials/Hielo_Muro_Malvado":"Nexoterra/Materials/Hielo_Muro");
  static Material Snow()=>Resources.Load<Material>("Nexoterra/Materials/Nieve");
  public static void Raise(Vector3 point,Vector3 shotDirection,bool corrupted)
  {
   if(current)current.Shatter();
   shotDirection.y=0;if(shotDirection.sqrMagnitude<.01f)shotDirection=Vector3.forward;
   var go=new GameObject("Muro de hielo");go.transform.position=point-Vector3.up*.15f;go.transform.rotation=Quaternion.LookRotation(shotDirection.normalized);
   var wall=go.AddComponent<IceWall>();current=wall;wall.Build(corrupted);
  }
  void Build(bool corrupted)
  {
   var model=new GameObject("Ola de hielo");model.transform.SetParent(transform,false);
   model.AddComponent<MeshFilter>().sharedMesh=WaveMesh();
   var r=model.AddComponent<MeshRenderer>();r.sharedMaterials=new[]{WallIce(corrupted),Snow()};
   var col=gameObject.AddComponent<BoxCollider>();col.center=new Vector3(0,Height*.45f,.5f);col.size=new Vector3(Width,Height*.9f,1.4f);
   transform.localScale=new Vector3(1,.05f,1);
   NexoAudio.PlayAt("slug_launch",transform.position,.6f);
  }
  void Update(){if(grow<1){grow=Mathf.Min(1,grow+Time.deltaTime/.28f);float k=Mathf.SmoothStep(0,1,grow);transform.localScale=new Vector3(Mathf.Lerp(.6f,1,k),Mathf.Lerp(.05f,1,k)+Mathf.Sin(k*Mathf.PI)*.08f,1);}}
  // Returns true when the collider belonged to a wall (which then shatters).
  public static bool TryHit(Collider c){var w=c?c.GetComponentInParent<IceWall>():null;if(!w)return false;w.Shatter();return true;}
  public void Shatter()
  {
   if(current==this)current=null;
   var mr=GetComponentInChildren<MeshRenderer>();var mat=mr?mr.sharedMaterial:null;
   for(int i=0;i<14;i++)
   {
    var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name="Esquirla de hielo";g.layer=2;
    float s=Random.Range(.18f,.45f);g.transform.localScale=new Vector3(s,s*Random.Range(1,2.2f),s*.7f);
    g.transform.position=transform.TransformPoint(new Vector3(Random.Range(-Width*.45f,Width*.45f),Random.Range(.3f,Height*.9f),Random.Range(0,.9f)));
    g.transform.rotation=Random.rotation;if(mat)g.GetComponent<Renderer>().sharedMaterial=mat;
    var rb=g.AddComponent<Rigidbody>();rb.AddExplosionForce(260,transform.position+Vector3.up*1.2f-transform.forward*.6f,6);Destroy(g,1.4f);
   }
   Destroy(gameObject);
  }

  // ---- Procedural faceted wave ----
  static float Noise(int i,int j,int k){unchecked{int h=i*73856093^j*19349663^k*83492791;h=(h<<13)^h;return ((h*(h*h*15731+789221)+1376312589)&0x7fffffff)/1073741824f-1f;}}
  static Vector3 Front(int i,int j,int su,int sv)
  {
   float u=-1+2f*i/su,v=(float)j/sv;
   float taper=1-.62f*Mathf.Pow(v,1.25f);
   float x=u*Width*.5f*taper;
   float bulge=.85f*(1-u*u)*(1-.4f*v);                         // crescent: middle forward, wings back
   float lean=.9f*Mathf.Pow(v,2.4f);               // leans toward the enemy
   float crest=Mathf.Max(0,(v-.72f)/.28f);
   float y=Height*v-1.0f*crest*crest;                // tip curls over and down
   float z=bulge+lean+.75f*crest*(2-crest);
   if(j>0&&j<sv)return new Vector3(x+Noise(i,j,1)*.07f,y+Noise(i,j,2)*.06f,z+Noise(i,j,3)*.07f);
   return new Vector3(x,y,z);
  }
  public static Mesh WaveMesh()
  {
   if(waveMesh)return waveMesh;
   const int su=8,sv=12;
   var front=new Vector3[su+1,sv+1];var back=new Vector3[su+1,sv+1];
   for(int i=0;i<=su;i++)for(int j=0;j<=sv;j++){var p=Front(i,j,su,sv);float v=(float)j/sv,u=-1+2f*i/su;float th=(.75f*(1-v)+.16f)*(1-.35f*u*u);front[i,j]=p;back[i,j]=p-new Vector3(0,v>.72f?.18f*(v-.72f)/.28f:0,th);}
   var verts=new List<Vector3>();var ice=new List<int>();var snow=new List<int>();
   // Both windings, unwelded: faceted look and visible from either side.
   void Tri(Vector3 a,Vector3 b,Vector3 c,bool white){var l=white?snow:ice;int n=verts.Count;verts.Add(a);verts.Add(b);verts.Add(c);l.Add(n);l.Add(n+1);l.Add(n+2);n=verts.Count;verts.Add(a);verts.Add(c);verts.Add(b);l.Add(n);l.Add(n+1);l.Add(n+2);}
   void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,bool white){Tri(a,b,c,white);Tri(a,c,d,white);}
   for(int i=0;i<su;i++)for(int j=0;j<sv;j++)
   {
    bool crest=j>=sv-1;
    Quad(front[i,j],front[i,j+1],front[i+1,j+1],front[i+1,j],crest);
    Quad(back[i+1,j],back[i+1,j+1],back[i,j+1],back[i,j],crest);
   }
   for(int j=0;j<sv;j++){bool crest=j>=sv-1;
    Quad(back[0,j],back[0,j+1],front[0,j+1],front[0,j],crest);
    Quad(front[su,j],front[su,j+1],back[su,j+1],back[su,j],crest);}
   for(int i=0;i<su;i++)Quad(front[i,sv],back[i,sv],back[i+1,sv],front[i+1,sv],true);
   // Snowy base flare, front and back.
   for(int i=0;i<su;i++)
   {
    float out1=.55f+.35f*Mathf.Abs(Noise(i,0,7)),out2=.55f+.35f*Mathf.Abs(Noise(i+1,0,7));
    var a=front[i,0]+Vector3.up*.38f;var b=front[i+1,0]+Vector3.up*.38f;
    var fa=new Vector3(front[i,0].x*1.12f,.02f,front[i,0].z+out1);var fb=new Vector3(front[i+1,0].x*1.12f,.02f,front[i+1,0].z+out2);
    Quad(fa,a,b,fb,true);
    var c=back[i,0]+Vector3.up*.3f;var d=back[i+1,0]+Vector3.up*.3f;
    var ba=new Vector3(back[i,0].x*1.1f,.02f,back[i,0].z-out1*.7f);var bb=new Vector3(back[i+1,0].x*1.1f,.02f,back[i+1,0].z-out2*.7f);
    Quad(bb,d,c,ba,true);
   }
   // Long snow spikes at the wing tips.
   foreach(int side in new[]{-1,1}){int i=side<0?0:su;var root=front[i,0];var tip=new Vector3(root.x+side*1.1f,.03f,root.z+.25f);
    Tri(root+Vector3.up*.5f,tip,back[i,0]+Vector3.up*.2f,true);Tri(back[i,0]+Vector3.up*.2f,tip,root+Vector3.up*.5f,true);}
   waveMesh=new Mesh{name="Ola de hielo"};waveMesh.SetVertices(verts);waveMesh.subMeshCount=2;waveMesh.SetTriangles(ice,0);waveMesh.SetTriangles(snow,1);
   waveMesh.RecalculateNormals();waveMesh.RecalculateBounds();
   return waveMesh;
  }
 }
}
