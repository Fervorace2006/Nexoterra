using UnityEngine;
using UnityEngine.Rendering;

namespace Nexoterra.World
{
    // Visuals only: flight, collision, inventory and return timing remain authoritative.
    public sealed class SlugFlightEffects : MonoBehaviour
    {
        public enum Style { Fire, Water, Ice, Lightning, Wind, Earth, Web, Spore, Impact }
        public Style Kind { get; private set; }
        public bool Finished { get; private set; }
        Color tint;
        bool corrupted;
        ParticleSystem particles;
        TrailRenderer trail;
        LineRenderer[] arcs;
        Light glow;
        float age, nextArc;
        bool bodyLightning;
        float bodyHeight=2,bodyRadius=.5f;
        static Material particleMaterial, lineMaterial;
        public static Material RibbonMaterial=>Material(true);
        static Material Material(bool line)
        {
            var material=line?lineMaterial:particleMaterial;
            if(material)return material;
            material=Resources.Load<Material>(line?"SlugVfxTrail":"SlugVfxParticle");
            if(!material){material=new Material(Shader.Find("Nexoterra/Slug Glow"));material.SetFloat("_Ribbon",line?1:0);}
            if(line)lineMaterial=material;else particleMaterial=material;
            return material;
        }
        public static Style ForSpecies(string id)
        {
            switch(SlugCatalog.Canonical(id.Replace("Malvada_","")))
            {
                case "Infierno":case "Elemental_Fuego":return Style.Fire;
                case "Elemental_Agua":return Style.Water;
                case "ice":return Style.Ice;
                case "tazerling":case "Elemental_Energia":return Style.Lightning;
                case "Elemental_Aire":case "Tornado":return Style.Wind;
                case "Elemental_Tierra":case "Armashelt":return Style.Earth;
                case "Arana":return Style.Web;
                case "Gon_Doc":return Style.Spore;
                default:return Style.Impact;
            }
        }
        public static SlugFlightEffects Create(Transform projectile,string species)
        {
            var go=new GameObject("Efecto transformado Ã‚Â· "+species);go.layer=2;go.transform.SetParent(projectile,false);
            var effect=go.AddComponent<SlugFlightEffects>();effect.Kind=ForSpecies(species);effect.corrupted=species.StartsWith("Malvada_");effect.Configure();go.transform.localPosition=Vector3.back*.65f;return effect;
        }
        static Color ColorFor(Style style)
        {
            switch(style)
            {
                case Style.Fire:return new Color(1,.27f,.035f);
                case Style.Water:return new Color(.08f,.65f,1);
                case Style.Ice:return new Color(.5f,.88f,1);
                case Style.Lightning:return new Color(.35f,.75f,1);
                case Style.Wind:return new Color(.65f,1,.85f);
                case Style.Earth:return new Color(1,.65f,.22f);
                case Style.Web:return new Color(.65f,.18f,1);
                case Style.Spore:return new Color(.3f,1,.15f);
                default:return new Color(1,.12f,.18f);
            }
        }
        public static SlugFlightEffects Electrify(Transform target)
        {
            var go=new GameObject("Electroshock · chispas corporales");go.layer=2;go.transform.SetParent(target,false);
            var effect=go.AddComponent<SlugFlightEffects>();effect.Kind=Style.Lightning;effect.bodyLightning=true;
            var motor=target.GetComponent<CharacterController>();if(motor){effect.bodyHeight=motor.height;effect.bodyRadius=Mathf.Max(.4f,motor.radius*1.3f);go.transform.localPosition=motor.center-Vector3.up*motor.height*.5f;}
            effect.Configure();effect.trail.enabled=false;effect.glow.intensity=.9f;effect.glow.range=2.5f;
            effect.particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);effect.particles.transform.localPosition=Vector3.up*effect.bodyHeight*.5f;
            var main=effect.particles.main;main.simulationSpace=ParticleSystemSimulationSpace.Local;main.startSize=new ParticleSystem.MinMaxCurve(.025f,.085f);main.startSpeed=new ParticleSystem.MinMaxCurve(.1f,.5f);
            var shape=effect.particles.shape;shape.shapeType=ParticleSystemShapeType.Box;shape.scale=new Vector3(effect.bodyRadius*2,effect.bodyHeight,effect.bodyRadius*2);
            var velocity=effect.particles.velocityOverLifetime;velocity.enabled=false;var emission=effect.particles.emission;emission.rateOverTime=65;effect.particles.Play();return effect;
        }
        static Gradient Fade(Color color,float alpha=1)
        {
            var g=new Gradient();g.SetKeys(new[]{new GradientColorKey(Color.Lerp(color,Color.white,.5f),0),new GradientColorKey(color,.25f),new GradientColorKey(color,1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(alpha,.1f),new GradientAlphaKey(alpha*.65f,.5f),new GradientAlphaKey(0,1)});return g;
        }
        void Configure()
        {
            tint=ColorFor(Kind);if(corrupted)tint=Color.Lerp(tint,new Color(.65f,.06f,1),.65f);
            particles=MakeParticles(transform,tint,true,Kind==Style.Fire?.62f:.3f);
            particles.transform.localPosition=Vector3.back*.35f;
            var renderer=particles.GetComponent<ParticleSystemRenderer>();
            if(Kind==Style.Fire||Kind==Style.Ice){renderer.renderMode=ParticleSystemRenderMode.Stretch;renderer.lengthScale=1.3f;renderer.velocityScale=.035f;}
            var velocity=particles.velocityOverLifetime;velocity.enabled=true;velocity.space=ParticleSystemSimulationSpace.Local;velocity.x=new ParticleSystem.MinMaxCurve(0,0);velocity.y=new ParticleSystem.MinMaxCurve(0,0);velocity.z=new ParticleSystem.MinMaxCurve(-3,-1);
            var emission=particles.emission;emission.rateOverTime=Kind==Style.Fire?90:55;particles.Play();
            if(Kind==Style.Water){var main=particles.main;main.startSize=new ParticleSystem.MinMaxCurve(.06f,.18f);main.startLifetime=new ParticleSystem.MinMaxCurve(.25f,.65f);emission.rateOverTime=75;renderer.renderMode=ParticleSystemRenderMode.Stretch;renderer.lengthScale=1.6f;renderer.velocityScale=.03f;}
            trail=gameObject.AddComponent<TrailRenderer>();trail.sharedMaterial=Material(true);trail.time=Kind==Style.Wind?.4f:.3f;trail.minVertexDistance=.08f;trail.widthMultiplier=Kind==Style.Fire?.7f:.42f;trail.widthCurve=AnimationCurve.EaseInOut(0,1,1,0);trail.colorGradient=Fade(tint,.65f);trail.shadowCastingMode=ShadowCastingMode.Off;trail.receiveShadows=false;trail.Clear();
            var lightObject=new GameObject("Luz elemental");lightObject.layer=2;lightObject.transform.SetParent(transform,false);glow=lightObject.AddComponent<Light>();glow.color=tint;glow.intensity=1.4f;glow.range=3;glow.shadows=LightShadows.None;
            int count=Kind==Style.Lightning?3:(Kind==Style.Wind||Kind==Style.Water||Kind==Style.Web?2:0);
            arcs=new LineRenderer[count];
            for(int i=0;i<count;i++)
            {
                var arc=new GameObject("Arco elemental");arc.layer=2;arc.transform.SetParent(transform,false);var line=arc.AddComponent<LineRenderer>();line.sharedMaterial=Material(true);line.useWorldSpace=false;line.positionCount=Kind==Style.Lightning?9:36;line.widthMultiplier=Kind==Style.Lightning?.045f:.035f;line.colorGradient=Fade(tint,.8f);line.shadowCastingMode=ShadowCastingMode.Off;line.receiveShadows=false;arcs[i]=line;
            }
            Burst(transform.position,tint,false);
            UpdateArcs();
        }
        static ParticleSystem MakeParticles(Transform parent,Color color,bool loop,float size)
        {
            var go=new GameObject(loop?"Estela de particulas":"Destello elemental");go.layer=2;
            if(parent)go.transform.SetParent(parent,false);
            var ps=go.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=ps.main;main.playOnAwake=false;main.loop=loop;main.duration=.65f;main.startLifetime=new ParticleSystem.MinMaxCurve(.2f,.55f);main.startSpeed=new ParticleSystem.MinMaxCurve(.4f,1.5f);main.startSize=new ParticleSystem.MinMaxCurve(size*.4f,size);main.startColor=Color.white;main.simulationSpace=ParticleSystemSimulationSpace.World;main.maxParticles=120;main.useUnscaledTime=false;
            var shape=ps.shape;shape.shapeType=ParticleSystemShapeType.Sphere;shape.radius=.28f;
            var emission=ps.emission;emission.enabled=loop;
            var colors=ps.colorOverLifetime;colors.enabled=true;colors.color=Fade(color,.8f);
            var scale=ps.sizeOverLifetime;scale.enabled=true;scale.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.EaseInOut(0,1,1,0));
            var r=ps.GetComponent<ParticleSystemRenderer>();r.sharedMaterial=Material(false);r.renderMode=ParticleSystemRenderMode.Billboard;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;
            return ps;
        }
        public static void Explosion(Vector3 position,bool fire,float radius)
        {
            var burst=MakeParticles(null,fire?new Color(1,.3f,.03f):new Color(.55f,.3f,.12f),false,fire?1.8f:.7f);burst.transform.position=position;
            var main=burst.main;main.startSpeed=new ParticleSystem.MinMaxCurve(2,radius*1.7f);main.startLifetime=new ParticleSystem.MinMaxCurve(.3f,.8f);main.gravityModifier=fire?0:1;burst.Play();burst.Emit(fire?90:55);Destroy(burst.gameObject,1.2f);
            var smoke=MakeParticles(null,fire?new Color(.2f,.18f,.17f):new Color(.42f,.32f,.2f),false,fire?3.5f:2.6f);smoke.transform.position=position+Vector3.up*.4f;
            main=smoke.main;main.startLifetime=new ParticleSystem.MinMaxCurve(1,2.2f);main.startSpeed=new ParticleSystem.MinMaxCurve(1,fire?4:3.5f);main.gravityModifier=-.06f;
            var size=smoke.sizeOverLifetime;size.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,.3f,1,1));
            var material=Resources.Load<Material>("SlugVfxSmoke");if(material)smoke.GetComponent<ParticleSystemRenderer>().sharedMaterial=material;
            smoke.Play();smoke.Emit(fire?40:36);Destroy(smoke.gameObject,2.8f);
        }
        public static ParticleSystem Status(Transform parent,Color color)
        {
            var ps=MakeParticles(parent,color,true,.22f);ps.transform.localPosition=Vector3.up;
            var emission=ps.emission;emission.rateOverTime=24;var shape=ps.shape;shape.radius=.65f;ps.Play();return ps;
        }
        static void Burst(Vector3 position,Color color,bool impact)
        {
            var ps=MakeParticles(null,color,false,impact?.28f:.4f);ps.transform.position=position;
            var main=ps.main;main.startSpeed=new ParticleSystem.MinMaxCurve(impact?1:2,impact?3:5);main.startLifetime=new ParticleSystem.MinMaxCurve(.15f,.4f);
            ps.Play();ps.Emit(impact?18:28);Destroy(ps.gameObject,.8f);
        }
        void Update()
        {
            if(Finished)return;age+=Time.deltaTime;
            if(glow)glow.intensity=1.2f+Mathf.Sin(age*8)*.2f;
            if(Kind!=Style.Lightning||age>=nextArc){UpdateArcs();nextArc=age+.055f;}
        }
        void UpdateArcs()
        {
            for(int a=0;a<arcs.Length;a++)
            {
                var line=arcs[a];int count=line.positionCount;
                for(int i=0;i<count;i++)
                {
                    float t=i/(float)(count-1),angle=t*Mathf.PI*4+age*7+a*Mathf.PI;
                    float radius=.32f+Mathf.Sin(t*Mathf.PI)*.2f;
                    if(Kind==Style.Lightning){angle=a*Mathf.PI*2/3+Mathf.Sin(i*12.7f+nextArc*71)*.55f;radius=.42f+Mathf.Sin(i*19.3f+nextArc*47)*.17f;}
                    if(bodyLightning){angle=a*Mathf.PI*2/3+t*Mathf.PI*2+Mathf.Sin(i*12.7f+nextArc*71)*.55f;radius=bodyRadius*(.85f+Mathf.Sin(i*19.3f+nextArc*47)*.18f);line.SetPosition(i,new Vector3(Mathf.Cos(angle)*radius,.08f+t*(bodyHeight-.05f),Mathf.Sin(angle)*radius));}
                    else line.SetPosition(i,new Vector3(Mathf.Cos(angle)*radius,Mathf.Sin(angle)*radius,Mathf.Lerp(-2.1f,-.15f,t)));
                }
            }
        }
        public void End(bool impact)
        {
            if(Finished)return;Finished=true;
            if(impact)Burst(transform.position,tint,true);
            transform.SetParent(null,true);
            if(trail)trail.emitting=false;
            if(particles)particles.Stop(true,ParticleSystemStopBehavior.StopEmitting);
            if(glow)glow.enabled=false;
            if(arcs!=null)foreach(var arc in arcs)if(arc)arc.enabled=false;
            Destroy(gameObject,.8f);
        }
    }
}
