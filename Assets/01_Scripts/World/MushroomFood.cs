using System.Collections.Generic;
using UnityEngine;
namespace Nexoterra.World
{
    public sealed class MushroomFood : MonoBehaviour
    {
        public static readonly HashSet<MushroomFood> Available=new HashSet<MushroomFood>();
        public float regrowSeconds=90;
        Renderer[] renderers;
        float regrowAt;
        bool collected;
        void Awake(){renderers=GetComponentsInChildren<Renderer>();}
        void OnEnable(){if(!collected)Available.Add(this);}
        void OnDisable(){Available.Remove(this);}
        public void Collect(){collected=true;Available.Remove(this);regrowAt=Time.time+regrowSeconds;foreach(var r in renderers)r.enabled=false;}
        void Update(){if(collected&&Time.time>=regrowAt){collected=false;Available.Add(this);foreach(var r in renderers)r.enabled=true;}}
    }
}
