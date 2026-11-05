using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace Nexoterra.World
{
    public sealed class SlugSpawnPoint : MonoBehaviour
    {
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
        void Start(){if(editorPreview)editorPreview.gameObject.SetActive(false);var p=FindFirstObjectByType<NexoterraThirdPerson>();if(p){player=p.transform;inventory=p.GetComponent<SlugInventory>();}}
        void Update()
        {
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
            while(instances.Count<(unique?1:population))
            {
                Vector3 p=transform.position+new Vector3(instances.Count*1.3f,0,instances.Count*.5f);
                if(Physics.Raycast(p+Vector3.up*2,Vector3.down,out var hit,5,1,QueryTriggerInteraction.Ignore))p.y=hit.point.y;
                var g=Instantiate(creaturePrefab,p,Quaternion.Euler(0,Random.Range(0,360),0),transform);
                var slug=g.GetComponent<WildSlug>();slug.species=SlugCatalog.Canonical(species);slug.roamRadius=unique?.65f:2;instances.Add(g);
            }
        }
        void OnDrawGizmosSelected(){Gizmos.color=Color.cyan;Gizmos.DrawWireSphere(transform.position,4);}
    }
}
