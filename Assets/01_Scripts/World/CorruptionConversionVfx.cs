using UnityEngine;

namespace Nexoterra.World
{
    // Owned by the conversion coroutine: it stays alive until the new model is visible.
    public sealed class CorruptionConversionVfx : MonoBehaviour
    {
        readonly LineRenderer[] arcs = new LineRenderer[5];
        float nextArc;
        void Awake()
        {
            gameObject.layer = 2;
            var smokeObject = new GameObject("Humo rojo");
            smokeObject.transform.SetParent(transform, false);
            smokeObject.layer = 2;
            var smoke = smokeObject.AddComponent<ParticleSystem>();
            smoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = smoke.main;
            main.loop = true;
            main.startLifetime = .65f;
            main.startSpeed = .12f;
            main.startSize = new ParticleSystem.MinMaxCurve(.25f, .5f);
            main.startColor = new Color(.8f, .015f, .025f, .75f);
            main.maxParticles = 100;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            var emission = smoke.emission;
            emission.rateOverTime = 65;
            var shape = smoke.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = .23f;
            shape.position = Vector3.up * .2f;
            var colors = smoke.colorOverLifetime;
            colors.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(new Color(.5f,.1f,.1f), 1) },
                new[] { new GradientAlphaKey(0,0), new GradientAlphaKey(.85f,.15f), new GradientAlphaKey(0,1) });
            colors.color = gradient;
            smoke.GetComponent<ParticleSystemRenderer>().sharedMaterial = Resources.Load<Material>("SlugVfxSmoke");
            smoke.Play();
            smoke.Emit(25);
            for(int i=0;i<arcs.Length;i++)
            {
                var go = new GameObject("Arco rojo");
                go.layer = 2;
                go.transform.SetParent(transform,false);
                var line = go.AddComponent<LineRenderer>();
                line.useWorldSpace = false;
                line.sharedMaterial = SlugFlightEffects.RibbonMaterial;
                line.positionCount = 12;
                line.widthMultiplier = .014f;
                line.startColor = new Color(1,.025f,.035f);
                line.endColor = new Color(1,.12f,.08f);
                arcs[i] = line;
            }
            Update();
        }
        void Update()
        {
            if(Time.time<nextArc)return;
            nextArc=Time.time+.045f;
            for(int i=0;i<arcs.Length;i++)
                for(int j=0;j<12;j++)
                {
                    float angle=(j/11f*1.7f+i*1.2566f+Time.time*3);
                    float radius=.24f+Random.Range(-.035f,.035f);
                    arcs[i].SetPosition(j,new Vector3(Mathf.Cos(angle)*radius,
                        .025f+j/11f*.4f+Random.Range(-.025f,.025f),Mathf.Sin(angle)*radius));
                }
        }
    }
}
