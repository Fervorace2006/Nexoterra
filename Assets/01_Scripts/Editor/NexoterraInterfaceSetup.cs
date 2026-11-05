using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Nexoterra.World;
namespace Nexoterra.Editor
{
 [InitializeOnLoad] public static class NexoterraInterfaceSetup
 {
  static double captureAt;static bool stopAfterCapture;
  static NexoterraInterfaceSetup(){EditorApplication.update+=Poll;}
  static void Poll(){
   if(captureAt>0&&EditorApplication.timeSinceStartup>captureAt){
    captureAt=0;
    if(stopAfterCapture){stopAfterCapture=false;EditorApplication.isPlaying=false;return;}
    Directory.CreateDirectory("Documentation/Nexoterra/Interfaz");ScreenCapture.CaptureScreenshot("Documentation/Nexoterra/Interfaz/HUD.png");stopAfterCapture=true;captureAt=EditorApplication.timeSinceStartup+2;return;
   }
   if(EditorApplication.isPlaying&&GameSession.Current&&GameSession.Current.Ready&&File.Exists("Library/NexoterraHudPreview.request")){File.Delete("Library/NexoterraHudPreview.request");Preview();return;}
   if(EditorApplication.isCompiling||EditorApplication.isUpdating||!File.Exists("Library/NexoterraInterface.request"))return;if(EditorApplication.isPlayingOrWillChangePlaymode){EditorApplication.isPlaying=false;return;}File.Delete("Library/NexoterraInterface.request");try{Build();File.WriteAllText("Library/NexoterraInterface.result.txt","PASS · menu, guardado manual, HUD y retratos instalados");}catch(Exception e){File.WriteAllText("Library/NexoterraInterface.result.txt",e.ToString());}}
  static void Preview()
  {
   var session=GameSession.Current;if(!session)return;session.Resume();
   var player=session.GetComponent<NexoterraThirdPerson>();var inventory=session.GetComponent<SlugInventory>();
   foreach(string id in new[]{"Infierno","tazerling","Armashelt","Elemental_Agua","Carnero","Elemental_Fuego"})inventory.Add(id);
   session.GetComponent<PlayerHealth>().ReceiveDamage(27);
   foreach(var ai in UnityEngine.Object.FindObjectsByType<EnemyShooter>())ai.enabled=false;
   var sequence=UnityEngine.Object.FindAnyObjectByType<BossEncounters>();sequence.enabled=false;
   var enemy=UnityEngine.Object.Instantiate(sequence.bossPrefab,player.transform.position+Vector3.forward*20,Quaternion.identity).GetComponent<EnemyCombatant>();enemy.GetComponent<EnemyShooter>().enabled=false;enemy.maxHealth=420;enemy.Revive();enemy.ReceiveDamage(110);
   typeof(BossEncounters).GetField("active",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(sequence,enemy);
   captureAt=EditorApplication.timeSinceStartup+2;
  }
  public static void BatchBuild(){EditorSceneManager.OpenScene(NexoterraBiomeBuilder.ScenePath);Build();}
  [MenuItem("Nexoterra/Interfaz/Instalar menu y HUD")]
  public static void Build()
  {
   var p=UnityEngine.Object.FindAnyObjectByType<NexoterraThirdPerson>();if(!p)throw new Exception("Abrir CuatroNodos");
   var scene=p.gameObject.scene;EditorSceneManager.SaveScene(scene);File.Copy(scene.path,"Library/AntesInterfaz_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".unity",true);
   if(!p.GetComponent<GameSession>())p.gameObject.AddComponent<GameSession>();var hud=p.GetComponent<NexoHud>();if(!hud)hud=p.gameObject.AddComponent<NexoHud>();
   var companion=GameObject.Find("Comando · companera en el refugio");if(companion&&!companion.GetComponent<RiggedCharacterModel>()){var idle=companion.GetComponent<CompanionIdle>();if(!idle)idle=companion.AddComponent<CompanionIdle>();idle.pose=HumanoidRigBuilder.Build(companion,"ComandoRefugio");}
   hud.malePortrait=Portrait(p.male,"Militar");hud.femalePortrait=Portrait(p.female,"Comando");
   var settings=new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);if(!settings.Exists(s=>s.path==scene.path)){settings.Add(new EditorBuildSettingsScene(scene.path,true));EditorBuildSettings.scenes=settings.ToArray();}
   EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
  }
  public static Texture2D Portrait(GameObject source,string name)
  {
   const string folder="Assets/NexoterraWorld/Interface";Directory.CreateDirectory(folder);
   var clone=UnityEngine.Object.Instantiate(source);clone.SetActive(true);clone.transform.position=new Vector3(0,3000,0);clone.transform.rotation=Quaternion.identity;
   foreach(var t in clone.GetComponentsInChildren<Transform>(true))t.gameObject.layer=31;
   foreach(var component in clone.GetComponentsInChildren<MonoBehaviour>())component.enabled=false;
   var head=clone.GetComponentInChildren<HumanoidPose>();Vector3 focus=head&&head.head?head.head.position:clone.transform.position+Vector3.up*1.7f;
   var go=new GameObject("Portrait camera");var camera=go.AddComponent<Camera>();camera.cullingMask=1<<31;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.025f,.18f,.22f);camera.orthographic=true;camera.orthographicSize=.29f;camera.nearClipPlane=.01f;camera.farClipPlane=10;camera.transform.position=focus+new Vector3(.05f,.02f,2);camera.transform.LookAt(focus);
   var lightGo=new GameObject("Portrait light");var light=lightGo.AddComponent<Light>();light.type=LightType.Directional;light.intensity=2;light.cullingMask=1<<31;light.transform.rotation=Quaternion.Euler(30,160,0);
   var rt=new RenderTexture(256,256,24);camera.targetTexture=rt;camera.Render();var previous=RenderTexture.active;RenderTexture.active=rt;var texture=new Texture2D(256,256,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,256,256),0,0);texture.Apply();RenderTexture.active=previous;
   string path=folder+"/"+name+"_retrato.png";File.WriteAllBytes(path,texture.EncodeToPNG());camera.targetTexture=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(texture);UnityEngine.Object.DestroyImmediate(go);UnityEngine.Object.DestroyImmediate(lightGo);UnityEngine.Object.DestroyImmediate(clone);
   AssetDatabase.ImportAsset(path);return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
  }
 }
}
