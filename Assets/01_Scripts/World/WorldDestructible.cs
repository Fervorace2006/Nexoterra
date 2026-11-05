using UnityEngine;
namespace Nexoterra.World
{
 public sealed class WorldDestructible : MonoBehaviour
 {
  public enum Kind { CeilingRock, RockBarrier, Barrel }
  public Kind kind;
  public float respawnSeconds=300,blastRadius=2.8f,damage=35,rockLength=3;
  public bool IsDormant {get;private set;}
  public bool IsFalling {get;private set;}
  public float RespawnRemaining=>IsDormant?Mathf.Max(0,readyAt-Time.time):0;
  Vector3 origin;Quaternion rotation;Collider[] colliders;Renderer[] renderers;float readyAt,fallSpeed;
  void Awake(){origin=transform.position;rotation=transform.rotation;colliders=GetComponentsInChildren<Collider>();renderers=GetComponentsInChildren<Renderer>();}
  public bool Hit(string species,bool transformed)
  {
   if(!transformed||IsDormant||IsFalling)return false;
   string id=SlugCatalog.Canonical(species);
   if(kind!=Kind.Barrel&&id!="Carnero"&&id!="Armashelt"&&id!="Elemental_Tierra")return false;
   if(kind==Kind.CeilingRock){IsFalling=true;fallSpeed=0;foreach(var c in colliders)c.enabled=false;}
   else Detonate();return true;
  }
  void Update(){Advance(Time.deltaTime);}
  public void Advance(float dt)
  {
   if(IsDormant){if(Time.time>=readyAt)TryRespawn();return;}
   if(!IsFalling)return;
   fallSpeed+=24*Mathf.Max(0,dt);float distance=fallSpeed*dt;
   Vector3 tip=transform.position-Vector3.up*rockLength;
   if(Physics.SphereCast(tip,.35f,Vector3.down,out var hit,distance+.05f,~0,QueryTriggerInteraction.Ignore))
   {transform.position+=Vector3.down*Mathf.Max(0,hit.distance-.05f);Detonate(hit.point);}
   else transform.position+=Vector3.down*distance;
   if(transform.position.y < -20)Detonate();
  }
  public void Detonate(){Detonate(transform.position);}
  void Detonate(Vector3 point)
  {
   if(IsDormant)return;IsDormant=true;IsFalling=false;readyAt=Time.time+respawnSeconds;
   foreach(var c in colliders)c.enabled=false;foreach(var r in renderers)r.enabled=false;
   SlugFlightEffects.Explosion(point,kind==Kind.Barrel,blastRadius);
   AreaDamage.Apply(point,blastRadius,damage);
  }
  void TryRespawn()
  {
   // Never recreate solid geometry around an actor.
   foreach(var c in Physics.OverlapSphere(origin+(kind==Kind.CeilingRock?Vector3.down*rockLength*.5f:Vector3.up),kind==Kind.CeilingRock?rockLength*.5f:1.5f,~0,QueryTriggerInteraction.Ignore))
    if(c.GetComponentInParent<IDamageReceiver>()!=null){readyAt=Time.time+1;return;}
   transform.SetPositionAndRotation(origin,rotation);IsDormant=false;fallSpeed=0;
   foreach(var c in colliders)c.enabled=true;foreach(var r in renderers)r.enabled=true;
  }
 }
}
