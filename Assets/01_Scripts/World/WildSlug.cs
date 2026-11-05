using System.Collections.Generic;
using UnityEngine;
namespace Nexoterra.World
{
    public sealed class WildSlug : MonoBehaviour
    {
        public static readonly HashSet<WildSlug> Active=new HashSet<WildSlug>();
        public string species;
        public float roamRadius=4,hopHeight=.32f,hopDuration=.55f,hopDistance=.85f;
        public bool adopted;
        Vector3 home,target,hopStart,hopEnd,visualScale;
        float changeAt,pause,hopTime;
        Transform visual;
        Animator animator;
        bool moving,hopping;
        void Awake()
        {
            home=transform.position;target=home;animator=GetComponentInChildren<Animator>();
            if(transform.childCount>0){visual=transform.GetChild(0);visual.localScale*=.7f;visual.localPosition*=.7f;visualScale=visual.localScale;}
            pause=Random.Range(.1f,1.2f);
        }
        void OnEnable(){Active.Add(this);}
        void OnDisable(){Active.Remove(this);}
        void Update(){Advance(Time.deltaTime);}
        public void Advance(float dt)
        {
            if(adopted)return;
            if(!hopping&&Time.time>changeAt)
            {
                Vector2 offset=Random.insideUnitCircle*roamRadius;target=home+new Vector3(offset.x,0,offset.y);
                moving=Random.value>.2f;changeAt=Time.time+Random.Range(3,6);
                if(animator&&animator.runtimeAnimatorController)animator.CrossFade(moving?"Walk":"Idle",.15f);
            }
            if(!hopping)
            {
                pause-=dt;if(!moving||pause>0)return;
                Vector3 delta=target-transform.position;delta.y=0;
                if(delta.magnitude<.2f){moving=false;return;}
                if(!SlugHop.Plan(transform.position,target,hopDistance,hopHeight,0,out hopEnd)){moving=false;changeAt=0;return;}
                // Keep wild creatures out of the lake beds.
                if(hopEnd.y<-.15f){moving=false;return;}
                hopStart=transform.position;hopTime=0;hopping=true;
                var direction=hopEnd-hopStart;direction.y=0;transform.rotation=Quaternion.LookRotation(direction);
            }
            hopTime+=dt;float t=Mathf.Clamp01(hopTime/hopDuration);
            transform.position=SlugHop.Sample(hopStart,hopEnd,t,hopHeight);
            if(visual){float squash=1-.15f*Mathf.Abs(2*t-1);visual.localScale=Vector3.Scale(visualScale,new Vector3(1/squash,squash,1/squash));}
            if(t>=1){hopping=false;pause=Random.Range(.12f,.45f);if(visual)visual.localScale=visualScale;}
        }
        public void Adopt(){adopted=true;Active.Remove(this);gameObject.SetActive(false);}
    }
}
