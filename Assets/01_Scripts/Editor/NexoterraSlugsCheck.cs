using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Nexoterra.World;

namespace Nexoterra.Editor
{
    [InitializeOnLoad]
    public static class NexoterraSlugsCheck
    {
        const string Key="Nexoterra.SlugsCheck";
        static int step;
        static float next=3;
        static GameObject target;
        static SlugProjectile blocked;
        static SlugProjectile uiFlight;
        static NexoterraSlugsCheck()
        {
            EditorApplication.update+=Tick;
            EditorApplication.playModeStateChanged+=state=>
            {
                if(state==PlayModeStateChange.EnteredEditMode&&SessionState.GetBool(Key+"Restore",false))
                {
                    SessionState.SetBool(Key+"Restore",false);
                    var inventory=UnityEngine.Object.FindAnyObjectByType<SlugInventory>();
                    if(inventory){EditorJsonUtility.FromJsonOverwrite(SessionState.GetString(Key+"Inventory","{}"),inventory);EditorSceneManager.MarkSceneDirty(inventory.gameObject.scene);EditorSceneManager.SaveScene(inventory.gameObject.scene);}
                }
            };
        }
        public static void BatchRun()
        {
            EditorSceneManager.OpenScene(NexoterraBiomeBuilder.ScenePath);
            Begin();
        }
        static void Begin()
        {
            var inventory=UnityEngine.Object.FindAnyObjectByType<SlugInventory>();
            if(!Application.isBatchMode)
            {
                EditorSceneManager.SaveScene(inventory.gameObject.scene);
                SessionState.SetString(Key+"Inventory",EditorJsonUtility.ToJson(inventory));SessionState.SetBool(Key+"Restore",true);
                EditorApplication.ExecuteMenuItem("Window/General/Game");
            }
            inventory.persist=false;inventory.owned.Clear();inventory.equipped=new[]{-1,-1,-1,-1,-1,-1};
            SessionState.SetBool(Key,true);step=0;next=3;
            File.WriteAllText("Library/NexoterraSlugsCheck.txt","RUNNING\n");EditorApplication.isPlaying=true;
        }
        static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
        static void Tick()
        {
            if(EditorApplication.isCompiling||EditorApplication.isUpdating)return;
            if(File.Exists("Library/NexoterraSlugsCheck.request")){File.Delete("Library/NexoterraSlugsCheck.request");Begin();return;}
            if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying||Time.time<next)return;
            next=Time.time+2;
            try
            {
                var inventory=UnityEngine.Object.FindAnyObjectByType<SlugInventory>();var player=inventory.GetComponent<NexoterraThirdPerson>();
                if(step==0)
                {
                    Require(!inventory.persist,"Test must not write the user's save");
                    Require(inventory.catalog.species.Length==11,"Catalog count");
                    foreach(var d in inventory.catalog.species)
                    {
                        Require(d.normal&&d.transformed&&d.portrait,"Incomplete definition "+d.id);
                        Require(inventory.Add(d.id),"Cannot collect "+d.id);
                        var go=new GameObject("Projectile test");go.transform.position=new Vector3(0,100,0);
                        var shot=go.AddComponent<SlugProjectile>();shot.Launch(d,new Vector3(1,.6f,1));
                        shot.Advance(7.9f/shot.speed);Require(!shot.IsTransformed,"Early transformation "+d.id);
                        shot.Advance(.2f/shot.speed);Require(shot.IsTransformed,"Missing transformation "+d.id);
                        Require(Vector3.Dot((shot.transform.position-new Vector3(0,100,0)).normalized,new Vector3(1,.6f,1).normalized)>.999f,"Flight did not follow the 3D aim direction");
                        Require(shot.GetComponentsInChildren<Renderer>().Any(),"Missing transformed mesh");
                        UnityEngine.Object.Destroy(go);
                    }
                    Require(inventory.Count==11&&inventory.equipped.Length==6&&inventory.equipped.All(i=>i>=0),"Six automatic slots");
                    inventory.Equip(10,2);Require(inventory.equipped[2]==10&&inventory.Count==11,"Backpack swap lost a slug");
                    inventory.Equip(0,2);Require(inventory.equipped[2]==0&&inventory.equipped[0]==10,"Equipped slot swap");
                    Require(inventory.equipped.Distinct().Count()==6,"Duplicate equipment");
                    Require(!inventory.Equip(99,0),"Invalid index accepted");
                    player.food=1;var wild=WildSlug.Active.First(s=>s&&s.isActiveAndEnabled);
                    player.Teleport(wild.transform.position+Vector3.right*.3f);
                    typeof(NexoterraThirdPerson).GetMethod("Feed",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(player,null);
                    Require(inventory.Count==12&&player.food==0,"Feed did not enter inventory");
                    player.Teleport(player.spawn);
                    var shotFromGun=inventory.Fire(player.muzzle,Vector3.forward);
                    Require(shotFromGun&&Vector3.Distance(shotFromGun.transform.position,player.muzzle.position)<.001f,"Wrong muzzle origin");
                    Require(inventory.Fire(player.muzzle,Vector3.forward)==null,"Cooldown ignored");UnityEngine.Object.Destroy(shotFromGun.gameObject);
                    target=GameObject.CreatePrimitive(PrimitiveType.Cube);target.transform.position=new Vector3(0,100,3);
                    Physics.SyncTransforms();var blocker=new GameObject("Collision test");blocker.transform.position=new Vector3(0,100,0);
                    blocked=blocker.AddComponent<SlugProjectile>();blocked.Launch(inventory.catalog.species[0],Vector3.forward,inventory);blocked.Advance(1);
                    inventory.selected=2;step++;return;
                }
                if(step==1)
                {
                    Require(blocked&&blocked.IsReturning&&!blocked.IsTransformed,"Impact did not start the small-form return");UnityEngine.Object.Destroy(blocked.gameObject);UnityEngine.Object.Destroy(target);
                    inventory.selected=2;
                    uiFlight=inventory.Fire(player.muzzle,player.view.transform.forward);
                    if(uiFlight)uiFlight.speed=0;
                    Directory.CreateDirectory("Documentation/Nexoterra/Inventario");
                    ScreenCapture.CaptureScreenshot("Documentation/Nexoterra/Inventario/01_barra.png");step++;return;
                }
                if(step==2)
                {
                    if(uiFlight){uiFlight.BeginReturn();uiFlight.enabled=false;uiFlight.transform.position=player.transform.position+Vector3.forward*3;}
                    inventory.SetOpen(true);step++;return;
                }
                if(step==3){ScreenCapture.CaptureScreenshot("Documentation/Nexoterra/Inventario/02_mochila.png");step++;return;}
                if(step==4)
                {
                    File.WriteAllText("Library/NexoterraSlugsCheck.txt","PASS\n11 normal/transformed pairs and portraits.\nTransformation threshold at 8m; 3D aimed flight; visible transformed meshes.\n6-slot automatic equipment; backpack and equipped swaps preserve ownership.\nFood adoption enters inventory. Muzzle origin, cooldown and collision verified.\nTest uses a temporary collection; player save untouched.\n");Finish();
                }
            }
            catch(Exception e){File.WriteAllText("Library/NexoterraSlugsCheck.txt","FAIL\n"+e);Finish();}
        }
        static void Finish()
        {
            SessionState.SetBool(Key,false);EditorApplication.isPlaying=false;
            if(Application.isBatchMode)EditorApplication.delayCall+=()=>EditorApplication.Exit(File.ReadAllText("Library/NexoterraSlugsCheck.txt").StartsWith("PASS")?0:1);
        }
    }
}
