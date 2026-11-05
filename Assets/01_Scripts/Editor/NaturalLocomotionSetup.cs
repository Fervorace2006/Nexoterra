using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using Nexoterra.World;
using Object=UnityEngine.Object;
namespace Nexoterra.Editor {
[InitializeOnLoad] public static class NaturalLocomotionSetup {
 const string Folder="Assets/NexoterraWorld/Locomotion";
 static NaturalLocomotionSetup(){EditorApplication.update+=Poll;}
 static void Poll(){if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||!File.Exists("Library/NaturalMotion.request"))return;File.Delete("Library/NaturalMotion.request");try{Build();}catch(Exception e){File.WriteAllText("Library/NaturalMotion.result.txt","FAIL\n"+e);Debug.LogException(e);}}
 public static void Build(){
  AssetDatabase.Refresh();Directory.CreateDirectory(Folder);AssetDatabase.Refresh();var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();if(scene.path==NexoterraBiomeBuilder.ScenePath&&scene.isDirty)EditorSceneManager.SaveScene(scene);EditorSceneManager.OpenScene(NexoterraBiomeBuilder.ScenePath);File.Copy(NexoterraBiomeBuilder.ScenePath,"Library/Locomotion/Before_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".unity",true);
  var report=new StringBuilder();var aim=AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/NexoterraWorld/Expansion/Idle_Pistol.anim");
  var idle=Import("Idle",aim,report);var walk=Import("Walk",aim,report);var run=Import("Run",aim,report);
  string path=Folder+"/NaturalPlayer.controller";var c=AssetDatabase.LoadAssetAtPath<AnimatorController>(path);if(!c)c=AnimatorController.CreateAnimatorControllerAtPath(path);
  while(c.layers.Length>1)c.RemoveLayer(c.layers.Length-1);var layers=c.layers;layers[0].iKPass=false;c.layers=layers;var sm=c.layers[0].stateMachine;foreach(var st in sm.states)sm.RemoveState(st.state);
  var old=AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/NexoterraWorld/Expansion/Player.controller");
  foreach(var entry in old.layers[0].stateMachine.states){var st=sm.AddState(entry.state.name);st.motion=st.name=="Idle"?idle:st.name=="Walk"?walk:st.name=="Run"?run:entry.state.motion;st.iKOnFeet=false;if(st.name=="Idle")sm.defaultState=st;}
  string maskPath=Folder+"/AimUpperBody.mask";var mask=AssetDatabase.LoadAssetAtPath<AvatarMask>(maskPath);if(!mask){mask=new AvatarMask();AssetDatabase.CreateAsset(mask,maskPath);}for(int i=0;i<(int)AvatarMaskBodyPart.LastBodyPart;i++)mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i,false);
  foreach(var part in new[]{AvatarMaskBodyPart.Body,AvatarMaskBodyPart.Head,AvatarMaskBodyPart.LeftArm,AvatarMaskBodyPart.RightArm,AvatarMaskBodyPart.LeftFingers,AvatarMaskBodyPart.RightFingers})mask.SetHumanoidBodyPartActive(part,true);EditorUtility.SetDirty(mask);
  c.AddLayer("AimUpperBody");layers=c.layers;layers[1].avatarMask=mask;layers[1].defaultWeight=0;layers[1].iKPass=true;c.layers=layers;var pose=c.layers[1].stateMachine.AddState("AimPose");pose.motion=aim;c.layers[1].stateMachine.defaultState=pose;EditorUtility.SetDirty(c);
  var player=Object.FindAnyObjectByType<NexoterraThirdPerson>();var driver=player.GetComponent<NativePlayerAnimation>();
  foreach(var a in new[]{driver.male,driver.female})Configure(a,c,report);Calibrate(driver.male,out driver.maleGrip,out driver.maleGripRotation);Calibrate(driver.female,out driver.femaleGrip,out driver.femaleGripRotation);driver.gripHeightCorrection=0;var legacy=player.GetComponent<PlayerAnimationDriver>();if(legacy)legacy.enabled=false;foreach(var p in player.GetComponentsInChildren<HumanoidPose>(true))p.enabled=false;
  driver.walkReferenceSpeed=Speed("Walk",2);driver.runReferenceSpeed=Speed("Run",4.5f);report.AppendLine("Playback walk="+driver.walkReferenceSpeed+" run="+driver.runReferenceSpeed);
  foreach(string role in new[]{"Player_Male","Player_Female","Enemy1","Sir_Black"}){
   string prefab="Assets/NexoterraWorld/Characters/"+role+".prefab";var root=PrefabUtility.LoadPrefabContents(prefab);try{Configure(root.GetComponentInChildren<Animator>(true),c,report);PrefabUtility.SaveAsPrefabAsset(root,prefab);}finally{PrefabUtility.UnloadPrefabContents(root);}
  }
  foreach(var enemy in Object.FindObjectsByType<NativeEnemyAnimation>()){Configure(enemy.animator,c,report);enemy.walkReferenceSpeed=driver.walkReferenceSpeed;Calibrate(enemy.animator,out enemy.grip,out enemy.gripRotation);}
  foreach(string prefab in new[]{"Assets/NexoterraWorld/Combat/Enemy1.prefab","Assets/NexoterraWorld/Combat/SirBlack.prefab"}){var root=PrefabUtility.LoadPrefabContents(prefab);try{var e=root.GetComponent<NativeEnemyAnimation>();if(e){Configure(e.animator,c,report);e.walkReferenceSpeed=driver.walkReferenceSpeed;Calibrate(e.animator,out e.grip,out e.gripRotation);}PrefabUtility.SaveAsPrefabAsset(root,prefab);}finally{PrefabUtility.UnloadPrefabContents(root);}}
  foreach(var st in old.layers[0].stateMachine.states.Where(s=>s.state.name.StartsWith("Roll")))if(sm.states.First(s=>s.state.name==st.state.name).state.motion!=st.state.motion)throw new Exception("Voltereta modificada");report.AppendLine("PASS volteretas originales conservadas");
  EditorSceneManager.MarkSceneDirty(player.gameObject.scene);EditorSceneManager.SaveScene(player.gameObject.scene);AssetDatabase.SaveAssets();File.WriteAllText("Library/NaturalMotion.result.txt","PASS\n"+report);File.WriteAllText("Documentation/Nexoterra/Locomotion/Setup.txt",report.ToString());
 }
 static float Speed(string name,float fallback){var clip=AssetDatabase.LoadAllAssetsAtPath("Assets/Characters/Animations/Locomotion/"+name+".fbx").OfType<AnimationClip>().First(x=>!x.name.StartsWith("__preview"));return clip.averageSpeed.magnitude>.3f?clip.averageSpeed.magnitude:fallback;}
 static AnimationClip Import(string name,AnimationClip grip,StringBuilder report){
  string source="Assets/Characters/Animations/Locomotion/"+name+".fbx";var imp=(ModelImporter)AssetImporter.GetAtPath(source);imp.animationType=ModelImporterAnimationType.Human;imp.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;imp.importCameras=false;imp.importLights=false;imp.materialImportMode=ModelImporterMaterialImportMode.None;imp.animationCompression=ModelImporterAnimationCompression.Off;
  var clips=imp.clipAnimations.Length>0?imp.clipAnimations:imp.defaultClipAnimations;foreach(var clip in clips){clip.loopTime=true;clip.loopPose=true;clip.lockRootPositionXZ=true;clip.keepOriginalPositionXZ=false;clip.lockRootHeightY=true;clip.heightFromFeet=true;clip.keepOriginalPositionY=false;clip.lockRootRotation=true;clip.keepOriginalOrientation=false;}
  imp.clipAnimations=clips;imp.SaveAndReimport();var avatar=AssetDatabase.LoadAllAssetsAtPath(source).OfType<Avatar>().FirstOrDefault();if(!avatar||!avatar.isValid||!avatar.isHuman)throw new Exception("Rig de animación inválido "+name);
  var original=AssetDatabase.LoadAllAssetsAtPath(source).OfType<AnimationClip>().First(x=>!x.name.StartsWith("__preview"));var copy=Object.Instantiate(original);copy.name=name+"_Natural";
  foreach(var b in AnimationUtility.GetCurveBindings(grip).Where(b=>b.propertyName.StartsWith("RightHand.")||b.propertyName.StartsWith("Right ")&&(b.propertyName.Contains("Thumb")||b.propertyName.Contains("Index")||b.propertyName.Contains("Middle")||b.propertyName.Contains("Ring")||b.propertyName.Contains("Little")))){var curve=AnimationUtility.GetEditorCurve(grip,b);AnimationUtility.SetEditorCurve(copy,b,AnimationCurve.Constant(0,copy.length,curve.Evaluate(0)));}
  string path=Folder+"/"+copy.name+".anim";var existing=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);if(existing){EditorUtility.CopySerialized(copy,existing);Object.DestroyImmediate(copy);copy=existing;}else AssetDatabase.CreateAsset(copy,path);EditorUtility.SetDirty(copy);report.AppendLine(name+" length="+original.length+" speed="+original.averageSpeed);return copy;
 }
 static void Calibrate(Animator source,out Vector3 grip,out Quaternion rotation){
 var root=Object.Instantiate(source.gameObject);root.SetActive(true);root.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);root.transform.localScale=source.transform.lossyScale;foreach(var mb in root.GetComponentsInChildren<MonoBehaviour>())mb.enabled=false;
 try{var a=root.GetComponent<Animator>();a.Rebind();a.Update(0);a.Play("Idle",0,0);a.SetLayerWeight(1,1);a.Update(.01f);var hand=a.GetBoneTransform(HumanBodyBones.RightHand);var left=a.GetBoneTransform(HumanBodyBones.LeftHand);rotation=Quaternion.Inverse(hand.rotation);grip=hand.InverseTransformVector(new Vector3(0,.03f,.07f));var ik=source.GetComponent<HumanoidWeaponIK>();if(ik){ik.supportOffset=hand.InverseTransformPoint(left.position);ik.supportRotation=Quaternion.Inverse(hand.rotation)*left.rotation;PrefabUtility.RecordPrefabInstancePropertyModifications(ik);}}
 finally{Object.DestroyImmediate(root);}
 }
 static void Configure(Animator a,RuntimeAnimatorController c,StringBuilder r){if(!a||!a.avatar||!a.avatar.isValid||!a.avatar.isHuman)throw new Exception("Avatar inválido "+(a?a.name:"null"));a.runtimeAnimatorController=c;a.applyRootMotion=false;a.enabled=true;a.cullingMode=AnimatorCullingMode.AlwaysAnimate;PrefabUtility.RecordPrefabInstancePropertyModifications(a);r.AppendLine("Humanoide válido "+a.name);}
}}
