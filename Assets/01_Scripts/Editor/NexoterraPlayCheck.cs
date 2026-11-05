using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Nexoterra.World;

namespace Nexoterra.Editor
{
    [InitializeOnLoad]
    public static class NexoterraPlayCheck
    {
        const string Key="Nexoterra.PlayCheck";
        public static void RunBatch()
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(NexoterraBiomeBuilder.ScenePath);
            File.WriteAllText("Library/NexoterraPlayCheck.request","run");
        }
        static int step;
        static double next;
        static float simulationNext=2;
        static NexoterraPlayCheck(){EditorApplication.update+=Tick;}
        static void Tick()
        {
            if(EditorApplication.isCompiling||EditorApplication.isUpdating)return;
            if(File.Exists("Library/NexoterraPlayCheck.request"))
            {
                File.Delete("Library/NexoterraPlayCheck.request");
                SessionState.SetBool(Key,true);step=0;simulationNext=2;next=EditorApplication.timeSinceStartup+4;
                File.WriteAllText("Library/NexoterraPlayCheck.txt","RUNNING\n");
                EditorApplication.isPlaying=true;return;
            }
            if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying||EditorApplication.timeSinceStartup<next)return;
            if(Time.time<simulationNext)return;
            simulationNext=Time.time+2;
            try
            {
                var p=UnityEngine.Object.FindFirstObjectByType<NexoterraThirdPerson>();
                if(!p)throw new Exception("No player");
                if(step<6)
                {
                    if(step>0&&(!p.GetComponent<CharacterController>().isGrounded||p.transform.position.y<-.2f))throw new Exception("Grounding failed at zone "+(step-1)+" position="+p.transform.position+" grounded="+p.GetComponent<CharacterController>().isGrounded);
                    p.Teleport(p.travelPoints[step].position);step++;return;
                }
                if(step==6)
                {
                    if(!p.GetComponent<CharacterController>().isGrounded)throw new Exception("Final zone grounding failed");
                    if(WildSlug.Active.Count==0)throw new Exception("No runtime spawns");
                    var food=MushroomFood.Available.First(m=>m&&m.isActiveAndEnabled);
                    p.Teleport(food.transform.position+Vector3.right*.5f);
                    typeof(NexoterraThirdPerson).GetMethod("Collect",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(p,null);
                    if(p.food!=1||MushroomFood.Available.Contains(food))throw new Exception("Food collection failed");
                    var slug=WildSlug.Active.First(s=>s&&s.isActiveAndEnabled);
                    int activeBefore=WildSlug.Active.Count;
                    p.Teleport(slug.transform.position+Vector3.right*.5f);
                    typeof(NexoterraThirdPerson).GetMethod("Feed",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(p,null);
                    if(p.adopted!=1||p.food!=0||WildSlug.Active.Count!=activeBefore-1)throw new Exception("Adoption failed");
                    typeof(NexoterraThirdPerson).GetMethod("Shoot",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(p,null);
                    if(!p.tracer.enabled)throw new Exception("Weapon tracer failed");
                    p.male.SetActive(false);p.female.SetActive(true);
                    if(!p.female.GetComponentsInChildren<Renderer>().Any())throw new Exception("Female model missing");
                    File.WriteAllText("Library/NexoterraPlayCheck.txt","PASS\nSix biome landing points grounded in Play mode.\nRuntime slugs spawn. Mushroom collection and adoption succeed.\nWeapon tracer and both character models present.\n");
                    Finish();
                }
            }
            catch(Exception e){File.WriteAllText("Library/NexoterraPlayCheck.txt","FAIL\n"+e);Finish();}
        }
        static void Finish(){SessionState.SetBool(Key,false);EditorApplication.isPlaying=false;if(Application.isBatchMode)EditorApplication.delayCall+=()=>EditorApplication.Exit(File.ReadAllText("Library/NexoterraPlayCheck.txt").StartsWith("PASS")?0:1);}
    }
}
