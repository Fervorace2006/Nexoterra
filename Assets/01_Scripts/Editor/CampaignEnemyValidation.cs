using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Nexoterra.World;

[InitializeOnLoad]
public static class CampaignEnemyValidation
{
 const string Report="Library/CampaignEnemyValidation.result";
 static int stage;static float at;static double deadline;static EnemyShooter soldier;static WildSlug reward;
 static CampaignEnemyValidation(){EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredEditMode&&SessionState.GetBool("EnemyValidation",false)){SessionState.SetBool("EnemyValidation",false);EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>(SessionState.GetString("EnemyValidation.Start",""));}};}
 static void Check(bool value,string label){if(!value)throw new Exception(label);File.AppendAllText(Report,"PASS "+label+"\n");}
 static void Tick(){try{
  if(!EditorApplication.isPlayingOrWillChangePlaymode&&!EditorApplication.isCompiling&&!EditorApplication.isUpdating&&File.Exists("Library/CampaignEnemyValidation.request")){
   SessionState.SetString("EnemyValidation.Mode",File.ReadAllText("Library/CampaignEnemyValidation.request").Trim());File.Delete("Library/CampaignEnemyValidation.request");File.WriteAllText(Report,"");stage=0;deadline=0;
   SessionState.SetBool("EnemyValidation",true);SessionState.SetString("EnemyValidation.Start",AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/00_Scenes/Campaign/"+(SessionState.GetString("EnemyValidation.Mode","")=="water"?"Nexoterra_Agua":SessionState.GetString("EnemyValidation.Mode","")=="models"?"Nexoterra_Carmesi":"Nexoterra_Jardines")+".unity");EditorApplication.isPlaying=true;return;
  }
  if(!SessionState.GetBool("EnemyValidation",false)||!EditorApplication.isPlaying)return;
  if(deadline==0)deadline=EditorApplication.timeSinceStartup+150;
  if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Tiempo agotado en etapa "+stage);
  var c=CampaignLevel.Current;if(!c||!GameSession.Current||!GameSession.Current.Ready)return;
  if(stage==0&&Time.timeSinceLevelLoad>2){
   if(SessionState.GetString("EnemyValidation.Mode","")=="models"){Capture(c.enemies[0],"Enemy2");Capture(c.guardian,"SirBlack2");Capture(c.enemies.First(e=>e.name=="Secuaz de Sir Black"),"Secuaz");File.AppendAllText(Report,"COMPLETE model snapshots\n");EditorApplication.isPlaying=false;return;}
   if(c.index==2)Check(UnityEngine.Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None).Count(m=>m.name=="Lago del bioma"||m.name.StartsWith("Laguna adicional"))==3,"tres zonas de agua");
   Check(c.enemies.Length==(c.index>=4?9:8),"cantidad nivel "+c.index);
   Check(c.enemies.Count(e=>e.name=="Secuaz de Sir Black")==1,"un solo secuaz "+c.index);
   Check(c.enemies.Count(e=>e.name.StartsWith("Sir Black"))==(c.index>=4?1:0),"jefes por nivel "+c.index);
   var pool=UnityEngine.Object.FindObjectsByType<SlugSpawnPoint>(FindObjectsSortMode.None).Where(s=>s.localSpecies!=null).SelectMany(s=>s.localSpecies).Select(s=>SlugCatalog.BaseSpecies(s.id)).ToHashSet();
   foreach(var e in c.enemies){var ai=e.GetComponent<EnemyShooter>();Check(ai.slugSlots.Length==3&&ai.slugSlots.All(s=>(s.StartsWith("Malvada_")||s=="Elemental_Energia")&&(pool.Contains(SlugCatalog.BaseSpecies(s))||SlugCatalog.BaseSpecies(s)==CampaignLevel.Rewards[c.index])),"tres slots del bioma: "+e.name);var a=e.GetComponent<NativeEnemyAnimation>().animator;Check(a&&a.avatar&&a.avatar.isHuman&&a.avatar.isValid&&!a.applyRootMotion,"avatar "+e.name);}
   if(c.index==0)Capture(c.guardian,"Secuaz");if(c.index==5){Capture(c.enemies[0],"Enemy2");Capture(c.guardian,"SirBlack2");}
   soldier=c.enemies[0].GetComponent<EnemyShooter>();soldier.enabled=false;
   float hp=c.enemies[0].Health;Vector3 origin=soldier.transform.position+Vector3.up;
   for(int i=0;i<3;i++)Check(soldier.TryLaunch(origin,origin+Vector3.up*8),"lanzamiento slot "+i+" nivel "+c.index);
   Check(!soldier.TryLaunch(origin,origin+Vector3.up*8),"bloquea cuarto disparo "+c.index);Check(c.enemies[0].Health==hp,"enemigo no paga vida "+c.index);
   for(int i=0;i<3;i++){var shot=soldier.ProjectileInSlot(i);shot.transform.position=soldier.transform.position+Vector3.right*(2+i*.25f)+Vector3.up*.3f;shot.BeginReturn();}
   at=Time.time;stage=1;
  }
  if(stage==1){
   if(Enumerable.Range(0,3).Any(soldier.IsSlotBusy)){if(Time.time-at>20)throw new Exception("Babosas no regresaron, nivel "+c.index+" owner="+soldier.transform.position+" shots="+string.Join(";",Enumerable.Range(0,3).Select(i=>soldier.ProjectileInSlot(i)?soldier.ProjectileInSlot(i).transform.position.ToString():"ready")));return;}
   Check(true,"regreso real de las tres babosas "+c.index);
   c.enemies[0].ReceiveDamage(10000);var loot=UnityEngine.Object.FindObjectsByType<EnemySlugLoot>(FindObjectsSortMode.None).Where(l=>l.key.StartsWith("0:")).ToArray();Check(loot.Length==3,"suelta tres babosas "+c.index);
   c.CollectLoot(loot[0].key);loot[0].GetComponent<WildSlug>().Adopt();Check(GameSession.Current.Capture().collectedLoot.Contains(loot[0].key),"recogida persistente "+c.index);
   if(c.index==4){Check(!c.ambushBoss.gameObject.activeSelf,"jefe oculto antes de trampa");c.trapGuard.ReceiveDamage(10000);Check(c.ambushBoss.gameObject.activeSelf&&!c.guardian.IsDead,"doble batalla activada");Check(!WildSlug.Active.Any(c.IsReward),"secuaz trampa no suelta elemental");at=Time.time;stage=2;}
   else stage=3;
  }
  if(stage==2&&Time.time-at>.5f){Capture(c.guardian,"SirBlack1");stage=3;}
  if(stage==3){
   foreach(var enemy in c.enemies)enemy.ReceiveDamage(10000);
   Check(c.Remaining==0,"todos derrotados "+c.index);
   if(CampaignLevel.Rewards[c.index]!=""){
    reward=WildSlug.Active.FirstOrDefault(c.IsReward);Check(reward&&reward.species==(c.index==0?"":"Malvada_")+CampaignLevel.Rewards[c.index],"recompensa correcta "+c.index);Check(!c.Cleared,"curación requerida "+c.index);
    if(c.index!=0)typeof(SlugProjectile).GetMethod("PurifyWild",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{reward,GameSession.Current.GetComponent<SlugInventory>()});stage=4;
   }else stage=5;
  }
  if(stage==4&&reward&&!reward.GetComponent<SlugPurification>()){Check(reward.species==CampaignLevel.Rewards[c.index],"purificación "+c.index);GameSession.Current.GetComponent<SlugInventory>().Add(reward.species);reward.Adopt();stage=5;}
  if(stage==5){Check(c.Cleared,"progresión "+c.index);if(c.index==CampaignLevel.Scenes.Length-1||SessionState.GetString("EnemyValidation.Mode","")=="water"){if(c.index==5)Check(c.gameObject.scene.name=="Nexoterra_Carmesi","agua oscura es el final");File.AppendAllText(Report,"COMPLETE\n");EditorApplication.isPlaying=false;return;}GameSession.Current.Travel(CampaignLevel.Scenes[c.index+1]);stage=6;deadline=EditorApplication.timeSinceStartup+150;}
  if(stage==6&&Time.timeSinceLevelLoad<1)stage=0;
 }catch(Exception e){File.AppendAllText(Report,"FAIL "+e+"\n");EditorApplication.isPlaying=false;}}
 static void Capture(EnemyCombatant source,string name){
  string dir="Documentation/Nexoterra/Campaign/NewEnemyPreviews";Directory.CreateDirectory(dir);
  var ai=source.GetComponent<EnemyShooter>();Check(ai.weapon.IsChildOf(source.GetComponent<NativeEnemyAnimation>().animator.GetBoneTransform(HumanBodyBones.RightHand)),"arma sujeta a mano derecha "+name);var native=source.GetComponent<NativeEnemyAnimation>();bool aiOn=ai.enabled,nativeOn=native.enabled;ai.enabled=native.enabled=false;
  var camObject=new GameObject("Cámara de revisión");var cam=camObject.AddComponent<Camera>();cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.07f,.09f,.12f);cam.fieldOfView=34;cam.nearClipPlane=.05f;cam.farClipPlane=15;
  var rt=new RenderTexture(800,900,24);rt.Create();var previous=RenderTexture.active;
  try{
   foreach(string state in new[]{"Idle","Run","CaidaAtras","levantarsedecaida"}){
    native.animator.Play(state,0,.3f);native.animator.SetLayerWeight(1,state=="Idle"?1:0);native.animator.Update(.01f);
    cam.transform.position=source.transform.position+new Vector3(-2.5f,1.7f,4.5f);cam.transform.LookAt(source.transform.position+Vector3.up*1.15f);
    RenderPipeline.SubmitRenderRequest(cam,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});RenderTexture.active=rt;var png=new Texture2D(800,900,TextureFormat.RGB24,false);png.ReadPixels(new Rect(0,0,800,900),0,0);png.Apply();File.WriteAllBytes(dir+"/"+name+"_"+state+".png",png.EncodeToPNG());UnityEngine.Object.Destroy(png);
   }
  }finally{RenderTexture.active=previous;rt.Release();UnityEngine.Object.Destroy(rt);UnityEngine.Object.Destroy(camObject);ai.enabled=aiOn;native.enabled=nativeOn;}
 }
}


