using UnityEngine;
namespace Nexoterra.World
{
 public sealed class EnemySlugLoot:MonoBehaviour
 {
  public string key;
  public static WildSlug Spawn(SlugDefinition definition,Vector3 position,string key){
   float ground=float.NegativeInfinity;
   foreach(var hit in Physics.RaycastAll(position+Vector3.up*2,Vector3.down,8,SlugHop.WorldMask,QueryTriggerInteraction.Ignore))
    if(hit.normal.y>.5f&&!hit.collider.GetComponentInParent<EnemyCombatant>()&&!hit.collider.GetComponentInParent<NexoterraThirdPerson>())ground=Mathf.Max(ground,hit.point.y);
   if(float.IsFinite(ground))position.y=ground+.05f;
   var go=new GameObject("Babosa liberada · "+definition.id);go.transform.position=position;go.AddComponent<EnemySlugLoot>().key=key;
   var visual=Instantiate(definition.normal,go.transform);foreach(var c in visual.GetComponentsInChildren<Collider>())Destroy(c);
   var rs=visual.GetComponentsInChildren<Renderer>();if(rs.Length>0){var b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);float scale=.4f/Mathf.Max(.01f,b.size.y);Vector3 offset=b.center-go.transform.position;visual.transform.localScale*=scale;visual.transform.localPosition=-offset*scale+Vector3.up*.2f;}
   var collider=go.AddComponent<SphereCollider>();collider.center=Vector3.up*.2f;collider.radius=.35f;collider.isTrigger=true;
   var slug=go.AddComponent<WildSlug>();slug.species=definition.id;slug.roamRadius=18;
   slug.SetHabitat(CampaignLevel.Current?new Rect(-122,-122,244,244):new Rect(position.x-30,position.z-30,60,60),true);return slug;
  }
 }
}
