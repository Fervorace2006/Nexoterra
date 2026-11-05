using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Nexoterra.World;

namespace Nexoterra.Editor
{
    [InitializeOnLoad]
    public static class NexoterraReturnCheck
    {
        const string Key="Nexoterra.ReturnCheck";
        static bool ran;
        static NexoterraReturnCheck(){EditorApplication.update+=Tick;}
        public static void BatchRun()
        {
            EditorSceneManager.OpenScene(NexoterraBiomeBuilder.ScenePath);
            var inventory=UnityEngine.Object.FindAnyObjectByType<SlugInventory>();inventory.persist=false;
            inventory.owned.Clear();inventory.equipped=new[]{-1,-1,-1,-1,-1,-1};
            SessionState.SetBool(Key,true);File.WriteAllText("Library/NexoterraReturnCheck.txt","RUNNING");EditorApplication.isPlaying=true;
        }
        static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
        static void Tick()
        {
            if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying||EditorApplication.isCompiling||Time.time<3||ran)return;
            ran=true;
            try
            {
                var inv=UnityEngine.Object.FindAnyObjectByType<SlugInventory>();var player=inv.GetComponent<NexoterraThirdPerson>();player.enabled=false;
                var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="Test floor";floor.transform.position=new Vector3(0,999,0);floor.transform.localScale=new Vector3(100,2,100);
                var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.name="Enemy collider test";wall.transform.position=new Vector3(0,1002,15);wall.transform.localScale=new Vector3(4,4,1);
                player.Teleport(new Vector3(0,1000.1f,0));
                var muzzle=new GameObject("Test muzzle").transform;muzzle.position=new Vector3(0,1001,0);
                Physics.SyncTransforms();var d=inv.catalog.species[0];var other=inv.catalog.species[1];
                inv.Add(d.id);inv.Add(d.id);inv.Add(other.id);inv.selected=0;
                var shot=inv.Fire(muzzle,Vector3.forward);Check(shot&&inv.IsBusy(d.id),"Species did not lock");
                inv.selected=1;Check(inv.Fire(muzzle,Vector3.forward)==null,"Duplicate species bypassed lock");
                inv.selected=2;var different=inv.Fire(muzzle,Vector3.right);Check(different,"Other species incorrectly blocked");
                different.maxDistance=10;different.Advance(1);Check(different.IsReturning&&!different.IsTransformed,"Miss did not return in small form");
                inv.Equip(0,4);inv.selected=4;Check(inv.Fire(muzzle,Vector3.forward)==null,"Re-equipping bypassed lock");
                shot.Advance(8.1f/shot.speed);Check(shot.IsTransformed,"No transformation before collision");
                shot.Advance(.5f);Check(shot.IsReturning&&!shot.IsTransformed,"Collision did not remove transformation");
                Check(inv.IsBusy(d.id),"Species unlocked at impact");
                float peak=shot.transform.position.y,minY=peak;bool sawHop=false;
                // Follow a moving owner, then verify recovery really unlocks the species.
                for(int i=0;i<800&&!shot.HasArrived;i++)
                {
                    if(i==60)player.Teleport(new Vector3(5,1000.1f,-2));
                    float before=shot.transform.position.y;shot.Advance(.025f);
                    if(i>35&&shot.transform.position.y>before+.01f)sawHop=true;
                    peak=Mathf.Max(peak,shot.transform.position.y);minY=Mathf.Min(minY,shot.transform.position.y);
                }
                Check(sawHop,"No upward return hops");Check(shot.HasArrived&&!inv.IsBusy(d.id),"Did not reach moving owner / unlock");
                inv.selected=4;Check(inv.Fire(muzzle,Vector3.forward),"Cannot refire after arrival");
                // Restore the ordinary walking surface to test wild forward hopping.
                var wildObject=new GameObject("Wild jump test");wildObject.transform.position=new Vector3(20,1000,0);
                var wild=wildObject.AddComponent<WildSlug>();bool wildHop=false;
                for(int i=0;i<120;i++){wild.Advance(.025f);if(wild.transform.position.y>1000.05f)wildHop=true;}
                Check(wildHop,"Wild slug did not hop");
                // Untransformed impacts must follow exactly the same return lifecycle.
                var earlyObject=new GameObject("Early impact");earlyObject.transform.position=new Vector3(0,1001,14);
                var early=earlyObject.AddComponent<SlugProjectile>();early.Launch(d,Vector3.forward,inv);early.Advance(.1f);
                Check(early.IsReturning&&!early.IsTransformed,"Early impact lifecycle failed");
                var ceiling=GameObject.CreatePrimitive(PrimitiveType.Cube);ceiling.transform.position=new Vector3(30,1013,0);ceiling.transform.localScale=new Vector3(8,1,8);
                Physics.SyncTransforms();
                var upObject=new GameObject("Upward shot test");upObject.transform.position=new Vector3(30,1001,0);
                var up=upObject.AddComponent<SlugProjectile>();up.Launch(d,Vector3.up,inv);up.Advance(.35f);
                Check(up.Direction.y>.99f&&up.IsTransformed,"Upward aim was flattened");
                up.Advance(.3f);Check(up.IsReturning&&!up.IsTransformed,"Ceiling impact did not revert to small form");
                float impactY=up.transform.position.y;up.Advance(.2f);Check(up.transform.position.y<impactY,"Small slug did not fall from ceiling");
                var downObject=new GameObject("Downward shot test");downObject.transform.position=new Vector3(20,1010,0);
                var down=downObject.AddComponent<SlugProjectile>();down.Launch(d,Vector3.down,inv);down.Advance(.6f);
                Check(down.Direction.y<-.99f&&down.IsReturning&&!down.IsTransformed,"Downward aim / floor impact failed");
                File.WriteAllText("Library/NexoterraReturnCheck.txt","PASS\nSpecies lock includes duplicates and re-equipping; other species can fire.\nImpact removes transformation without unlocking.\nForward hops return to a moving owner and unlock on arrival; refire succeeds.\nMisses and pre-transformation impacts return in small form.\nWild forward hopping verified.\nUpward and downward aim, ceiling reversion and small-form falling verified.\n");
            }
            catch(Exception e){File.WriteAllText("Library/NexoterraReturnCheck.txt","FAIL\n"+e);}
            SessionState.SetBool(Key,false);EditorApplication.isPlaying=false;
            EditorApplication.delayCall+=()=>EditorApplication.Exit(File.ReadAllText("Library/NexoterraReturnCheck.txt").StartsWith("PASS")?0:1);
        }
    }
}
