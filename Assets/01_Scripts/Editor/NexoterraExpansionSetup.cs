using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Globalization;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using Nexoterra.World;
namespace Nexoterra.Editor
{
 [InitializeOnLoad] public static class NexoterraExpansionSetup
 {
  const string Folder="Assets/NexoterraWorld/Expansion";
  static NexoterraExpansionSetup(){EditorApplication.update+=Poll;}
  static void Poll(){if(EditorApplication.isCompiling||EditorApplication.isUpdating||!File.Exists("Library/NexoterraExpansion.request"))return;if(EditorApplication.isPlayingOrWillChangePlaymode){EditorApplication.isPlaying=false;return;}File.Delete("Library/NexoterraExpansion.request");try{Build();}catch(Exception e){File.WriteAllText("Library/NexoterraExpansion.result.txt","FAIL\n"+e);Debug.LogException(e);}}
  public static void BatchBuild(){EditorSceneManager.OpenScene(NexoterraBiomeBuilder.ScenePath);Build();}
  public static void Build()
  {
   var p=UnityEngine.Object.FindAnyObjectByType<NexoterraThirdPerson>();if(!p||EditorApplication.isPlaying)throw new Exception("Abrir escena CuatroNodos fuera de Play");
   EditorSceneManager.SaveScene(p.gameObject.scene);File.Copy(p.gameObject.scene.path,"Library/AntesExpansion_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".unity",true);
   Directory.CreateDirectory(Folder);AssetDatabase.Refresh();var report=new StringBuilder();
   InstallAnimations(p,report);InstallSlugs(report);InstallBoss(report);ExportMixamo("Enemy1");ExportMixamo("Sir_Black");
   AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(p.gameObject.scene);EditorSceneManager.SaveScene(p.gameObject.scene);
   File.WriteAllText("Library/NexoterraExpansion.result.txt","PASS\n"+report);Directory.CreateDirectory("Documentation/Nexoterra/Expansion");File.WriteAllText("Documentation/Nexoterra/Expansion/Instalacion.txt",report.ToString());
  }
  static Avatar Human(string path)
  {
   var importer=(ModelImporter)AssetImporter.GetAtPath(path);importer.animationType=ModelImporterAnimationType.Human;importer.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;importer.SaveAndReimport();
   var avatar=AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();if(!avatar||!avatar.isValid||!avatar.isHuman)throw new Exception("Avatar humano inválido: "+path);return avatar;
  }
  static AnimationClip Clip(string name,bool loop,StringBuilder report)
  {
   string path="Assets/Characters/Animations/"+name+".fbx";Human(path);var importer=(ModelImporter)AssetImporter.GetAtPath(path);importer.importAnimation=true;
   var clips=importer.defaultClipAnimations;foreach(var clip in clips){clip.loopTime=loop;clip.loopPose=loop;clip.lockRootRotation=true;clip.lockRootHeightY=true;clip.lockRootPositionXZ=true;clip.keepOriginalOrientation=true;clip.keepOriginalPositionY=true;clip.keepOriginalPositionXZ=true;}importer.clipAnimations=clips;importer.SaveAndReimport();
   var animation=AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().First(c=>!c.name.StartsWith("__preview"));report.AppendLine(name+": "+animation.length+" s; humanoid="+animation.humanMotion+"; desplazamiento horneado al cuerpo.");return animation;
  }
  static void InstallAnimations(NexoterraThirdPerson p,StringBuilder report)
  {
   var run=Clip("Pistol_Run",true,report);var left=Clip("voltereta",false,report);var right=Clip("voltereta2",false,report);var death=Clip("Death",false,report);
   var idle=UnityEngine.Object.Instantiate(run);idle.name="Idle_Pistol";foreach(var binding in AnimationUtility.GetCurveBindings(idle)){var curve=AnimationUtility.GetEditorCurve(idle,binding);if(curve!=null&&curve.length>0)AnimationUtility.SetEditorCurve(idle,binding,AnimationCurve.Constant(0,1,curve.Evaluate(0)));}
   string idlePath=Folder+"/Idle_Pistol.anim";var oldIdle=AssetDatabase.LoadAssetAtPath<AnimationClip>(idlePath);if(oldIdle){EditorUtility.CopySerialized(idle,oldIdle);UnityEngine.Object.DestroyImmediate(idle);idle=oldIdle;}else AssetDatabase.CreateAsset(idle,idlePath);
   string controllerPath=Folder+"/Player.controller";var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);if(!controller)controller=AnimatorController.CreateAnimatorControllerAtPath(controllerPath);var machine=controller.layers[0].stateMachine;foreach(var state in machine.states)machine.RemoveState(state.state);
   var names=new[]{"Idle","Run","RollLeft","RollRight","Death"};var motions=new[]{idle,run,left,right,death};for(int i=0;i<names.Length;i++){var state=machine.AddState(names[i]);state.motion=motions[i];if(i==0)machine.defaultState=state;}
   foreach(string role in new[]{"Player_Male","Player_Female"})
   {
    var avatar=Human(Directory.GetFiles("Assets/Characters/Rigged/"+role,"*.fbx").Single().Replace('\\','/'));string path=NexoterraRiggedModels.Folder+"/"+role+".prefab";var prefab=PrefabUtility.LoadPrefabContents(path);try{var animator=prefab.GetComponentInChildren<Animator>(true);animator.avatar=avatar;animator.runtimeAnimatorController=controller;animator.applyRootMotion=false;animator.enabled=true;PrefabUtility.RecordPrefabInstancePropertyModifications(animator);PrefabUtility.SaveAsPrefabAsset(prefab,path);}finally{PrefabUtility.UnloadPrefabContents(prefab);}
   }
   var abilities=p.GetComponent<PlayerAbilities>();if(!abilities)abilities=p.gameObject.AddComponent<PlayerAbilities>();abilities.dodgeDuration=PlayerAbilities.QuickRollDuration;abilities.dodgeDistance=4.5f;p.GetComponent<PlayerHealth>().respawnDelay=death.length+.15f;
   var driver=p.GetComponent<NativePlayerAnimation>();if(!driver)driver=p.gameObject.AddComponent<NativePlayerAnimation>();driver.male=p.male.GetComponentInChildren<Animator>(true);driver.female=p.female.GetComponentInChildren<Animator>(true);driver.rollClipDuration=left.length;driver.weapon=p.muzzle.parent;
   Calibrate("Player_Male",out driver.maleGrip,out driver.maleGripRotation);Calibrate("Player_Female",out driver.femaleGrip,out driver.femaleGripRotation);
   // NPC has no gameplay controller and remains at rest until its own animation is requested.
   var companion=GameObject.Find("Comando · companera en el refugio");if(companion){var a=companion.GetComponentInChildren<Animator>(true);if(a)a.enabled=false;}
  }
  static void Calibrate(string role,out Vector3 position,out Quaternion rotation)
  {
   var go=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(NexoterraRiggedModels.Folder+"/"+role+".prefab"));go.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);var animator=go.GetComponentInChildren<Animator>();animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;animator.Rebind();animator.Update(0);animator.Play("Run",0,0);animator.Update(0);var hand=animator.GetBoneTransform(HumanBodyBones.RightHand);rotation=Quaternion.Inverse(hand.rotation);position=hand.InverseTransformVector(-new Vector3(0,.085f,-.08f));UnityEngine.Object.DestroyImmediate(go);
  }
  static GameObject Visual(string id,string source,bool turned,bool transformed)
  {
   var holder=new GameObject(id);var model=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(source));model.transform.SetParent(holder.transform,false);model.transform.localPosition=Vector3.zero;model.transform.localScale=Vector3.one;model.transform.localRotation=turned?Quaternion.Euler(-90,0,0):Quaternion.identity;
   foreach(var c in model.GetComponentsInChildren<Collider>())UnityEngine.Object.DestroyImmediate(c);var rs=model.GetComponentsInChildren<Renderer>();var b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);float scale=1/Mathf.Max(.01f,transformed?Mathf.Max(b.size.x,b.size.y,b.size.z):b.size.y);model.transform.localScale=Vector3.one*scale;model.transform.localPosition=-b.center*scale;
   if(id.StartsWith("Elemental_Energia"))Guardian(holder.transform,transformed);
   foreach(var t in holder.GetComponentsInChildren<Transform>())t.gameObject.layer=2;
   var prefab=PrefabUtility.SaveAsPrefabAsset(holder,Folder+"/"+id+".prefab");UnityEngine.Object.DestroyImmediate(holder);return prefab;
  }
  static void Guardian(Transform root,bool transformed)
  {
   string path=Folder+"/Guardian.mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);if(!material){material=new Material(Shader.Find("Universal Render Pipeline/Unlit"));material.SetColor("_BaseColor",new Color(.65f,1,.85f));AssetDatabase.CreateAsset(material,path);}
   var glyph=new GameObject("Marca de guardiana").AddComponent<LineRenderer>();glyph.transform.SetParent(root,false);glyph.useWorldSpace=false;glyph.loop=true;glyph.positionCount=4;glyph.startWidth=glyph.endWidth=.014f;glyph.sharedMaterial=material;float y=transformed?.15f:.32f,z=transformed?.25f:.29f;glyph.SetPositions(new[]{new Vector3(0,y+.065f,z),new Vector3(.045f,y,z),new Vector3(0,y-.065f,z),new Vector3(-.045f,y,z)});
   var orb=GameObject.CreatePrimitive(PrimitiveType.Sphere);orb.name="Luz de energía";orb.transform.SetParent(root,false);orb.transform.localPosition=new Vector3(-.12f,transformed?.27f:.57f,.03f);orb.transform.localScale=Vector3.one*.065f;UnityEngine.Object.DestroyImmediate(orb.GetComponent<Collider>());orb.GetComponent<Renderer>().sharedMaterial=material;var light=orb.AddComponent<Light>();light.color=new Color(.5f,1,.75f);light.intensity=.8f;light.range=.8f;light.shadows=LightShadows.None;
  }
  static GameObject Creature(SlugDefinition definition)
  {
   var root=new GameObject(definition.id);var model=UnityEngine.Object.Instantiate(definition.normal);model.transform.SetParent(root.transform,false);model.transform.localScale=Vector3.one*.65f;model.transform.localPosition=Vector3.up*.325f;root.AddComponent<WildSlug>().species=definition.id;
   var prefab=PrefabUtility.SaveAsPrefabAsset(root,Folder+"/Wild_"+definition.id+".prefab");UnityEngine.Object.DestroyImmediate(root);return prefab;
  }
  static void InstallSlugs(StringBuilder report)
  {
   var catalog=UnityEngine.Object.FindAnyObjectByType<SlugInventory>().catalog;var list=catalog.species.ToList();
   foreach(string id in new[]{"Tornado","Elemental_Energia"})
   {
    var definition=catalog.Find(id)??new SlugDefinition{id=id,displayName=id=="Tornado"?"Tornado":"E. energía",unique=id.StartsWith("Elemental")};
    definition.normal=Visual(id,"Assets/TripoModels/"+id+"/"+id+".fbx",true,false);
    definition.transformed=id=="Tornado"?Visual(id+"_T","Assets/TripoModels/Tornado_T/Tornado_T.fbx",true,true):Visual(id+"_T",AssetDatabase.GetAssetPath(catalog.Find("Gon_Doc").transformed),false,true);
    definition.portrait=NexoterraSlugsSetup.Portrait(definition);definition.combat=SlugCombatProfile.Default(id);definition.combatConfigured=true;if(!list.Contains(definition))list.Add(definition);
   }
   catalog.species=list.ToArray();var doc=catalog.Find("Gon_Doc");doc.combat=SlugCombatProfile.Default("Gon_Doc");doc.combatConfigured=true;EditorUtility.SetDirty(catalog);
   var energy=catalog.Find("Elemental_Energia");var tornado=catalog.Find("Tornado");var energyPrefab=Creature(energy);var tornadoPrefab=Creature(tornado);
   var points=UnityEngine.Object.FindObjectsByType<SlugSpawnPoint>();var initial=points.FirstOrDefault(s=>s.species=="Elemental_Energia")??points.First(s=>s.species=="Gon_Doc"&&s.transform.position.x>0&&s.transform.position.z< -80);
   SetSpawn(initial,energy,energyPrefab);
   foreach(var point in points.Where(s=>s.species=="Armashelt"&&(s.transform.position.z> -80&&s.transform.position.z<80)))SetSpawn(point,tornado,tornadoPrefab);
   var docs=UnityEngine.Object.FindObjectsByType<SlugSpawnPoint>().Where(s=>s.species=="Gon_Doc").ToArray();foreach(var spawn in docs)spawn.population=1;
   if(docs.Length!=3)throw new Exception("Se esperaban tres spawns Doc; hay "+docs.Length);
   if(UnityEngine.Object.FindObjectsByType<SlugSpawnPoint>().Count(s=>s.species=="Elemental_Energia")!=1)throw new Exception("Elemental energía duplicada");report.AppendLine("13 especies; 3 Doc; 1 elemental energía única en inicio; Tornado en hielo/agua.");
  }
  static void SetSpawn(SlugSpawnPoint point,SlugDefinition definition,GameObject creature){if(point.editorPreview)UnityEngine.Object.DestroyImmediate(point.editorPreview.gameObject);point.species=definition.id;point.unique=definition.unique;point.population=1;point.creaturePrefab=creature;var preview=UnityEngine.Object.Instantiate(definition.normal,point.transform);preview.name="Vista previa · se sustituye al jugar";preview.transform.localScale=Vector3.one*.65f;preview.transform.localPosition=Vector3.up*.325f;point.editorPreview=preview.transform;}
  static void InstallBoss(StringBuilder report)
  {
   var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/02_Prefabs/Gameplay/Enemies/SirBlack.prefab");var root=UnityEngine.Object.Instantiate(source);root.name="SirBlack_Final";var ai=root.GetComponent<EnemyShooter>();var health=root.GetComponent<EnemyCombatant>();var visual=health.visualRoot;
   foreach(Transform t in visual.Cast<Transform>().ToArray())if(t!=ai.weapon)UnityEngine.Object.DestroyImmediate(t.gameObject);
   var model=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/TripoModels/Sir_Black_Final/Sir_Black_Final.fbx"));model.transform.SetParent(visual,false);model.transform.localRotation=Quaternion.Euler(-90,0,0);var rs=model.GetComponentsInChildren<Renderer>();var bounds=rs[0].bounds;foreach(var r in rs)bounds.Encapsulate(r.bounds);float scale=2.4f/bounds.size.y;model.transform.localScale=Vector3.one*scale;model.transform.localPosition=new Vector3(-bounds.center.x,-bounds.min.y,-bounds.center.z)*scale;ai.model=model.transform;ai.pose=null;ai.shotCooldown=1.9f;health.displayName="Sir Black · fase final";health.maxHealth=600;
   var prefab=PrefabUtility.SaveAsPrefabAsset(root,Folder+"/SirBlack_Final.prefab");UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.FindAnyObjectByType<BossEncounters>().finalBossPrefab=prefab;report.AppendLine("Último encuentro: Sir Black y fase final de 600 PV; derrotarla completa la historia.");
  }
  static string F(float value)=>value.ToString("R",CultureInfo.InvariantCulture);
  static void ExportMixamo(string role)
  {
   string folder="Documentation/Nexoterra/Mixamo/"+role;Directory.CreateDirectory(folder);var go=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(NexoterraRiggedModels.Folder+"/"+role+".prefab"));go.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);var skin=go.GetComponentInChildren<SkinnedMeshRenderer>();var mesh=new Mesh();skin.BakeMesh(mesh,false);var text=new StringBuilder("mtllib "+role+".mtl\no "+role+"\n");
   foreach(var v in mesh.vertices){var p=skin.transform.position+skin.transform.rotation*v;text.AppendLine("v "+F(p.x)+" "+F(p.y)+" "+F(p.z));}foreach(var uv in mesh.uv)text.AppendLine("vt "+F(uv.x)+" "+F(uv.y));foreach(var n in mesh.normals){var normal=skin.transform.rotation*n;text.AppendLine("vn "+F(normal.x)+" "+F(normal.y)+" "+F(normal.z));}text.AppendLine("usemtl Character");var triangles=mesh.triangles;for(int i=0;i<triangles.Length;i+=3){text.Append("f");for(int k=0;k<3;k++){int index=triangles[i+k]+1;text.Append(" "+index+"/"+index+"/"+index);}text.AppendLine();}File.WriteAllText(folder+"/"+role+".obj",text.ToString());File.WriteAllText(folder+"/"+role+".mtl","newmtl Character\nKd 1 1 1\nmap_Kd basecolor.png\n");var texture=skin.sharedMaterial.GetTexture("_BaseMap");File.Copy(AssetDatabase.GetAssetPath(texture),folder+"/basecolor.png",true);UnityEngine.Object.DestroyImmediate(mesh);UnityEngine.Object.DestroyImmediate(go);
   // Keep the original rigged FBX separately, not mixed into the OBJ package.
   File.Copy(Directory.GetFiles("Assets/Characters/Rigged/"+role,"*.fbx").Single(),"Documentation/Nexoterra/Mixamo/"+role+"_con_esqueleto.fbx",true);
  }
 }
}
