#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Nexoterra.World;
using Object=UnityEngine.Object;

public sealed class UltimasSlugTestRunner:MonoBehaviour
{
    SlugCatalog catalog;SlugInventory inventory;int passed;
    static void Invoke(object target,string name,params object[] args)=>target.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(target,args);
    void Check(bool condition,string name){if(!condition)throw new Exception(name);passed++;File.AppendAllText("Library/UltimasChecks.result","PASS "+name+"\n");}
    IEnumerator Start(){File.AppendAllText("Library/UltimasChecks.result","START RUNNER\n");Application.runInBackground=true;Time.timeScale=1;var tests=Run();while(true){object current;try{if(!tests.MoveNext())break;current=tests.Current;}catch(Exception e){File.AppendAllText("Library/UltimasChecks.result","FAIL "+e);EditorApplication.isPlaying=false;yield break;}yield return current;}File.AppendAllText("Library/UltimasChecks.result","COMPLETE "+passed+" checks\n");EditorApplication.isPlaying=false;}
    SlugProjectile Shot(string id,Vector3 position){
        inventory.Add(id);inventory.transform.position=position;inventory.transformationDistance=0;
        var shot=inventory.Fire(inventory.transform,Vector3.forward);if(!shot)throw new Exception("Cannot launch "+id);
        shot.Advance(.001f);shot.speed=0;return shot;
    }
    void Clear(){foreach(var p in FindObjectsByType<SlugProjectile>(FindObjectsSortMode.None)){p.gameObject.SetActive(false);Destroy(p.gameObject);}inventory.owned.Clear();inventory.equipped=new[]{-1,-1,-1,-1,-1,-1};}
    IEnumerator Run(){
        catalog=AssetDatabase.LoadAssetAtPath<SlugCatalog>("Assets/NexoterraWorld/Slugs/Catalog.asset");
        var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.position=new Vector3(0,-.5f,0);floor.transform.localScale=new Vector3(200,1,200);
        inventory=new GameObject("Owner").AddComponent<SlugInventory>();inventory.catalog=catalog;inventory.enabled=false;
        foreach(string id in new[]{"Demoledora","Malvada_Demoledora","Malvada_Goon"}){var d=catalog.Find(id);Check(d!=null&&d.normal&&d.transformed&&d.portrait,"catalog/model/portrait "+id);}
        Check(Mathf.Approximately(catalog.Find("Demoledora").Combat.impactDamage,26),"balanced impact 26");
        Check(catalog.Find("Malvada_Demoledora").Combat.impactDamage>26,"evil increased damage");
        var enemy=new GameObject("Target").AddComponent<EnemyCombatant>();enemy.enabled=false;float health=enemy.Health;enemy.ReceiveSlug(catalog.Find("Demoledora"),Vector3.forward,true);
        Check(Mathf.Approximately(health-enemy.Health,26)&&enemy.IsKnockedDown,"single damage and knockdown");Destroy(enemy.gameObject);
        var rock=new GameObject("Ceiling rock").AddComponent<WorldDestructible>();rock.kind=WorldDestructible.Kind.CeilingRock;
        Check(rock.Hit("Malvada_Demoledora",true)&&rock.IsFalling,"evil Demoledora breaks ceiling rock");Destroy(rock.gameObject);
        Check(EnemySlugLoot.Spawn(catalog.Find("Malvada_Goon"),Vector3.zero,"test")==null,"Goon never drops");
        var goon=Shot("Malvada_Goon",new Vector3(0,2,20));var good=Shot("Demoledora",new Vector3(10,2,20));
        good.transform.position=goon.transform.position+Vector3.forward*.5f;Invoke(goon,"CheckProjectileContacts",1f);
        Check(!goon.IsReturning&&good.IsReturning&&!good.IsTransformed,"Goon continues and target falls small");
        inventory.transform.position=new Vector3(10,0,20);yield return new WaitForSeconds(.85f);
        Check(good.Species=="Malvada_Demoledora"&&inventory.owned.Contains("Malvada_Demoledora")&&inventory.IsBusy("Malvada_Demoledora"),"corruption updates inventory and busy lock");
        Clear();yield return null;
        var doc=Shot("Gon_Doc",new Vector3(0,2,20));var evil=Shot("Malvada_Demoledora",new Vector3(10,2,20));
        evil.transform.position=doc.transform.position+Vector3.forward*.5f;Invoke(doc,"CheckProjectileContacts",1f);
        Check(!doc.IsReturning&&evil.IsReturning,"Doc continues while evil slug falls");inventory.transform.position=new Vector3(10,0,20);yield return new WaitForSeconds(.85f);
        Check(evil.Species=="Demoledora"&&inventory.IsBusy("Demoledora"),"Doc restores flying slug");
        Clear();yield return null;
        doc=Shot("Gon_Doc",new Vector3(0,2,20));goon=Shot("Malvada_Goon",new Vector3(10,2,20));goon.transform.position=doc.transform.position+Vector3.forward*.5f;
        Invoke(doc,"CheckProjectileContacts",1f);Check(doc.IsReturning&&goon.IsReturning&&!doc.IsTransformed&&!goon.IsTransformed,"Doc Goon mutual small stun");
        inventory.transform.position=new Vector3(10,0,20);yield return new WaitForSeconds(1);Check(doc&&goon&&!doc.HasArrived&&!goon.HasArrived,"stun delays recovery");
        yield return new WaitForSeconds(5);Check(!doc&&!goon,"both return after stun");Clear();yield return null;
        var wild=EnemySlugLoot.Spawn(catalog.Find("Demoledora"),new Vector3(20,0,20),"wild");var before=wild.transform.position;
        goon=Shot("Malvada_Goon",new Vector3(30,2,20));Invoke(goon,"CorruptArea",wild.transform.position);
        yield return new WaitForSeconds(1.6f);Check(wild.species=="Malvada_Demoledora","Goon area corrupts wild slug");
        UnityEngine.Random.InitState(12);yield return new WaitForSeconds(6);
        Check(Vector3.Distance(wild.transform.position,before)>.2f,"enemy loot roams");Destroy(wild.gameObject);Clear();
        yield return null;
        var a=Shot("Malvada_Demoledora",new Vector3(-1.25f,1.4f,0));var b=Shot("Malvada_Goon",new Vector3(1.25f,1.4f,0));
        var camera=new GameObject("Visual validation camera").AddComponent<Camera>();camera.transform.position=new Vector3(4,3,7);camera.transform.LookAt(new Vector3(0,1.3f,1));camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.025f,.035f,.05f);camera.fieldOfView=40;
        var light=new GameObject("Preview light").AddComponent<Light>();light.type=LightType.Directional;light.intensity=2;light.transform.rotation=Quaternion.Euler(40,-140,0);
        RenderSettings.ambientLight=new Color(.45f,.45f,.45f);
        for(int i=0;i<20;i++){a.transform.position+=Vector3.forward*.08f;b.transform.position+=Vector3.forward*.08f;yield return new WaitForSeconds(.025f);}
        foreach(var shot in new[]{a,b}){var tail=shot.GetComponentInChildren<TrailRenderer>();var color=tail.colorGradient.Evaluate(.3f);Check(color.r>.8f&&color.g<.15f,"red tail "+shot.Species);}
        string dir=Path.GetFullPath(Path.Combine(Application.dataPath,"../Documentation/Ultimas"));Directory.CreateDirectory(dir);
        var rt=new RenderTexture(1100,700,24);rt.Create();var previous=RenderTexture.active;
        UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera,new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest{destination=rt});
        RenderTexture.active=rt;var image=new Texture2D(1100,700,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1100,700),0,0);image.Apply();File.WriteAllBytes(dir+"/EstelasRojas_v2.png",image.EncodeToPNG());RenderTexture.active=previous;rt.Release();Destroy(rt);Destroy(image);
        Clear();yield return null;
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/02_Prefabs/Gameplay/Player.prefab");
        var actor=Instantiate(prefab,new Vector3(0,1,-10),Quaternion.identity);
        foreach(var behaviour in actor.GetComponentsInChildren<MonoBehaviour>())if(!(behaviour is NativePlayerAnimation)&&!(behaviour is PlayerEmotes)&&!(behaviour is PlayerAbilities))behaviour.enabled=false;
        foreach(var c in actor.GetComponentsInChildren<Camera>())c.enabled=false;
        var session=actor.GetComponent<GameSession>();if(session)session.menu=false;
        var motor=actor.GetComponent<CharacterController>();var driver=actor.GetComponent<NativePlayerAnimation>();var emote=actor.GetComponent<PlayerEmotes>();var movement=actor.GetComponent<NexoterraThirdPerson>();var abilities=actor.GetComponent<PlayerAbilities>();
        motor.Move(Vector3.down*2);yield return null;
        Check(emote.BeginDance(),"dance starts while standing");Vector3 start=actor.transform.position;
        yield return new WaitForSeconds(.4f);
        Check(driver.male.GetCurrentAnimatorStateInfo(0).IsName("Dance1")||driver.female.GetCurrentAnimatorStateInfo(0).IsName("Dance1"),"dance animator state");
        yield return new WaitForSeconds(1);Check(Vector3.Distance(start,actor.transform.position)<.01f,"dance stays in place");
        motor.Move(Vector3.right*.3f);Invoke(driver,"Update");Check(!emote.IsDancing,"movement cancels dance");
        motor.Move(Vector3.down*.1f);yield return null;
        movement.male.SetActive(false);movement.female.SetActive(true);yield return null;
        Check(emote.BeginDance(),"female retarget supports dance");yield return new WaitForSeconds(.3f);
        Check(driver.female.GetCurrentAnimatorStateInfo(0).IsName("Dance1"),"female dance state");emote.Cancel();
        motor.Move(Vector3.down*.1f);yield return null;
        Check(abilities.TryDodge(1,Vector3.forward,true),"forward roll starts");start=actor.transform.position;bool observed=false;
        while(abilities.IsDodging){motor.Move((abilities.DodgeDirection*abilities.DodgeSpeed+Vector3.down*2)*Time.deltaTime);observed|=driver.female.GetCurrentAnimatorStateInfo(0).IsName("RollForward");yield return null;}
        Check(observed,"forward roll plays supplied clip");float distance=Vector3.Distance(start,actor.transform.position);Check(distance>3&&distance<5,"forward roll displaces controller");
        Vector3 end=actor.transform.position;yield return new WaitForSeconds(.8f);Check(Vector3.Distance(end,actor.transform.position)<.01f,"roll does not snap back");
        Check(!abilities.TryDodge(-1,Vector3.forward,true),"backward roll remains disabled");
        var bag=actor.GetComponent<SlugInventory>();bag.owned.Clear();bag.equipped=new[]{-1,-1,-1,-1,-1,-1};bag.Add("Demoledora");
        driver.PlayShot();yield return new WaitForSeconds(.4f);
        Check(!driver.TryAutoReload(false),"automatic reload off respects manual mode");
        Check(driver.TryAutoReload(true),"automatic reload starts on available selected slug");
        yield return new WaitForSeconds(actor.GetComponent<PlayerAnimationDriver>().reloadDuration+.2f);
        Check(driver.IsLoaded&&!driver.IsReloading,"automatic reload waits for animation");
        Check(!driver.TryAutoReload(true),"loaded slug cannot reload twice");
        bag.Add("Gon_Doc");Check(driver.TryAutoReload(true),"changing selected slot loads its slug");
        yield return new WaitForSeconds(actor.GetComponent<PlayerAnimationDriver>().reloadDuration+.2f);
        var muzzle=new GameObject("Test muzzle").transform;muzzle.position=actor.transform.position+Vector3.up+Vector3.forward*2;
        var returning=bag.Fire(muzzle,Vector3.forward);driver.PlayShot();returning.BeginReturn();
        Check(!driver.TryAutoReload(true),"returning slug blocks automatic reload");
        yield return new WaitForSeconds(2);
        Check(!bag.IsBusy("Gon_Doc")&&driver.TryAutoReload(true),"automatic reload resumes after return");Destroy(muzzle.gameObject);
        Destroy(actor);yield return null;
        var menu=new GameObject("Menu validation").AddComponent<FrontEndMenu>();yield return new WaitForSeconds(.2f);
        ScreenCapture.CaptureScreenshot(Path.Combine(dir,"Menu.png"));yield return new WaitForSeconds(.2f);
        typeof(FrontEndMenu).GetField("settings",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(menu,true);yield return new WaitForSeconds(.2f);
        ScreenCapture.CaptureScreenshot(Path.Combine(dir,"Configuracion.png"));yield return new WaitForSeconds(.3f);


    }
}

#endif
