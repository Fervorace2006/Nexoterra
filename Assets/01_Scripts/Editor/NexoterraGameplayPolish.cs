using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using Nexoterra.World;
namespace Nexoterra.Editor
{
 [InitializeOnLoad] public static class NexoterraGameplayPolish
 {
  const string Folder="Assets/NexoterraWorld/Expansion";
  static NexoterraGameplayPolish(){EditorApplication.update+=Poll;}
  static void Poll(){if(EditorApplication.isCompiling||EditorApplication.isUpdating||!File.Exists("Library/GameplayPolish.request"))return;if(EditorApplication.isPlayingOrWillChangePlaymode){EditorApplication.isPlaying=false;return;}File.Delete("Library/GameplayPolish.request");try{Build();}catch(Exception e){File.WriteAllText("Library/GameplayPolish.result.txt","FAIL\n"+e);Debug.LogException(e);}}
  public static void Build()
  {
   AssetDatabase.Refresh();EditorSceneManager.OpenScene(NexoterraBiomeBuilder.ScenePath);var report=new StringBuilder();
   AnimationClip Clip(string name,bool loop)
   {
    string path="Assets/Characters/Animations/"+name+".fbx";var imp=(ModelImporter)AssetImporter.GetAtPath(path);
    var clips=imp.clipAnimations.Length>0?imp.clipAnimations:imp.defaultClipAnimations;
    foreach(var c in clips){c.loopTime=loop;c.loopPose=loop;c.lockRootPositionXZ=false;c.keepOriginalPositionXZ=false;c.lockRootRotation=true;c.keepOriginalOrientation=true;c.lockRootHeightY=true;c.keepOriginalPositionY=false;c.heightFromFeet=true;}
    imp.clipAnimations=clips;imp.animationCompression=ModelImporterAnimationCompression.Off;imp.SaveAndReimport();
    var clip=AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().First(x=>!x.name.StartsWith("__preview"));report.AppendLine(name+" root velocity="+clip.averageSpeed);return clip;
   }
   var run=Clip("Pistol_Run",true);var left=Clip("voltereta",false);var right=Clip("voltereta2",false);var death=Clip("Death",false);
   var restModel=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/NexoterraWorld/Characters/Player_Male.prefab"));var restAnimator=restModel.GetComponentInChildren<Animator>();restAnimator.enabled=false;var restPose=new HumanPose();using(var handler=new HumanPoseHandler(restAnimator.avatar,restAnimator.transform)){handler.GetHumanPose(ref restPose);}UnityEngine.Object.DestroyImmediate(restModel);
   var idle=UnityEngine.Object.Instantiate(run);idle.name="Idle_Pistol";var walk=UnityEngine.Object.Instantiate(run);walk.name="Walk_Pistol";
   foreach(var binding in AnimationUtility.GetCurveBindings(idle))
   {
    var curve=AnimationUtility.GetEditorCurve(idle,binding);if(curve==null||curve.length==0)continue;
    string n=binding.propertyName.Replace(" ","");int muscle=Array.IndexOf(HumanTrait.MuscleName,binding.propertyName);bool leg=muscle>=0&&(n.Contains("UpperLeg")||n.Contains("LowerLeg")||n.Contains("Foot")||n.Contains("Toes"));
    float neutral=leg?restPose.muscles[muscle]:0;float value=leg?neutral:curve.Evaluate(0);if(n=="RootT.x"||n=="RootT.z")value=0;
    AnimationUtility.SetEditorCurve(idle,binding,AnimationCurve.Constant(0,1,value));
    if(leg){var walking=AnimationUtility.GetEditorCurve(walk,binding);var keys=walking.keys;for(int i=0;i<keys.Length;i++){keys[i].value=Mathf.Lerp(neutral,keys[i].value,.55f);keys[i].inTangent*=.55f;keys[i].outTangent*=.55f;}walking.keys=keys;AnimationUtility.SetEditorCurve(walk,binding,walking);}
   }
   idle=SaveClip(idle);walk=SaveClip(walk);
   var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(Folder+"/Player.controller");var layers=controller.layers;layers[0].iKPass=true;controller.layers=layers;var machine=layers[0].stateMachine;
   foreach(var s in machine.states)machine.RemoveState(s.state);
   string[] names={"Idle","Walk","Run","RollLeft","RollRight","Death"};AnimationClip[] motions={idle,walk,run,left,right,death};
   for(int i=0;i<names.Length;i++){var state=machine.AddState(names[i]);state.motion=motions[i];state.iKOnFeet=i==2;if(i==0)machine.defaultState=state;}
   foreach(string role in new[]{"Player_Male","Player_Female","Enemy1","Sir_Black"})ConfigureCharacter(role,controller,run,report);
   var player=UnityEngine.Object.FindAnyObjectByType<NexoterraThirdPerson>();var driver=player.GetComponent<NativePlayerAnimation>();
   driver.male=player.male.GetComponentInChildren<Animator>(true);driver.female=player.female.GetComponentInChildren<Animator>(true);
   ConfigureInstance(driver.male,controller);ConfigureInstance(driver.female,controller);
   Calibrate(driver.male,run,out driver.maleGrip,out driver.maleGripRotation);Calibrate(driver.female,run,out driver.femaleGrip,out driver.femaleGripRotation);
   driver.runReferenceSpeed=Mathf.Max(1,run.averageSpeed.magnitude);player.walkSpeed=2.8f;player.sprintSpeed=5.8f;player.GetComponent<PlayerAbilities>().dodgeDuration=1.05f;
   var web=BuildWeb();
   foreach(string path in new[]{"Assets/NexoterraWorld/Combat/Enemy1.prefab","Assets/NexoterraWorld/Combat/SirBlack.prefab",Folder+"/SirBlack_Final.prefab"})
   {
    var root=PrefabUtility.LoadPrefabContents(path);try{SetupEnemy(root,controller,run,web,report);PrefabUtility.SaveAsPrefabAsset(root,path);}finally{PrefabUtility.UnloadPrefabContents(root);}
   }
   foreach(var ai in UnityEngine.Object.FindObjectsByType<EnemyShooter>())SetupEnemy(ai.gameObject,controller,run,web,report);
   var catalog=player.GetComponent<SlugInventory>().catalog;foreach(var d in catalog.species){d.combat=SlugCombatProfile.Default(d.id);d.combatConfigured=true;}EditorUtility.SetDirty(catalog);
   EditorSceneManager.MarkSceneDirty(player.gameObject.scene);EditorSceneManager.SaveScene(player.gameObject.scene);AssetDatabase.SaveAssets();
   var menu=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);new GameObject("Menú principal").AddComponent<FrontEndMenu>();var camera=new GameObject("Menu Camera").AddComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;camera.cullingMask=0;camera.gameObject.AddComponent<AudioListener>();
   const string menuPath="Assets/00_Scenes/Nexoterra_Menu.unity";EditorSceneManager.SaveScene(menu,menuPath);
   EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(menuPath,true),new EditorBuildSettingsScene(NexoterraBiomeBuilder.ScenePath,true)};
   EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>(menuPath);
   var tex=(TextureImporter)AssetImporter.GetAtPath("Assets/Resources/Nexoterra/Loading.jpg");tex.maxTextureSize=2048;tex.mipmapEnabled=false;tex.textureCompression=TextureImporterCompression.CompressedHQ;tex.SaveAndReimport();
   EditorSceneManager.OpenScene(NexoterraBiomeBuilder.ScenePath);AssetDatabase.SaveAssets();
   Directory.CreateDirectory("Documentation/Nexoterra/GameplayPolish");File.WriteAllText("Documentation/Nexoterra/GameplayPolish/Instalacion.txt",report.ToString());File.WriteAllText("Library/GameplayPolish.result.txt","PASS\n"+report);
  }
  static AnimationClip SaveClip(AnimationClip clip){string path=Folder+"/"+clip.name+".anim";var old=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);if(old){EditorUtility.CopySerialized(clip,old);UnityEngine.Object.DestroyImmediate(clip);return old;}AssetDatabase.CreateAsset(clip,path);return clip;}
  static void ConfigureCharacter(string role,RuntimeAnimatorController controller,AnimationClip run,StringBuilder report)
  {
   string source=Directory.GetFiles("Assets/Characters/Rigged/"+role,"*.fbx").Single().Replace('\\','/');var imp=(ModelImporter)AssetImporter.GetAtPath(source);imp.animationType=ModelImporterAnimationType.Human;imp.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;imp.SaveAndReimport();var avatar=AssetDatabase.LoadAllAssetsAtPath(source).OfType<Avatar>().First();if(!avatar.isValid||!avatar.isHuman)throw new Exception("Avatar inválido: "+role);
   string path="Assets/NexoterraWorld/Characters/"+role+".prefab";var root=PrefabUtility.LoadPrefabContents(path);try{var animator=root.GetComponentInChildren<Animator>(true);animator.avatar=avatar;ConfigureInstance(animator,controller);Calibrate(animator,run,out _,out _);PrefabUtility.SaveAsPrefabAsset(root,path);report.AppendLine(role+": humanoid válido, IK y controlador compartido.");}finally{PrefabUtility.UnloadPrefabContents(root);}
  }
  static void ConfigureInstance(Animator animator,RuntimeAnimatorController controller){animator.enabled=true;animator.runtimeAnimatorController=controller;animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;if(!animator.GetComponent<HumanoidWeaponIK>())animator.gameObject.AddComponent<HumanoidWeaponIK>();PrefabUtility.RecordPrefabInstancePropertyModifications(animator);}
  static void Calibrate(Animator animator,AnimationClip run,out Vector3 position,out Quaternion rotation)
  {
   // Sample the imported clip directly in edit mode; Animator.Update(0) can leave a T-pose here.
   var root=UnityEngine.Object.Instantiate(animator.gameObject);root.SetActive(true);root.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);root.transform.localScale=animator.transform.lossyScale;
   var a=root.GetComponent<Animator>();a.enabled=true;AnimationMode.StartAnimationMode();AnimationMode.BeginSampling();AnimationMode.SampleAnimationClip(root,run,0);AnimationMode.EndSampling();
   var hand=a.GetBoneTransform(HumanBodyBones.RightHand);var support=a.GetBoneTransform(HumanBodyBones.LeftHand);rotation=Quaternion.Inverse(hand.rotation);position=hand.InverseTransformVector(new Vector3(0,-.07f,.07f));
   Vector3 offset=hand.InverseTransformPoint(support.position);Quaternion relative=Quaternion.Inverse(hand.rotation)*support.rotation;
   AnimationMode.StopAnimationMode();UnityEngine.Object.DestroyImmediate(root);var ik=animator.GetComponent<HumanoidWeaponIK>();ik.supportOffset=offset;ik.supportRotation=relative;
  }
  static void SetupEnemy(GameObject root,RuntimeAnimatorController controller,AnimationClip run,GameObject web,StringBuilder report)
  {
   var effects=root.GetComponent<EnemyControlEffects>();if(!effects)effects=root.AddComponent<EnemyControlEffects>();effects.webPrefab=web;
   var animator=root.GetComponentInChildren<Animator>(true);if(!animator||!animator.avatar||!animator.avatar.isHuman){report.AppendLine(root.name+": sin avatar humano; se conserva modelo estático.");return;}
   ConfigureInstance(animator,controller);var driver=root.GetComponent<NativeEnemyAnimation>();if(!driver)driver=root.AddComponent<NativeEnemyAnimation>();driver.animator=animator;Calibrate(animator,run,out driver.grip,out driver.gripRotation);
  }
  static GameObject BuildWeb()
  {
   var root=new GameObject("Telaraña de inmovilización");var model=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Directory.GetFiles("Assets/TripoModels/Teladearana","*.fbx").Single().Replace('\\','/')));model.transform.SetParent(root.transform,false);model.transform.localRotation=Quaternion.Euler(-90,0,0);
   var rs=model.GetComponentsInChildren<Renderer>();var bounds=rs[0].bounds;foreach(var r in rs)bounds.Encapsulate(r.bounds);float scale=2.2f/Mathf.Max(bounds.size.x,bounds.size.z);model.transform.localScale=Vector3.one*scale;model.transform.localPosition=new Vector3(-bounds.center.x,-bounds.min.y,-bounds.center.z)*scale;
   var mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));mat.color=new Color(.85f,.95f,1);string path=Folder+"/Web.mat";var old=AssetDatabase.LoadAssetAtPath<Material>(path);if(old){EditorUtility.CopySerialized(mat,old);UnityEngine.Object.DestroyImmediate(mat);mat=old;}else AssetDatabase.CreateAsset(mat,path);foreach(var r in rs)r.sharedMaterials=Enumerable.Repeat(mat,r.sharedMaterials.Length).ToArray();foreach(var c in root.GetComponentsInChildren<Collider>())UnityEngine.Object.DestroyImmediate(c);
   var prefab=PrefabUtility.SaveAsPrefabAsset(root,Folder+"/Web.prefab");UnityEngine.Object.DestroyImmediate(root);return prefab;
  }
 }
}
