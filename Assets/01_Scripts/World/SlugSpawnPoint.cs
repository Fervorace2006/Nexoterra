using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace Nexoterra.World
{
    public sealed class SlugSpawnPoint : MonoBehaviour
    {
        [System.Serializable] public struct HabitatSpecies
        {
            public string id;
            public GameObject prefab;
        }
        public HabitatSpecies[] localSpecies;
        int speciesIndex;
        public GameObject creaturePrefab;
        public string species,biome;
        public int population=1;
        public bool unique;
        public float activeDistance=85,respawnSeconds=75;
        public Transform editorPreview;
        readonly List<GameObject> instances=new List<GameObject>();
        Transform player;
        SlugInventory inventory;
        float nextCheck,nextRespawn;
        void Awake(){if(SlugCatalog.Canonical(species)=="Infierno"){unique=true;population=1;}}
        void Start(){if(localSpecies!=null && localSpecies.Length>0)speciesIndex=Random.Range(0,localSpecies.Length);if(editorPreview)editorPreview.gameObject.SetActive(false);var p=FindFirstObjectByType<NexoterraThirdPerson>();if(p){player=p.transform;inventory=p.GetComponent<SlugInventory>();}}
        void Update()
        {
            if(SlugCatalog.BaseSpecies(species)=="Infierno")return;
            if(!player||!creaturePrefab||Time.time<nextCheck)return;nextCheck=Time.time+1;
            if(unique&&inventory&&inventory.HasSpecies(species))
            {foreach(var g in instances)if(g)Destroy(g);instances.Clear();return;}
            float distance=Vector3.Distance(player.position,transform.position);
            if(distance>activeDistance+25){foreach(var g in instances)if(g)Destroy(g);instances.Clear();return;}
            if(distance>activeDistance)return;
            bool removed=false;
            for(int i=instances.Count-1;i>=0;i--)if(!instances[i]||!instances[i].activeSelf){if(instances[i])Destroy(instances[i]);instances.RemoveAt(i);removed=true;}
            if(removed)nextRespawn=Time.time+respawnSeconds;
            if(Time.time<nextRespawn)return;
            if(unique&&instances.Count==0&&WildSlug.Active.Any(s=>s&&s.isActiveAndEnabled&&!s.adopted&&SlugCatalog.Canonical(s.species)==SlugCatalog.Canonical(species)))return;
            var habitat=Habitat();
            bool elemental=SlugCatalog.BaseSpecies(species).StartsWith("Elemental_");
            while(instances.Count<(unique||elemental?1:population))
            {
                Vector3 p=transform.position+new Vector3(instances.Count*1.3f,0,instances.Count*.5f);
                if(!unique&&!elemental){p.x=Mathf.Clamp(p.x,habitat.xMin,habitat.xMax);p.z=Mathf.Clamp(p.z,habitat.yMin,habitat.yMax);}
                if(Physics.Raycast(p+Vector3.up*2,Vector3.down,out var hit,5,1,QueryTriggerInteraction.Ignore))p.y=hit.point.y;
                string chosen=species;
                var prefab=creaturePrefab;
                if(!unique&&!elemental&&localSpecies!=null&&localSpecies.Length>0)
                {
                    var entry=localSpecies[speciesIndex];
                    speciesIndex=(speciesIndex+1)%localSpecies.Length;
                    chosen=entry.id;
                    prefab=entry.prefab;
                }
                var g=Instantiate(prefab,p,Quaternion.Euler(0,Random.Range(0,360),0),transform);
                if(chosen.StartsWith("Malvada_")&&inventory){var d=inventory.catalog.Find(chosen);if(d!=null){foreach(var renderer in g.GetComponentsInChildren<Renderer>())renderer.enabled=false;var visual=Instantiate(d.normal,g.transform);var rs=visual.GetComponentsInChildren<Renderer>();if(rs.Length>0){var bounds=rs[0].bounds;foreach(var r in rs)bounds.Encapsulate(r.bounds);float scale=.35f/Mathf.Max(.01f,bounds.size.y);var center=bounds.center-g.transform.position;visual.transform.localScale*=scale;visual.transform.localPosition=-center*scale+Vector3.up*.175f;}}}
                var slug=g.GetComponent<WildSlug>();slug.species=SlugCatalog.Canonical(chosen);slug.roamRadius=unique||elemental?.65f:2;slug.SetHabitat(habitat,!unique&&!elemental);instances.Add(g);
            }
        }
        Rect Habitat()
        {
            if(CampaignLevel.Current)return Rect.MinMaxRect(-122,-122,122,122);
            // Same six map regions used to paint the floor and place the lakes.
            bool east=biome=="Jardines del Nexo"||biome=="Galeria acuatica"||biome=="Caldera volcanica";
            bool north=biome=="Tierras antiguas"||biome=="Caldera volcanica";
            bool south=biome=="Cienaga carmesi"||biome=="Jardines del Nexo";
            return Rect.MinMaxRect(east?.5f:-318,north?80.5f:south?-318:-79.5f,east?318:-.5f,north?318:south?-80.5f:79.5f);
        }
        void OnDrawGizmosSelected(){Gizmos.color=Color.cyan;Gizmos.DrawWireSphere(transform.position,4);}
    }
}
