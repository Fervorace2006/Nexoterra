using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Nexoterra.World;
namespace Nexoterra.Editor
{
 [InitializeOnLoad] public static class NexoterraRiggedModels
 {
  public const string Folder="Assets/NexoterraWorld/Characters";
  static readonly string[] Roles={"Player_Male","Player_Female","Enemy1","Sir_Black"};
  static NexoterraRiggedModels(){EditorApplication.update+=Poll;}
  static void Poll(){if(EditorApplication.isCompiling||EditorApplication.isUpdating||!File.Exists("Library/NexoterraModels.request"))return;if(EditorApplication.isPlayingOrWillChangePlaymode){EditorApplication.isPlaying=false;return;}File.Delete("Library/NexoterraModels.request");try{Build();}catch(Exception e){File.WriteAllText("Library/NexoterraModels.result.txt","FAIL\n"+e);Debug.LogException(e);}}
  public static void BatchBuild(){EditorSceneManager.OpenScene(NexoterraBiomeBuilder.ScenePath);Build();}
  [MenuItem("Nexoterra/Personajes/Instalar modelos con esqueleto importado")]
  public static void Build()
  {
   if(EditorApplication.isPlaying)throw new Exception("Salir de Play antes de cambiar los modelos.");
   var player=UnityEngine.Object.FindAnyObjectByType<NexoterraThirdPerson>();if(!player||player.gameObject.scene.path!=NexoterraBiomeBuilder.ScenePath)throw new Exception("Abrir CuatroNodos.");
   EditorSceneManager.SaveScene(player.gameObject.scene);File.Copy(player.gameObject.scene.path,"Library/AntesModelosRigged_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".unity",true);
   Directory.CreateDirectory(Folder);AssetDatabase.Refresh();var report=new StringBuilder();
   foreach(string file in Directory.GetFiles("Assets/Characters/Rigged","*",SearchOption.AllDirectories).Where(p=>p.EndsWith(".png",StringComparison.OrdinalIgnoreCase))){var path=file.Replace('\\','/');var ti=(TextureImporter)AssetImporter.GetAtPath(path);ti.textureShape=TextureImporterShape.Texture2D;ti.textureType=path.Contains("normal")?TextureImporterType.NormalMap:TextureImporterType.Default;ti.sRGBTexture=path.Contains("basecolor");ti.SaveAndReimport();}
   foreach(string role in Roles)CreateModel(role,report);
   ReplaceEnemy("Enemy1","Enemy1");ReplaceEnemy("SirBlack","Sir_Black");
   bool female=player.female.activeSelf;var oldMale=player.male;var oldFemale=player.female;
   player.male=Instance("Player_Male",player.visuals);player.female=Instance("Player_Female",player.visuals);player.male.SetActive(!female);player.female.SetActive(female);
   SetLayer(player.male,2);SetLayer(player.female,2);UnityEngine.Object.DestroyImmediate(oldMale);UnityEngine.Object.DestroyImmediate(oldFemale);
   var driver=player.GetComponent<PlayerAnimationDriver>();if(driver)UnityEngine.Object.DestroyImmediate(driver);player.visuals.name="Personajes · esqueletos importados";
   var companion=GameObject.Find("Comando · companera en el refugio");if(companion){var next=Instance("Player_Female",companion.transform.parent);next.name=companion.name;next.transform.SetPositionAndRotation(companion.transform.position,companion.transform.rotation);UnityEngine.Object.DestroyImmediate(companion);}
   // Replace loose source prefabs too, preserving their GUIDs for existing references.
   SaveAlias("Enemy1","Assets/02_Prefabs/Enemy1.prefab");SaveAlias("Sir_Black","Assets/02_Prefabs/Boss_Sir_Black.prefab");
   var hud=player.GetComponent<NexoHud>();if(hud){hud.malePortrait=NexoterraInterfaceSetup.Portrait(player.male,"Militar");hud.femalePortrait=NexoterraInterfaceSetup.Portrait(player.female,"Comando");}
   AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(player.gameObject.scene);EditorSceneManager.SaveScene(player.gameObject.scene);
   if(UnityEngine.Object.FindObjectsByType<HumanoidPose>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length!=0)throw new Exception("Quedan rigs provisionales en la escena.");
   if(UnityEngine.Object.FindObjectsByType<PlayerAnimationDriver>().Length!=0)throw new Exception("Queda el animador provisional.");
   foreach(var skin in player.visuals.GetComponentsInChildren<SkinnedMeshRenderer>(true))CheckSkin(skin);
   Capture();report.AppendLine("PASS: modelos reemplazados, esqueletos originales conservados, sin animaciones provisionales.");Directory.CreateDirectory("Documentation/Nexoterra/ModelosRigged");File.WriteAllText("Documentation/Nexoterra/ModelosRigged/Validacion.txt",report.ToString());File.WriteAllText("Library/NexoterraModels.result.txt",report.ToString());
  }
  static void CheckSkin(SkinnedMeshRenderer skin){if(!skin.sharedMesh||skin.bones.Length==0||skin.bones.Any(b=>!b)||skin.sharedMesh.bindposes.Length!=skin.bones.Length)throw new Exception("Esqueleto inválido: "+skin.name);}
  static void CreateModel(string role,StringBuilder report)
  {
   string source=Directory.GetFiles("Assets/Characters/Rigged/"+role,"*.fbx").Single().Replace('\\','/');var importer=(ModelImporter)AssetImporter.GetAtPath(source);importer.animationType=ModelImporterAnimationType.Generic;importer.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;importer.importAnimation=false;importer.SaveAndReimport();
   var holder=new GameObject(role);var model=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(source));model.transform.SetParent(holder.transform,false);
   var skins=model.GetComponentsInChildren<SkinnedMeshRenderer>(true);if(skins.Length==0)throw new Exception(role+" no tiene malla con esqueleto");foreach(var skin in skins)CheckSkin(skin);
   foreach(var a in model.GetComponentsInChildren<Animator>(true)){a.runtimeAnimatorController=null;a.enabled=false;PrefabUtility.RecordPrefabInstancePropertyModifications(a);}
   var renderers=model.GetComponentsInChildren<Renderer>(true);Bounds bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
   float height=role=="Sir_Black"?2.2f:role=="Player_Female"?1.85f:1.9f;
   if(bounds.size.y<.001f)throw new Exception("Altura inválida "+role);float factor=height/bounds.size.y;model.transform.localScale*=factor;model.transform.localPosition-=new Vector3(bounds.center.x,bounds.min.y,bounds.center.z)*factor;PrefabUtility.RecordPrefabInstancePropertyModifications(model.transform);
   string texture=Directory.GetFiles("Assets/Characters/Rigged/"+role,"*basecolor*",SearchOption.AllDirectories).First(p=>!p.EndsWith(".meta")).Replace('\\','/');string materialPath=Folder+"/"+role+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);if(!material){material=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(material,materialPath);}var baseMap=AssetDatabase.LoadAssetAtPath<Texture2D>(texture);if(!baseMap)throw new Exception("Textura 2D ausente: "+texture);material.SetTexture("_BaseMap",baseMap);material.SetColor("_BaseColor",Color.white);material.SetFloat("_Smoothness",.22f);
   string normal=Directory.GetFiles("Assets/Characters/Rigged/"+role,"*normal*",SearchOption.AllDirectories).FirstOrDefault(p=>!p.EndsWith(".meta"));if(normal!=null){normal=normal.Replace('\\','/');var ti=(TextureImporter)AssetImporter.GetAtPath(normal);ti.textureType=TextureImporterType.NormalMap;material.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(normal));material.EnableKeyword("_NORMALMAP");}
   foreach(var r in renderers){r.sharedMaterials=Enumerable.Repeat(material,r.sharedMaterials.Length).ToArray();PrefabUtility.RecordPrefabInstancePropertyModifications(r);}
   holder.AddComponent<RiggedCharacterModel>().role=role;PrefabUtility.SaveAsPrefabAsset(holder,Folder+"/"+role+".prefab");
   report.AppendLine(role+": "+skins.Length+" mallas, "+skins.Sum(s=>s.bones.Length)+" huesos asignados; altura "+height+" m; animación desactivada.");report.AppendLine("Huesos: "+string.Join(", ",skins[0].bones.Select(b=>b.name)));
   UnityEngine.Object.DestroyImmediate(holder);
  }
  static GameObject Instance(string role,Transform parent){var go=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/"+role+".prefab"));go.transform.SetParent(parent,false);return go;}
  static void SetLayer(GameObject go,int layer){foreach(var t in go.GetComponentsInChildren<Transform>(true))t.gameObject.layer=layer;}
  static void SaveAlias(string role,string path){var go=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/"+role+".prefab"));PrefabUtility.SaveAsPrefabAsset(go,path);UnityEngine.Object.DestroyImmediate(go);}
  static void ReplaceEnemy(string prefabName,string role)
  {
   string path="Assets/02_Prefabs/Gameplay/Enemies/"+prefabName+".prefab";var root=PrefabUtility.LoadPrefabContents(path);
   try{var shooter=root.GetComponent<EnemyShooter>();var visual=root.GetComponent<EnemyCombatant>().visualRoot;foreach(Transform child in visual.Cast<Transform>().ToArray())if(child!=shooter.weapon)UnityEngine.Object.DestroyImmediate(child.gameObject);var model=Instance(role,visual);shooter.pose=null;shooter.model=model.transform;PrefabUtility.SaveAsPrefabAsset(root,path);}finally{PrefabUtility.UnloadPrefabContents(root);}
  }
  static void Capture()
  {
   Directory.CreateDirectory("Documentation/Nexoterra/ModelosRigged");
   foreach(string role in Roles){var go=Instance(role,null);go.transform.position=new Vector3(0,2000,0);SetLayer(go,31);foreach(var skin in go.GetComponentsInChildren<SkinnedMeshRenderer>()){var mesh=new Mesh();skin.BakeMesh(mesh,false);var baked=new GameObject("Capture skin");baked.layer=31;baked.transform.SetParent(go.transform,false);baked.transform.SetPositionAndRotation(skin.transform.position,skin.transform.rotation);baked.AddComponent<MeshFilter>().sharedMesh=mesh;baked.AddComponent<MeshRenderer>().sharedMaterials=skin.sharedMaterials;skin.enabled=false;}var camGo=new GameObject("Revision modelo");var cam=camGo.AddComponent<Camera>();cam.cullingMask=1<<31;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.06f,.08f,.1f);cam.fieldOfView=35;cam.transform.position=go.transform.position+new Vector3(2,1.5f,4.5f);cam.transform.LookAt(go.transform.position+Vector3.up*1.1f);var lightGo=new GameObject("Revision luz");var light=lightGo.AddComponent<Light>();light.type=LightType.Directional;light.intensity=2;light.transform.rotation=Quaternion.Euler(30,180,0);light.cullingMask=1<<31;var rt=new RenderTexture(800,800,24);cam.targetTexture=rt;cam.Render();var previous=RenderTexture.active;RenderTexture.active=rt;var png=new Texture2D(800,800,TextureFormat.RGB24,false);png.ReadPixels(new Rect(0,0,800,800),0,0);png.Apply();File.WriteAllBytes("Documentation/Nexoterra/ModelosRigged/"+role+".png",png.EncodeToPNG());RenderTexture.active=previous;cam.targetTexture=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(png);UnityEngine.Object.DestroyImmediate(camGo);UnityEngine.Object.DestroyImmediate(lightGo);UnityEngine.Object.DestroyImmediate(go);}
  }
 }
}
