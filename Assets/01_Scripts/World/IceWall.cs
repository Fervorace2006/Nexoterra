using UnityEngine;
namespace Nexoterra.World
{
 // Ice slug fired at the ground raises a crystal wall facing the shooter.
 // It blocks movement and shots, and shatters when any slug hits it.
 public sealed class IceWall : MonoBehaviour
 {
  static IceWall current;
  float grow;Vector3 fullScale;
  // Shared transparent ice materials (assets in Resources so builds keep the transparent shader variant).
  public static Material Clear(bool corrupted)
  {
   var m=Resources.Load<Material>(corrupted?"Nexoterra/Materials/Hielo_Malvado":"Nexoterra/Materials/Hielo_Transparente");
   return m;
  }
  public static void Raise(Vector3 point,Vector3 shotDirection,bool corrupted)
  {
   if(current)current.Shatter();
   shotDirection.y=0;if(shotDirection.sqrMagnitude<.01f)shotDirection=Vector3.forward;
   var go=new GameObject("Muro de hielo");go.transform.position=point;go.transform.rotation=Quaternion.LookRotation(shotDirection.normalized);
   var wall=go.AddComponent<IceWall>();current=wall;wall.Build(corrupted);
  }
  void Build(bool corrupted)
  {
   var mat=Clear(corrupted);
   // Main slab + leaning crystal shards so it reads as natural ice, not a box.
   var shapes=new[]{
    (new Vector3(0,1.25f,0),new Vector3(3.4f,2.5f,.55f),Vector3.zero),
    (new Vector3(-1.5f,1.0f,.15f),new Vector3(.9f,2.2f,.8f),new Vector3(0,20,10)),
    (new Vector3(1.45f,.9f,.1f),new Vector3(1f,1.9f,.8f),new Vector3(0,-15,-12)),
    (new Vector3(-.4f,2.35f,0),new Vector3(.7f,1.1f,.5f),new Vector3(0,0,18)),
    (new Vector3(.6f,2.25f,.05f),new Vector3(.6f,1f,.5f),new Vector3(0,10,-20)),
    (new Vector3(0,.25f,.35f),new Vector3(3.8f,.5f,.5f),Vector3.zero)};
   foreach(var (pos,size,rot) in shapes)
   {
    var s=GameObject.CreatePrimitive(PrimitiveType.Cube);s.name="Cristal";s.transform.SetParent(transform,false);
    s.transform.localPosition=pos;s.transform.localRotation=Quaternion.Euler(rot);s.transform.localScale=size;
    var r=s.GetComponent<Renderer>();if(mat)r.sharedMaterial=mat;r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
    Destroy(s.GetComponent<Collider>());
   }
   var col=gameObject.AddComponent<BoxCollider>();col.center=new Vector3(0,1.3f,0);col.size=new Vector3(3.6f,2.7f,.8f);
   fullScale=Vector3.one;transform.localScale=new Vector3(1,.05f,1);
   NexoAudio.PlayAt("slug_launch",transform.position,.6f);
  }
  void Update(){if(grow<1){grow=Mathf.Min(1,grow+Time.deltaTime/.25f);transform.localScale=new Vector3(1,Mathf.SmoothStep(.05f,1,grow),1);}}
  // Returns true when the collider belonged to a wall (which then shatters).
  public static bool TryHit(Collider c){var w=c?c.GetComponentInParent<IceWall>():null;if(!w)return false;w.Shatter();return true;}
  public void Shatter()
  {
   if(current==this)current=null;
   // Fling the crystals as short-lived debris.
   foreach(var t in System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Cast<Transform>(transform))){var g=t.gameObject;g.layer=2;g.transform.SetParent(null,true);var rb=g.AddComponent<Rigidbody>();g.AddComponent<BoxCollider>();rb.AddExplosionForce(220,transform.position+Vector3.up,5);Destroy(g,1.6f);}
   Destroy(gameObject);
  }
 }
}
