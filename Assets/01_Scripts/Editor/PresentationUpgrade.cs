using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Nexoterra.World;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class PresentationUpgrade
{
 static PresentationUpgrade(){EditorApplication.update+=Poll;}
 static void Poll(){
  const string request="Library/PresentationUpgrade.request";
  if(!File.Exists(request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
  var mode=File.ReadAllText(request).Trim();File.Delete(request);
  try{if(mode=="portraits")CapturePortraits();else if(mode=="prefabs")ConnectPrefabs();else if(mode=="landmarks")CampaignLandmarkRestore.Run();else if(mode=="rolls")InstallRolls();else if(mode=="finish")Finish();else if(mode=="organize")OrganizeModels();else Audit();File.WriteAllText("Library/PresentationUpgrade.result","PASS "+mode);}catch(Exception e){File.WriteAllText("Library/PresentationUpgrade.result",e.ToString());Debug.LogException(e);}
 }
 static void Finish(){
  AssetDatabase.Refresh();
  foreach(string role in new[]{"Player","Enemy1","Enemy2","Secuaz_Sir_Black","SirBlack","Sir_Black_2"}){
   string path=PrefabFolder+"/"+(role=="Player"?"":"Enemies/")+role+".prefab";var go=PrefabUtility.LoadPrefabContents(path);
   try{
    if(role=="Player"){var hud=go.GetComponent<NexoHud>();hud.malePortrait=Resources.Load<Texture2D>("Nexoterra/UI/Player_Male");hud.femalePortrait=Resources.Load<Texture2D>("Nexoterra/UI/Player_Female");if(!hud.malePortrait||!hud.femalePortrait)throw new Exception("Faltan retratos del jugador");}
    else {var hp=go.GetComponent<EnemyCombatant>();hp.portrait=Resources.Load<Texture2D>("Nexoterra/UI/"+(role=="SirBlack"?"Sir_Black":role));if(!hp.portrait)throw new Exception("Falta retrato "+role);}
    PrefabUtility.SaveAsPrefabAsset(go,path);
   }finally{PrefabUtility.UnloadPrefabContents(go);}
  }
  var active=SceneManager.GetActiveScene();string original="Assets/00_Scenes/Nexoterra_CuatroNodos.unity";var scene=SceneManager.GetSceneByPath(original);bool opened=!scene.isLoaded;if(opened)scene=EditorSceneManager.OpenScene(original,OpenSceneMode.Additive);
  try{
   if(scene.isDirty)throw new Exception("Guarda Cuatro Nodos antes de vincular el jugador.");
   var player=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<NexoterraThirdPerson>(true)).Single();Convert(player.gameObject,AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder+"/Player.prefab"));RemoveOverrides(player.gameObject,new[]{"maxHealth","respawnDelay","walkSpeed","sprintSpeed","dodgeDuration","dodgeDistance","dodgeCooldown","malePortrait","femalePortrait"});
   EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
  }finally{if(opened)EditorSceneManager.CloseScene(scene,true);SceneManager.SetActiveScene(active);}
  AssetDatabase.SaveAssets();Audit();
 }
 static void OrganizeModels(){
  foreach(var pair in new[]{("Assets/NexoterraWorld/Characters",PrefabFolder+"/Models"),("Assets/NexoterraWorld/Biomes/Creatures",PrefabFolder+"/WildSlugs")})if(AssetDatabase.IsValidFolder(pair.Item1)&&!AssetDatabase.IsValidFolder(pair.Item2)){string error=AssetDatabase.MoveAsset(pair.Item1,pair.Item2);if(error!="")throw new Exception(error);}
  foreach(string name in new[]{"Wild_Tornado","Wild_Elemental_Energia"}){string old="Assets/NexoterraWorld/Expansion/"+name+".prefab";if(AssetDatabase.LoadAssetAtPath<GameObject>(old)){string error=AssetDatabase.MoveAsset(old,PrefabFolder+"/WildSlugs/"+name+".prefab");if(error!="")throw new Exception(error);}}
  var catalog=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder+"/Player.prefab").GetComponent<SlugInventory>().catalog;
  foreach(string guid in AssetDatabase.FindAssets("t:Prefab",new[]{PrefabFolder+"/WildSlugs"})){
   string path=AssetDatabase.GUIDToAssetPath(guid);var root=PrefabUtility.LoadPrefabContents(path);
   try{var slug=root.GetComponent<WildSlug>();if(!slug||root.transform.childCount==0)continue;var def=catalog.Find(slug.species);if(def==null||!def.normal)continue;Convert(root.transform.GetChild(0).gameObject,def.normal);PrefabUtility.SaveAsPrefabAsset(root,path);}
   finally{PrefabUtility.UnloadPrefabContents(root);}
  }
  catalog.species=catalog.species.Where(s=>s.id!="Malvada_Elemental_Energia").ToArray();EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssets();
 }
 static void InstallRolls(){
  string path="Assets/Characters/Animations/Quaternius/SirBlack/Roll.fbx";var importer=(ModelImporter)AssetImporter.GetAtPath(path);
  importer.animationType=ModelImporterAnimationType.Human;importer.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;importer.importAnimation=true;
  var clips=importer.defaultClipAnimations;foreach(var c in clips){c.loopTime=false;c.lockRootRotation=true;c.lockRootHeightY=true;c.lockRootPositionXZ=true;c.keepOriginalOrientation=true;c.keepOriginalPositionXZ=true;}importer.clipAnimations=clips;importer.SaveAndReimport();
  var avatar=AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();if(!avatar||!avatar.isValid||!avatar.isHuman)throw new Exception("La voltereta frontal necesita un avatar humanoide válido.");
  var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/NexoterraWorld/Locomotion/RollForward_Grounded.anim")??AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().First(c=>!c.name.StartsWith("__preview"));
  foreach(string name in new[]{"Player","Enemy1","SirBlack"}){
   var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/NexoterraWorld/Locomotion/Imported/"+name+"_Animations.controller");var sm=controller.layers[0].stateMachine;
   foreach(string stateName in new[]{"RollForward","RollBackward"}){var state=sm.states.Select(s=>s.state).FirstOrDefault(s=>s.name==stateName);if(!state)state=sm.AddState(stateName);state.motion=clip;state.speed=(stateName=="RollBackward"?-1:1)*clip.length/(40f/30f);state.cycleOffset=stateName=="RollBackward"?1:0;EditorUtility.SetDirty(state);}
   EditorUtility.SetDirty(controller);
  }
  AssetDatabase.SaveAssets();File.WriteAllText("Documentation/Nexoterra/Upgrade/Roll.txt","Forward roll humanoid valid; duration "+clip.length+" seconds. Backward plays the same somersault in reverse.");
 }
 const string PrefabFolder="Assets/02_Prefabs/Gameplay";
 static string Role(EnemyCombatant e)=>e.name.StartsWith("Enemy2")?"Enemy2":e.name.Contains("Secuaz")?"Secuaz_Sir_Black":e.name.Contains("Sir Black 2")?"Sir_Black_2":e.name.Contains("Sir Black")?"SirBlack":"Enemy1";
 static void ConnectPrefabs(){
  Directory.CreateDirectory(PrefabFolder+"/Enemies");Directory.CreateDirectory(PrefabFolder+"/Slugs");AssetDatabase.Refresh();
  string backup="Library/PresentationBackup/"+DateTime.Now.ToString("yyyyMMdd_HHmmss");Directory.CreateDirectory(backup);
  var active=SceneManager.GetActiveScene();var scenes=new System.Collections.Generic.List<(Scene scene,bool opened)>();
  try{
   foreach(string name in CampaignLevel.Scenes.Concat(new[]{"Nexoterra_Arena"})){
    string path="Assets/00_Scenes/Campaign/"+name+".unity";File.Copy(path,backup+"/"+name+".unity");
    var scene=SceneManager.GetSceneByPath(path);bool opened=!scene.isLoaded;if(opened)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
    if(scene.isDirty)throw new Exception("Guarda tus cambios en "+path+" antes de vincular prefabs.");scenes.Add((scene,opened));
   }
   var enemies=scenes.SelectMany(s=>s.scene.GetRootGameObjects()).SelectMany(r=>r.GetComponentsInChildren<EnemyCombatant>(true)).ToArray();
   foreach(var group in enemies.GroupBy(Role)){
    // The most frequent edited scene value wins, preserving the user's current balance.
    float hp=group.GroupBy(e=>e.maxHealth).OrderByDescending(g=>g.Count()).First().Key;
    var source=group.First(e=>e.maxHealth==hp);string path=PrefabFolder+"/Enemies/"+group.Key+".prefab";
    if(!AssetDatabase.LoadAssetAtPath<GameObject>(path)){
     string old="Assets/NexoterraWorld/Combat/"+group.Key+".prefab";string error=AssetDatabase.MoveAsset(old,path);if(error!="")throw new Exception(error);
     var clone=Object.Instantiate(source.gameObject);clone.name=group.Key;clone.SetActive(true);clone.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
     if(PrefabUtility.IsPartOfPrefabInstance(clone))PrefabUtility.UnpackPrefabInstance(clone,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
     PrefabUtility.SaveAsPrefabAsset(clone,path);Object.DestroyImmediate(clone);
    }
    var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
    foreach(var enemy in group){Convert(enemy.gameObject,prefab);RemoveOverrides(enemy.gameObject,new[]{"maxHealth","corpseLifetime","portrait","aggroRange","attackRange","speed","shotCooldown","respawnSeconds"});}
   }
   var players=scenes.SelectMany(s=>s.scene.GetRootGameObjects()).SelectMany(r=>r.GetComponentsInChildren<NexoterraThirdPerson>(true)).ToArray();
   string playerPath=PrefabFolder+"/Player.prefab";
   if(!AssetDatabase.LoadAssetAtPath<GameObject>(playerPath)){
    var clone=Object.Instantiate(players[0].gameObject);clone.name="Player";clone.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);var p=clone.GetComponent<NexoterraThirdPerson>();p.view=null;p.travelPoints=Array.Empty<Transform>();
    PrefabUtility.SaveAsPrefabAsset(clone,playerPath);Object.DestroyImmediate(clone);
   }
   foreach(var player in players){Convert(player.gameObject,AssetDatabase.LoadAssetAtPath<GameObject>(playerPath));RemoveOverrides(player.gameObject,new[]{"maxHealth","respawnDelay","walkSpeed","sprintSpeed","dodgeDuration","dodgeDistance","dodgeCooldown","malePortrait","femalePortrait"});}
   var catalog=players[0].GetComponent<SlugInventory>().catalog;
   foreach(var definition in catalog.species){
    foreach(var model in new[]{definition.normal,definition.transformed}.Where(m=>m).Distinct()){
     string old=AssetDatabase.GetAssetPath(model);if(!old.EndsWith(".prefab"))continue;
     if(!old.StartsWith(PrefabFolder+"/Slugs/")){string dest=AssetDatabase.GenerateUniqueAssetPath(PrefabFolder+"/Slugs/"+Path.GetFileName(old));string error=AssetDatabase.MoveAsset(old,dest);if(error!="")throw new Exception(error);}
    }
    if(!definition.normal)continue;string path=AssetDatabase.GetAssetPath(definition.normal);var root=PrefabUtility.LoadPrefabContents(path);
    try{if(!root.GetComponent<SlugPrefabStats>()){var stats=root.AddComponent<SlugPrefabStats>();stats.combat=definition.combatConfigured&&definition.combat!=null?JsonUtility.FromJson<SlugCombatProfile>(JsonUtility.ToJson(definition.combat)):SlugCombatProfile.Default(definition.id);PrefabUtility.SaveAsPrefabAsset(root,path);}}
    finally{PrefabUtility.UnloadPrefabContents(root);}
   }
   foreach(var entry in scenes){var info=entry.scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<CampaignLevel>()).FirstOrDefault();if(info&&info.index==0){info.introduction="Llegaste a Nexoterra. Recoge hongos con E. Derrota a la patrulla y al secuaz para liberar a Energía; aliméntala con F. No necesita curación. M abre el mapa.";var ai=info.guardian.GetComponent<EnemyShooter>();ai.slugSlots[0]="Elemental_Energia";PrefabUtility.RecordPrefabInstancePropertyModifications(ai);}EditorSceneManager.MarkSceneDirty(entry.scene);EditorSceneManager.SaveScene(entry.scene);}
   AssetDatabase.SaveAssets();
  }finally{foreach(var entry in scenes)if(entry.opened)EditorSceneManager.CloseScene(entry.scene,true);if(active.IsValid()&&active.isLoaded)SceneManager.SetActiveScene(active);}
 }
 static void Convert(GameObject root,GameObject prefab){if(PrefabUtility.IsPartOfPrefabInstance(root))return;PrefabUtility.ConvertToPrefabInstance(root,prefab,new ConvertToPrefabInstanceSettings{objectMatchMode=ObjectMatchMode.ByHierarchy,recordPropertyOverridesOfMatches=true,componentsNotMatchedBecomesOverride=true,gameObjectsNotMatchedBecomesOverride=true,changeRootNameToAssetName=false},InteractionMode.AutomatedAction);}
 static void RemoveOverrides(GameObject root,string[] fields){var mods=PrefabUtility.GetPropertyModifications(root);if(mods!=null)PrefabUtility.SetPropertyModifications(root,mods.Where(m=>!fields.Contains(m.propertyPath)).ToArray());}
 static void CapturePortraits(){
  string dir="Documentation/Nexoterra/Upgrade/FaceReferences";Directory.CreateDirectory(dir);
  var active=SceneManager.GetActiveScene();var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);SceneManager.SetActiveScene(scene);
  try{
   RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.65f,.65f,.65f);
   var light=new GameObject("Portrait light").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.8f;light.transform.rotation=Quaternion.Euler(35,165,0);
   var camera=new GameObject("Portrait camera").AddComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.025f,.08f,.1f);camera.orthographic=true;camera.orthographicSize=.42f;camera.nearClipPlane=.01f;camera.farClipPlane=15;
   foreach(string role in new[]{"Player_Male","Player_Female","Enemy1","Sir_Black","Enemy2","Secuaz_Sir_Black","Sir_Black_2"}){
    var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/02_Prefabs/Gameplay/Models/"+role+".prefab");if(!prefab)throw new Exception("Missing model "+role);
    var go=Object.Instantiate(prefab);go.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
    foreach(var script in go.GetComponentsInChildren<MonoBehaviour>(true))script.enabled=false;
    var a=go.GetComponentInChildren<Animator>();a.Rebind();a.Update(0);if(a.runtimeAnimatorController){a.Play("Idle",0,0);a.Update(.01f);}var head=a.GetBoneTransform(HumanBodyBones.Head);
    var bounds=go.GetComponentsInChildren<Renderer>().Select(r=>r.bounds).Aggregate((b,c)=>{b.Encapsulate(c);return b;});
    camera.orthographicSize=bounds.size.y*.23f;Vector3 target=head.position+Vector3.down*bounds.size.y*.025f;camera.transform.position=target+Vector3.forward*4;camera.transform.LookAt(target);
    var rt=new RenderTexture(768,768,24);rt.Create();var previous=RenderTexture.active;
    try{RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});RenderTexture.active=rt;var png=new Texture2D(768,768,TextureFormat.RGB24,false);png.ReadPixels(new Rect(0,0,768,768),0,0);png.Apply();File.WriteAllBytes(dir+"/"+role+".png",png.EncodeToPNG());Object.DestroyImmediate(png);}
    finally{RenderTexture.active=previous;rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(go);}
   }
  }finally{EditorSceneManager.CloseScene(scene,true);SceneManager.SetActiveScene(active);}
 }
 static void Audit(){
  var report=new StringBuilder();var active=SceneManager.GetActiveScene();
  foreach(var name in CampaignLevel.Scenes.Concat(new[]{"Nexoterra_CuatroNodos"})){
   string path=name=="Nexoterra_CuatroNodos"?"Assets/00_Scenes/"+name+".unity":"Assets/00_Scenes/Campaign/"+name+".unity";
   var scene=SceneManager.GetSceneByPath(path);bool opened=!scene.isLoaded;if(opened)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
   try{
    report.AppendLine("SCENE "+name);
    var all=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).ToArray();
    foreach(var t in all){var hp=t.GetComponent<EnemyCombatant>();var p=t.GetComponent<PlayerHealth>();if(hp||p)report.AppendLine(t.name+" hp="+(hp?hp.maxHealth:p.maxHealth)+" prefab="+PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject));}
    if(name=="Nexoterra_CuatroNodos")foreach(var t in all){var m=t.GetComponent<MeshRenderer>();if(m&&(t.name.Contains("Santuario")||t.name.Contains("santuario")||t.name.Contains("crater")||t.name.Contains("lava")||t.name.Contains("Volcan")||t.name.Contains("islote")||t.name.Contains("elemental")))report.AppendLine("LANDMARK "+t.name+" bounds="+m.bounds);}
   }finally{if(opened)EditorSceneManager.CloseScene(scene,true);}
  }
  SceneManager.SetActiveScene(active);Directory.CreateDirectory("Documentation/Nexoterra/Upgrade");File.WriteAllText("Documentation/Nexoterra/Upgrade/Audit.txt",report.ToString());
 }
}
