using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Nexoterra.World;

namespace Nexoterra.Editor
{
 [InitializeOnLoad]
 public static class IncomingAnimationInstaller
 {
  const string Request="Library/IncomingAnimations.request";
  const string Root="Assets/Characters/Animations/Incoming";
  const string Output="Assets/NexoterraWorld/Locomotion/Imported";
  static readonly string[] Names={"idle_loop","walk_loop","pistol_idle_loop","pistol_shoot","pistol_reload"};
  static IncomingAnimationInstaller(){EditorApplication.update+=Poll;}
  static void Poll(){if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlaying||EditorApplication.isPlayingOrWillChangePlaymode||!File.Exists(Request))return;File.Delete(Request);try{Build();}catch(Exception e){File.WriteAllText("Library/IncomingAnimations.result.txt","FAIL\n"+e);Debug.LogException(e);}}
  public static void Build()
  {
   if(EditorApplication.isPlaying||EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Detén Play Mode antes de instalar las animaciones.");
   AssetDatabase.Refresh();var scene=SceneManager.GetActiveScene();if(scene.isDirty&&!string.IsNullOrEmpty(scene.path))EditorSceneManager.SaveScene(scene);
   EditorSceneManager.OpenScene(NexoterraBiomeBuilder.ScenePath);Directory.CreateDirectory(Output);Directory.CreateDirectory("Library/IncomingAnimationsBackup");
   File.Copy(NexoterraBiomeBuilder.ScenePath,"Library/IncomingAnimationsBackup/Scene_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".unity",true);
   var report=new StringBuilder();var clips=new System.Collections.Generic.Dictionary<string,AnimationClip[]>();
   foreach(string role in new[]{"Player","Enemy1","SirBlack"})clips[role]=ImportRole(role,report);
   var basis=AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/NexoterraWorld/Locomotion/NaturalPlayer.controller");if(!basis)throw new Exception("No se encontró NaturalPlayer.controller; no se reemplazó ningún controlador.");
   var originalRolls=new System.Collections.Generic.Dictionary<string,Motion>();foreach(string n in new[]{"RollLeft","RollRight","Death"}){var st=basis.layers[0].stateMachine.states.Select(s=>s.state).FirstOrDefault(s=>s.name==n);if(st&&st.motion)originalRolls[n]=st.motion;}
   RestoreBaseController(basis);
   var controllers=new System.Collections.Generic.Dictionary<string,AnimatorController>();
   foreach(string role in new[]{"Player","Enemy1","SirBlack"})controllers[role]=MakeController(role,basis,originalRolls,clips[role],report);
   var player=UnityEngine.Object.FindAnyObjectByType<NexoterraThirdPerson>();if(!player)throw new Exception("No se encontró el jugador en Nexoterra_CuatroNodos.");
   var native=player.GetComponent<NativePlayerAnimation>();if(!native)throw new Exception("El jugador no tiene NativePlayerAnimation.");
   native.male=player.male.GetComponentInChildren<Animator>(true);native.female=player.female.GetComponentInChildren<Animator>(true);native.playerController=controllers["Player"];native.weapon=player.muzzle?player.muzzle.parent:null;
   native.runReferenceSpeed=3.8f;native.reloadDuration=clips["Player"][4].length;native.shootDuration=Mathf.Clamp(clips["Player"][3].length*.65f,.18f,.5f);
   Configure(native.male,controllers["Player"],report);Configure(native.female,controllers["Player"],report);
   CalibrateGrip(native.male,out native.maleGrip,out native.maleGripRotation);CalibrateGrip(native.female,out native.femaleGrip,out native.femaleGripRotation);
   foreach(var ik in new[]{native.male.GetComponent<HumanoidWeaponIK>(),native.female.GetComponent<HumanoidWeaponIK>()})if(ik){ik.weaponHandIK=false;ik.supportOffset=Vector3.zero;ik.supportRotation=Quaternion.identity;EditorUtility.SetDirty(ik);PrefabUtility.RecordPrefabInstancePropertyModifications(ik);}
   var oldDriver=player.GetComponent<PlayerAnimationDriver>();if(!oldDriver)oldDriver=player.gameObject.AddComponent<PlayerAnimationDriver>();oldDriver.reloadDuration=native.reloadDuration;
   foreach(var pose in player.GetComponentsInChildren<HumanoidPose>(true))pose.enabled=false;
   AssignCharacterPrefab("Player_Male",controllers["Player"],report);AssignCharacterPrefab("Player_Female",controllers["Player"],report);
   AssignCharacterPrefab("Enemy1",controllers["Player"],report);AssignCharacterPrefab("Sir_Black",controllers["Player"],report);
   AssignCombatPrefab("Enemy1",controllers["Player"],report);AssignCombatPrefab("SirBlack",controllers["Player"],report);
   foreach(var enemy in UnityEngine.Object.FindObjectsByType<NativeEnemyAnimation>())
   {bool boss=enemy.GetComponent<EnemyShooter>()&&enemy.GetComponent<EnemyShooter>().boss;Configure(enemy.animator,controllers["Player"],report);CalibrateGrip(enemy.animator,out enemy.grip,out enemy.gripRotation);EditorUtility.SetDirty(enemy);}
   EditorUtility.SetDirty(native);EditorUtility.SetDirty(oldDriver);EditorSceneManager.MarkSceneDirty(player.gameObject.scene);EditorSceneManager.SaveScene(player.gameObject.scene);AssetDatabase.SaveAssets();
   File.WriteAllText("Library/IncomingAnimations.result.txt","PASS\n"+report);File.WriteAllText("Documentation/Nexoterra/Locomotion/IncomingAnimations.txt",report.ToString());
  }
  static AnimationClip[] ImportRole(string role,StringBuilder report)
  {
   var result=new AnimationClip[Names.Length];
   for(int i=0;i<Names.Length;i++)
   {
    string path=Root+"/"+role+"/"+Names[i]+"_unity_fit.fbx";var importer=AssetImporter.GetAtPath(path) as ModelImporter;if(!importer)throw new Exception("Falta el clip: "+path);
    importer.animationType=ModelImporterAnimationType.Human;importer.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;importer.importAnimation=true;importer.importCameras=false;importer.importLights=false;importer.materialImportMode=ModelImporterMaterialImportMode.None;importer.animationCompression=ModelImporterAnimationCompression.Off;
    var settings=importer.clipAnimations.Length>0?importer.clipAnimations:importer.defaultClipAnimations;
    foreach(var setting in settings){bool loop=Names[i].EndsWith("_loop");setting.loopTime=loop;setting.loopPose=loop;setting.lockRootPositionXZ=true;setting.keepOriginalPositionXZ=false;setting.lockRootHeightY=true;setting.heightFromFeet=true;setting.keepOriginalPositionY=false;setting.lockRootRotation=true;setting.keepOriginalOrientation=false;}
    importer.clipAnimations=settings;importer.SaveAndReimport();var avatar=AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();
    if(!avatar||!avatar.isValid||!avatar.isHuman)throw new Exception("El clip no se pudo configurar como Humanoid: "+path);
    result[i]=AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(c=>!c.name.StartsWith("__preview"));if(!result[i]||result[i].length<=0)throw new Exception("Clip vacío o inválido: "+path);
    report.AppendLine(role+" / "+Names[i]+": "+result[i].length.ToString("0.00")+" s, avatar humanoide válido");
   }
   return result;
  }
  static void RestoreBaseController(AnimatorController basis)
  {
   var main=basis.layers[0].stateMachine;Set(main,"Idle",AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/NexoterraWorld/Locomotion/Idle_Natural.anim"));Set(main,"Walk",AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/NexoterraWorld/Locomotion/Walk_Natural.anim"));Set(main,"Run",AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/NexoterraWorld/Locomotion/Walk_Natural.anim"));Remove(main,"SwimIdle");Remove(main,"SwimForward");
   int upper=Array.FindIndex(basis.layers,l=>l.name=="AimUpperBody");if(upper>=0){var machine=basis.layers[upper].stateMachine;Set(machine,"AimPose",AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/NexoterraWorld/Expansion/Idle_Pistol.anim"));Remove(machine,"AimUp");Remove(machine,"ShootUpper");Remove(machine,"ReloadUpper");}
   EditorUtility.SetDirty(basis);
  }
  static AnimatorController MakeController(string role,AnimatorController basis,System.Collections.Generic.Dictionary<string,Motion> rolls,AnimationClip[] clips,StringBuilder report)
  {
   string path=Output+"/"+role+"_Animations.controller";if(AssetDatabase.LoadAssetAtPath<AnimatorController>(path))AssetDatabase.DeleteAsset(path);
   var controller=AnimatorController.CreateAnimatorControllerAtPath(path);controller.name=role+"_Animations";var main=controller.layers[0].stateMachine;
   foreach(var state in main.states)main.RemoveState(state.state);
   Set(main,"Idle",clips[0]);Set(main,"Walk",clips[1]);Set(main,"Run",clips[1]);
   foreach(string n in new[]{"RollLeft","RollRight","Death"})if(rolls.TryGetValue(n,out var motion))Set(main,n,motion);
   controller.AddLayer("AimUpperBody");var upper=Array.FindIndex(controller.layers,l=>l.name=="AimUpperBody");var layers=controller.layers;layers[upper].avatarMask=AssetDatabase.LoadAssetAtPath<AvatarMask>("Assets/NexoterraWorld/Locomotion/AimUpperBody.mask");layers[upper].defaultWeight=0;layers[upper].iKPass=true;controller.layers=layers;
   var upperMachine=controller.layers[upper].stateMachine;foreach(var state in upperMachine.states)upperMachine.RemoveState(state.state);
   Set(upperMachine,"AimPose",clips[2]);Set(upperMachine,"ShootUpper",clips[3]);Set(upperMachine,"ReloadUpper",clips[4]);
   EditorUtility.SetDirty(controller);report.AppendLine("Controlador "+role+" aislado con sus propios estados; RollLeft/RollRight copiados intactos del controlador original.");return controller;
  }
  static void Set(AnimatorStateMachine machine,string name,Motion motion){var state=machine.states.Select(s=>s.state).FirstOrDefault(s=>s.name==name);if(!state)state=machine.AddState(name);state.motion=motion;if(name=="Idle"||name=="AimPose")machine.defaultState=state;}
  static void Remove(AnimatorStateMachine machine,string name){var state=machine.states.Select(s=>s.state).FirstOrDefault(s=>s.name==name);if(state)machine.RemoveState(state);}
  static void CalibrateGrip(Animator source,out Vector3 position,out Quaternion rotation){
   var clone=UnityEngine.Object.Instantiate(source.gameObject);try{
    clone.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
    foreach(var behaviour in clone.GetComponentsInChildren<MonoBehaviour>())behaviour.enabled=false;
    var a=clone.GetComponent<Animator>();a.Rebind();a.Update(0);a.Play("Idle",0,0);int upper=a.GetLayerIndex("AimUpperBody");a.SetLayerWeight(upper,1);a.Play("AimPose",upper,.25f);a.Update(.001f);
    var hand=a.GetBoneTransform(HumanBodyBones.RightHand);
    position=hand.InverseTransformVector(new Vector3(0,-.14f,.04f));rotation=Quaternion.Inverse(hand.rotation);
   }finally{UnityEngine.Object.DestroyImmediate(clone);}
  }
  static void Configure(Animator animator,RuntimeAnimatorController controller,StringBuilder report){if(!animator||!animator.avatar||!animator.avatar.isHuman||!animator.avatar.isValid)throw new Exception("Avatar no válido en "+(animator?animator.name:"Animator nulo"));animator.runtimeAnimatorController=controller;animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;EditorUtility.SetDirty(animator);report.AppendLine("Controlador asignado: "+animator.name+" → "+controller.name);}
  static void AssignCharacterPrefab(string role,AnimatorController controller,StringBuilder report)
  {string path="Assets/NexoterraWorld/Characters/"+role+".prefab";if(!File.Exists(path))return;var root=PrefabUtility.LoadPrefabContents(path);try{var animator=root.GetComponentInChildren<Animator>(true);if(animator)Configure(animator,controller,report);PrefabUtility.SaveAsPrefabAsset(root,path);}finally{PrefabUtility.UnloadPrefabContents(root);}}
  static void AssignCombatPrefab(string role,AnimatorController controller,StringBuilder report)
  {string path="Assets/NexoterraWorld/Combat/"+role+".prefab";if(!File.Exists(path))return;var root=PrefabUtility.LoadPrefabContents(path);try{var enemy=root.GetComponent<NativeEnemyAnimation>();if(enemy&&enemy.animator){Configure(enemy.animator,controller,report);CalibrateGrip(enemy.animator,out enemy.grip,out enemy.gripRotation);}PrefabUtility.SaveAsPrefabAsset(root,path);}finally{PrefabUtility.UnloadPrefabContents(root);}}
 }
}
