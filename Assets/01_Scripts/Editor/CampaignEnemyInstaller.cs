using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Nexoterra.World;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class CampaignEnemyInstaller
{
 const string Combat="Assets/02_Prefabs/Gameplay/Enemies/";
 static NativePlayerAnimation referencePlayer;
 static CampaignEnemyInstaller(){EditorApplication.update+=Poll;}
 static void Poll(){
  if(!File.Exists("Library/CampaignEnemies.request")||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
  File.Delete("Library/CampaignEnemies.request");
  try{Install();File.WriteAllText("Library/CampaignEnemies.result","PASS");}catch(Exception e){File.WriteAllText("Library/CampaignEnemies.result",e.ToString());Debug.LogException(e);}
 }
 public static void Install(){
  var active=SceneManager.GetActiveScene();var source=SceneManager.GetSceneByPath("Assets/00_Scenes/Nexoterra_CuatroNodos.unity");bool opened=!source.isLoaded;
  if(opened)source=EditorSceneManager.OpenScene("Assets/00_Scenes/Nexoterra_CuatroNodos.unity",OpenSceneMode.Additive);
  try{referencePlayer=source.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<NativePlayerAnimation>(true)).First();InstallInternal();}
  finally{referencePlayer=null;if(active.IsValid()&&active.isLoaded)SceneManager.SetActiveScene(active);if(opened)EditorSceneManager.CloseScene(source,true);}
 }
 static void InstallInternal(){
  var report=new StringBuilder();
  foreach(string role in new[]{"Enemy2","Secuaz_Sir_Black","Sir_Black_2"})CreateEnemy(role,report);
  var active=SceneManager.GetActiveScene();
  string backup="Library/CampaignEnemiesBackup/"+DateTime.Now.ToString("yyyyMMdd_HHmmss");Directory.CreateDirectory(backup);
  for(int i=0;i<CampaignLevel.Scenes.Length;i++){
   string path="Assets/00_Scenes/Campaign/"+CampaignLevel.Scenes[i]+".unity";File.Copy(path,backup+"/"+CampaignLevel.Scenes[i]+".unity");
   var scene=SceneManager.GetSceneByPath(path);bool opened=!scene.isLoaded;if(opened)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
   if(!opened&&scene.isDirty)throw new Exception("La escena tiene cambios sin guardar: "+path);
   SceneManager.SetActiveScene(scene);
   try{var info=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<CampaignLevel>(true)).Single();info.index=i;ConfigureScene(info);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);report.AppendLine(path+": "+info.enemies.Length+" enemigos, un secuaz; guardián "+info.guardian.name);}
   finally{if(opened)EditorSceneManager.CloseScene(scene,true);}
  }
  if(active.IsValid()&&active.isLoaded)SceneManager.SetActiveScene(active);CampaignWaterZones.Build();
  var paths=new List<string>{"Assets/00_Scenes/Nexoterra_Menu.unity","Assets/00_Scenes/Nexoterra_CuatroNodos.unity"};paths.AddRange(CampaignLevel.Scenes.Select(s=>"Assets/00_Scenes/Campaign/"+s+".unity"));paths.Add("Assets/00_Scenes/Campaign/Nexoterra_Arena.unity");
  EditorBuildSettings.scenes=paths.Select(p=>new EditorBuildSettingsScene(p,true)).ToArray();AssetDatabase.SaveAssets();
  Directory.CreateDirectory("Documentation/Nexoterra/Campaign");File.WriteAllText("Documentation/Nexoterra/Campaign/NewEnemies.txt",report.ToString());
 }
 static void CreateEnemy(string role,StringBuilder report){
  if(AssetDatabase.LoadAssetAtPath<GameObject>(Combat+role+".prefab"))return;
  string dir="Assets/Characters/Rigged/"+role;string source=Directory.GetFiles(dir,"*.fbx").Single().Replace('\\','/');
  foreach(string file in Directory.GetFiles(dir,"*",SearchOption.AllDirectories).Where(f=>f.EndsWith(".png",StringComparison.OrdinalIgnoreCase))){
   string p=file.Replace('\\','/');var texture=(TextureImporter)AssetImporter.GetAtPath(p);var type=p.Contains("normal")?TextureImporterType.NormalMap:TextureImporterType.Default;bool srgb=p.Contains("basecolor");if(texture.textureType!=type||texture.sRGBTexture!=srgb||texture.maxTextureSize!=2048){texture.textureType=type;texture.sRGBTexture=srgb;texture.maxTextureSize=2048;texture.SaveAndReimport();}
  }
  var importer=(ModelImporter)AssetImporter.GetAtPath(source);importer.animationType=ModelImporterAnimationType.Human;importer.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;importer.importAnimation=false;importer.importCameras=false;importer.importLights=false;importer.materialImportMode=ModelImporterMaterialImportMode.None;importer.SaveAndReimport();
  var avatar=AssetDatabase.LoadAllAssetsAtPath(source).OfType<Avatar>().FirstOrDefault();if(!avatar||!avatar.isHuman||!avatar.isValid)throw new Exception(role+": avatar humanoide inválido");
  bool boss=role=="Sir_Black_2";var root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Combat+(boss?"SirBlack":"Enemy1")+".prefab"));root.name=role;
  try{
   var ai=root.GetComponent<EnemyShooter>();var driver=root.GetComponent<NativeEnemyAnimation>();var health=root.GetComponent<EnemyCombatant>();var controller=driver.animator.runtimeAnimatorController;
   var playerWeapon=referencePlayer;if(!playerWeapon||!playerWeapon.maleMuzzle)throw new Exception("Falta el arma del jugador para replicar su agarre");
   ai.weapon.SetParent(root.transform,true);Object.DestroyImmediate(ai.weapon.gameObject);Object.DestroyImmediate(ai.model.gameObject);
   ai.weapon=Object.Instantiate(playerWeapon.maleMuzzle.parent.gameObject).transform;ai.weapon.SetParent(root.transform,false);ai.weapon.localScale=playerWeapon.maleMuzzle.parent.lossyScale;
   var model=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(source));model.name=role+" · modelo";model.transform.SetParent(health.visualRoot,false);
   var rs=model.GetComponentsInChildren<Renderer>();if(rs.Length==0)throw new Exception(role+": no tiene mallas");var bounds=rs[0].bounds;foreach(var r in rs)bounds.Encapsulate(r.bounds);
   float height=boss?2.2f:role=="Secuaz_Sir_Black"?2.05f:1.9f;float scale=height/bounds.size.y;model.transform.localScale*=scale;model.transform.localPosition-=health.visualRoot.InverseTransformVector(new Vector3(bounds.center.x-root.transform.position.x,bounds.min.y-root.transform.position.y,bounds.center.z-root.transform.position.z))*scale;
   string matPath="Assets/02_Prefabs/Gameplay/Models/"+role+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(matPath);if(!mat){mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(mat,matPath);}
   string FindTexture(string suffix)=>Directory.GetFiles(dir,"*",SearchOption.AllDirectories).First(p=>p.EndsWith(suffix+".PNG",StringComparison.OrdinalIgnoreCase)).Replace('\\','/');
   mat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(FindTexture("basecolor")));mat.SetColor("_BaseColor",Color.white);mat.SetFloat("_Smoothness",.25f);mat.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(FindTexture("normal")));mat.EnableKeyword("_NORMALMAP");EditorUtility.SetDirty(mat);
   foreach(var r in rs)r.sharedMaterials=Enumerable.Repeat(mat,r.sharedMaterials.Length).ToArray();
   foreach(var t in model.GetComponentsInChildren<Transform>(true))t.gameObject.layer=2;
   foreach(var c in model.GetComponentsInChildren<Collider>(true))Object.DestroyImmediate(c);
   var animator=model.GetComponentInChildren<Animator>();animator.avatar=avatar;animator.enabled=true;animator.runtimeAnimatorController=controller;animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
   var ik=animator.GetComponent<HumanoidWeaponIK>();if(!ik)ik=animator.gameObject.AddComponent<HumanoidWeaponIK>();ik.weaponHandIK=false;
   driver.animator=animator;Calibrate(animator,out driver.grip,out driver.gripRotation);driver.copyPlayerGrip=false;
   ai.model=model.transform;ai.pose=null;ai.boss=boss;ai.campaignMember=true;ai.dropSlugsOnDeath=true;ai.slugSlots=Array.Empty<string>();ai.equippedElemental="";
   health.maxHealth=boss?750:role=="Secuaz_Sir_Black"?220:180;health.displayName=role.Replace('_',' ');
   model.AddComponent<RiggedCharacterModel>().role=role;
   var character=Object.Instantiate(model);character.transform.SetParent(null,false);PrefabUtility.SaveAsPrefabAsset(character,"Assets/02_Prefabs/Gameplay/Models/"+role+".prefab");Object.DestroyImmediate(character);
   PrefabUtility.SaveAsPrefabAsset(root,Combat+role+".prefab");report.AppendLine(role+": humanoide válido, controlador "+controller.name+", altura "+height+", vida "+health.maxHealth);
  }finally{Object.DestroyImmediate(root);}
 }
 static void Calibrate(Animator source,out Vector3 grip,out Quaternion rotation){
  var go=Object.Instantiate(source.gameObject);go.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);go.transform.localScale=source.transform.lossyScale;
  foreach(var script in go.GetComponentsInChildren<MonoBehaviour>())script.enabled=false;
  try{
   void Pose(Animator a){a.Rebind();a.Update(0);a.Play("Idle",0,0);int layer=a.GetLayerIndex("AimUpperBody");if(layer>=0){a.SetLayerWeight(layer,1);a.Play("AimPose",layer,.25f);}a.Update(.01f);}
   var a=go.GetComponent<Animator>();Pose(a);var hand=a.GetBoneTransform(HumanBodyBones.RightHand);
   var player=referencePlayer;if(!player||!player.maleMuzzle)throw new Exception("Falta el jugador de referencia para calibrar el arma.");
   string path=AnimationUtility.CalculateTransformPath(player.maleMuzzle.parent,player.male.transform);
   var reference=Object.Instantiate(player.male.gameObject);reference.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);reference.transform.localScale=player.male.transform.lossyScale;
   try{foreach(var script in reference.GetComponentsInChildren<MonoBehaviour>())script.enabled=false;var original=reference.GetComponent<Animator>();Pose(original);var weapon=reference.transform.Find(path);if(!weapon)throw new Exception("No se encontró el arma del jugador en su mano");var originalHand=original.GetBoneTransform(HumanBodyBones.RightHand);grip=hand.InverseTransformVector(weapon.position-originalHand.position);rotation=Quaternion.Inverse(hand.rotation)*weapon.rotation;}
   finally{Object.DestroyImmediate(reference);}
  }
  finally{Object.DestroyImmediate(go);}
 }
 public static void ConfigureScene(CampaignLevel info){
  int level=info.index;var scene=info.gameObject.scene;
  var previous=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<EnemyShooter>(true)).ToArray();foreach(var e in previous)Object.DestroyImmediate(e.gameObject);
  var pool=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<SlugSpawnPoint>(true)).Where(s=>s.localSpecies!=null).SelectMany(s=>s.localSpecies).Select(s=>SlugCatalog.BaseSpecies(s.id)).Where(id=>!id.StartsWith("Elemental_")&&info.catalog.Find("Malvada_"+id)!=null).Distinct().ToArray();
  if(pool.Length==0)throw new Exception("Bioma sin especies malvadas configuradas: "+scene.name);
  string[] Slots(int seed,string elemental=null){var ids=Enumerable.Range(0,3).Select(i=>"Malvada_"+pool[(seed+i)%pool.Length]).ToArray();if(!string.IsNullOrEmpty(elemental))ids[0]=elemental=="Elemental_Energia"?elemental:"Malvada_"+elemental;return ids;}
  EnemyCombatant Spawn(string role,string name,Vector3 position,float hp,int seed,string elemental=null){
   var root=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Combat+role+".prefab"));root.name=name;root.transform.position=position;
   var ai=root.GetComponent<EnemyShooter>();ai.campaignMember=true;ai.dropSlugsOnDeath=true;ai.catalog=info.catalog;ai.equippedElemental=elemental??"";ai.slugSlots=Slots(seed,elemental);
   var h=root.GetComponent<EnemyCombatant>();h.displayName=name;return h;
  }
  var foes=new List<EnemyCombatant>();Vector3[] positions={new Vector3(-55,.1f,-50),new Vector3(35,.1f,-65),new Vector3(82,.1f,-20),new Vector3(-90,.1f,25),new Vector3(8,.1f,28),new Vector3(-50,.1f,78),new Vector3(75,.1f,35)};
  foreach(var p in positions)foes.Add(Spawn(level==5?"Enemy2":"Enemy1",(level==5?"Enemy2":"Enemy1")+" · "+(foes.Count+1),p,level==5?180:160,foes.Count));
  var minion=Spawn("Secuaz_Sir_Black","Secuaz de Sir Black",new Vector3(66,.1f,88),220,1,level<4?CampaignLevel.Rewards[level]:null);foes.Add(minion);info.guardian=minion;info.trapGuard=info.ambushBoss=null;
  if(level>=4){var boss=Spawn(level==4?"SirBlack":"Sir_Black_2",level==4?"Sir Black 1":"Sir Black 2",new Vector3(90,.1f,103),level==4?500:750,2,level==4?CampaignLevel.Rewards[level]:null);foes.Add(boss);info.guardian=boss;if(level==4){info.trapGuard=minion;info.ambushBoss=boss;boss.gameObject.SetActive(false);}}
  info.enemies=foes.ToArray();info.rewardPoint.position=info.guardian.transform.position;
  info.introduction=level==0?"Llegaste a Nexoterra. Recoge hongos con E y alimenta a Doc con F. El secuaz guarda la Energía: derrótalo y alimenta a Energía con F; no necesita curación. M abre el mapa.":level==4?"El secuaz asegura que lleva la elemental de fuego. Derrótalo para descubrir qué oculta.":level==5?"El agua oscura alimenta al ejército de Enemy2. Derrota al único secuaz y a Sir Black 2 para liberar Nexoterra.":"El secuaz lleva la elemental de este bioma. Vence a su patrulla, recupera la elemental malvada, cúrala y busca el túnel.";
 }
}


