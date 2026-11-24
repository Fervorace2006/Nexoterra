using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Nexoterra.World
{
    public sealed partial class SlugProjectile
    {
        static readonly HashSet<SlugProjectile> Flying=new HashSet<SlugProjectile>();
        readonly List<SlugProjectile> contacts=new List<SlugProjectile>();
        float stunRemaining;
        bool CanCorrupt=>definition!=null&&SlugCatalog.BaseSpecies(definition.id)=="Goon";
        bool IsDoc=>definition!=null&&SlugCatalog.BaseSpecies(definition.id)=="Gon_Doc";
        public string Species=>definition?.id;
        SlugCatalog Catalog=>owner?owner.catalog:enemyOwner?enemyOwner.catalog:null;
        void OnEnable(){Flying.Add(this);}
        void OnDisable(){Flying.Remove(this);}
        void DemoledoraImpact(Vector3 point)
        {
            if(IsTransformed&&SlugCatalog.BaseSpecies(definition.id)=="Demoledora")
                SlugFlightEffects.Explosion(point,true,1.8f); // Visual only: damage is applied once by ReceiveSlug.
        }
        void CheckProjectileContacts(float distance)
        {
            if(!IsTransformed||arenaShot)return;
            contacts.Clear();
            foreach(var other in Flying)
            {
                if(!other||other==this||!other.IsTransformed||other.IsReturning||other.arenaShot)continue;
                if(!(CanCorrupt||CanPurify||other.CanCorrupt||other.CanPurify))continue;
                Vector3 delta=other.transform.position-transform.position;
                float along=Mathf.Clamp(Vector3.Dot(delta,Direction),0,distance);
                float radius=transformedHitRadius+other.transformedHitRadius;
                if((delta-Direction*along).sqrMagnitude<=radius*radius)contacts.Add(other);
            }
            contacts.Sort((a,b)=>(a.transform.position-transform.position).sqrMagnitude.CompareTo((b.transform.position-transform.position).sqrMagnitude));
            foreach(var other in contacts)
            {
                if(IsReturning)break;
                if(!other||other.IsReturning)continue;
                if((IsDoc&&other.CanCorrupt)||(CanCorrupt&&other.IsDoc))
                {StunAndReturn();other.StunAndReturn();break;}
                if(CanCorrupt)other.ChangeInFlight(true,Catalog);
                else if(CanPurify)other.ChangeInFlight(false,Catalog);
                if(other.CanCorrupt)ChangeInFlight(true,other.Catalog);
                else if(other.CanPurify)ChangeInFlight(false,other.Catalog);
            }
        }
        void StunAndReturn(){BeginReturn();stunRemaining=3;}
        void ChangeInFlight(bool corrupt,SlugCatalog fallback)
        {
            if(IsReturning||!IsTransformed||definition==null||CanCorrupt)return;
            bool evil=definition.id.StartsWith("Malvada_");if(evil==corrupt)return;
            var catalog=Catalog?Catalog:fallback;if(!catalog)return;
            var next=catalog.Find((corrupt?"Malvada_":"")+SlugCatalog.BaseSpecies(definition.id));
            if(next==null)return; // Doc and the energy guardian have no corrupted model.
            BeginReturn();stunRemaining=1.5f;
            if(corrupt)StartCoroutine(ConvertFlight(next));
            else SlugPurification.Begin(gameObject,()=>ReplaceDefinition(next));
        }
        IEnumerator ConvertFlight(SlugDefinition next)
        {
            var fx=new GameObject("Conversión roja de proyectil");fx.transform.SetParent(transform,false);fx.AddComponent<CorruptionConversionVfx>();
            yield return new WaitForSeconds(.7f);ReplaceDefinition(next);
            yield return new WaitForSeconds(.7f);Destroy(fx);
        }
        void ReplaceDefinition(SlugDefinition next)
        {
            string previous=definition.id;
            if(owner)owner.ChangeTravellingSpecies(previous,next.id,this);
            if(enemyOwner&&enemySlot>=0&&enemySlot<enemyOwner.slugSlots.Length)enemyOwner.slugSlots[enemySlot]=next.id;
            definition=next;Show(next.normal,.40f);
        }
        void CorruptArea(Vector3 point)
        {
            if(healedArea||!IsTransformed)return;healedArea=true;
            foreach(var wild in FindObjectsByType<WildSlug>(FindObjectsSortMode.None))
                if(!wild.adopted&&Vector3.Distance(wild.transform.position,point)<=docHealingRadius)CorruptWild(wild);
            foreach(var loose in FindObjectsByType<LooseSlug>(FindObjectsSortMode.None))
                if(Vector3.Distance(loose.transform.position,point)<=docHealingRadius)CorruptLoose(loose);
        }
        void CorruptTarget(Collider collider)
        {
            if(!IsTransformed)return;
            CorruptWild(collider.GetComponentInParent<WildSlug>());
            CorruptLoose(collider.GetComponentInParent<LooseSlug>());
        }
        void CorruptLoose(LooseSlug loose)
        {
            if(!loose||loose.changing||loose.Species.StartsWith("Malvada_"))return;
            loose.Change("Malvada_"+SlugCatalog.BaseSpecies(loose.Species));
        }
        void CorruptWild(WildSlug wild)
        {
            if(!wild||wild.adopted||wild.species.StartsWith("Malvada_")||wild.GetComponent<SlugPurification>()||wild.GetComponent<WildSlugCorruption>())return;
            var catalog=Catalog;if(!catalog)return;
            var next=catalog.Find("Malvada_"+SlugCatalog.BaseSpecies(wild.species));if(next==null)return;
            wild.gameObject.AddComponent<WildSlugCorruption>().Begin(wild,next);
        }
        internal static void ReplaceWildVisual(WildSlug wild,SlugDefinition next){ApplyPurifiedWild(wild,next);}
    }
    public sealed class WildSlugCorruption:MonoBehaviour
    {
        public void Begin(WildSlug wild,SlugDefinition next){StartCoroutine(Change(wild,next));}
        IEnumerator Change(WildSlug wild,SlugDefinition next)
        {
            bool enabledBefore=wild.enabled;wild.enabled=false;
            var fx=new GameObject("Conversión roja de babosa silvestre");fx.transform.SetParent(transform,false);fx.AddComponent<CorruptionConversionVfx>();
            yield return new WaitForSeconds(.7f);
            SlugProjectile.ReplaceWildVisual(wild,next);
            yield return new WaitForSeconds(.7f);
            Destroy(fx);if(wild)wild.enabled=enabledBefore;Destroy(this);
        }
    }
}
