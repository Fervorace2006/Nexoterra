using System;
using System.Collections;
using UnityEngine;

namespace Nexoterra.World
{
    public sealed class SlugPurification : MonoBehaviour
    {
        public static void Begin(GameObject target, Action replace, Action finished=null)
        {
            if(target.GetComponent<SlugPurification>())return;
            target.AddComponent<SlugPurification>().StartCoroutine(Run(target,replace,finished));
        }
        static IEnumerator Run(GameObject target, Action replace, Action finished)
        {
            var component=target.GetComponent<SlugPurification>();
            var wild=target.GetComponent<WildSlug>();
            bool wasEnabled=wild&&wild.enabled;
            if(wild)wild.enabled=false;
            var old=target.GetComponentsInChildren<Renderer>();
            var fx=ImportedSlugVfx.Spawn("Purification",target.transform,Vector3.zero,.6f,Quaternion.identity);
            ImportedSlugVfx.Tint(fx,new Color(.15f,1,.3f));
            // Hide the old model inside the aura until the replacement is ready.
            foreach(var r in old)if(r)r.enabled=false;
            if(fx)foreach(var ps in fx.GetComponentsInChildren<ParticleSystem>())ps.Simulate(.35f,false,true);
            if(fx)foreach(var ps in fx.GetComponentsInChildren<ParticleSystem>())ps.Play();
            yield return new WaitForSeconds(.7f);
            replace();
            yield return new WaitForSeconds(.35f);
            if(fx)Destroy(fx);
            if(wild)wild.enabled=wasEnabled;
            finished?.Invoke();
            Destroy(component);
        }
    }
}
