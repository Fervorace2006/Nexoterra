using UnityEngine;
namespace Nexoterra.World {
 public static class ImportedSlugVfx {
  public static void Tint(GameObject go,Color color){if(!go)return;foreach(var ps in go.GetComponentsInChildren<ParticleSystem>()){var main=ps.main;main.startColor=color;}}
  public static GameObject BurningCharacter(Transform target)
  {
   var body=target.GetComponent<CharacterController>();
   Vector3 feet=body?body.center-Vector3.up*(body.height*.5f):Vector3.zero;
   return Spawn("Burn",target,feet,1,Quaternion.Euler(-90,0,0));
  }
  public static GameObject ShockCharacter(Transform target)
  {
   var body=target.GetComponent<CharacterController>();
   float height=body?body.height:2f;
   var group=new GameObject("Electroshock");
   group.transform.SetParent(target,false);
   group.transform.localPosition=body?body.center-Vector3.up*(height*.5f):Vector3.zero;
   foreach(float level in new[]{.08f,.42f,.76f})
    Spawn("Shock",group.transform,Vector3.up*(height*level),.78f,Quaternion.Euler(-90,0,0));
   return group;
  }
  public static GameObject GroundFire(Vector3 contact,bool corrupted)
  {
   RaycastHit? floor=null;
   foreach(var hit in Physics.RaycastAll(contact+Vector3.up*.18f,Vector3.down,8,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))
   {
    if(hit.normal.y<.55f||hit.collider.GetComponentInParent<CharacterController>()||hit.collider.GetComponentInParent<SlugProjectile>())continue;
    if(!floor.HasValue||hit.distance<floor.Value.distance)floor=hit;
   }
   if(!floor.HasValue)return null;
   var surface=floor.Value;
   var fire=Spawn("GroundFire",null,surface.point+surface.normal*.012f,.65f,Quaternion.FromToRotation(Vector3.up,surface.normal),4);
   if(!fire)return null;
   if(corrupted)Tint(fire,new Color(.15f,1,.55f));
   return fire;
  }
  public static GameObject Spawn(string name,Transform parent,Vector3 position,float scale,Quaternion rotation,float lifetime=0){
   var prefab=Resources.Load<GameObject>("ImportedSlugVfx/"+name);if(!prefab)return null;
   var go=Object.Instantiate(prefab,parent);go.transform.localPosition=position;go.transform.localRotation=rotation;go.transform.localScale=Vector3.one*scale;
   foreach(var t in go.GetComponentsInChildren<Transform>(true))t.gameObject.layer=2;
   foreach(var c in go.GetComponentsInChildren<Collider>(true))c.enabled=false;
   foreach(var ps in go.GetComponentsInChildren<ParticleSystem>(true)){var main=ps.main;main.scalingMode=ParticleSystemScalingMode.Hierarchy;ps.Play();}
   if(lifetime>0)Object.Destroy(go,lifetime);return go;
  }
 }
}
