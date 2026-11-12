using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Nexoterra.World;
using Object=UnityEngine.Object;
[InitializeOnLoad]
public static class MovementRepair
{
 static MovementRepair(){EditorApplication.update+=Tick;}
 static void Tick(){if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating||!File.Exists("Library/MovementRepair.request"))return;File.Delete("Library/MovementRepair.request");try{Rolls();Water();File.WriteAllText("Library/MovementRepair.result","PASS");}catch(Exception e){File.WriteAllText("Library/MovementRepair.result",e.ToString());Debug.LogException(e);}}
 static void Rolls(){
  var original=AssetDatabase.LoadAllAssetsAtPath("Assets/Characters/Animations/Quaternius/SirBlack/Roll.fbx").OfType<AnimationClip>().First(c=>!c.name.StartsWith("__preview"));
  var clip=Object.Instantiate(original);clip.name="RollForward_Grounded";float length=clip.length;
  void Curve(string property,AnimationCurve curve){AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve("",typeof(Animator),property),curve);}
  Curve("RootT.x",AnimationCurve.Constant(0,length,0));Curve("RootT.z",AnimationCurve.Constant(0,length,0));Curve("RootT.y",AnimationCurve.Constant(0,length,.8f));
  var go=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/02_Prefabs/Gameplay/Player.prefab"));go.transform.position=Vector3.zero;
  try{
   foreach(var script in go.GetComponentsInChildren<MonoBehaviour>(true))script.enabled=false;
   var animator=go.GetComponent<NativePlayerAnimation>().male;var overrides=new AnimatorOverrideController(animator.runtimeAnimatorController);var pairs=new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<AnimationClip,AnimationClip>>();overrides.GetOverrides(pairs);foreach(var pair in pairs.ToArray())if(pair.Key==original||pair.Key.name=="RollForward_Grounded")overrides[pair.Key]=clip;animator.runtimeAnimatorController=overrides;animator.Rebind();animator.Update(0);animator.SetLayerWeight(1,0);
   var bones=new[]{HumanBodyBones.Head,HumanBodyBones.LeftHand,HumanBodyBones.RightHand,HumanBodyBones.LeftFoot,HumanBodyBones.RightFoot,HumanBodyBones.LeftToes,HumanBodyBones.RightToes,HumanBodyBones.Hips,HumanBodyBones.LeftLowerArm,HumanBodyBones.RightLowerArm,HumanBodyBones.LeftLowerLeg,HumanBodyBones.RightLowerLeg}.Select(b=>animator.GetBoneTransform(b)).Where(t=>t).ToArray();
   var y=new AnimationCurve();int samples=Mathf.CeilToInt(length*60);
   for(int i=0;i<=samples;i++){float t=i/(float)samples;animator.Play("RollForward",0,t);animator.Update(.00001f);float lowest=bones.Min(b=>b.position.y);y.AddKey(t*length,.8f+(.11f-lowest)/animator.humanScale);}
   Curve("RootT.y",y);clip.EnsureQuaternionContinuity();
   string path="Assets/NexoterraWorld/Locomotion/RollForward_Grounded.anim";var saved=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);if(saved){EditorUtility.CopySerialized(clip,saved);Object.DestroyImmediate(clip);EditorUtility.SetDirty(saved);}else{AssetDatabase.CreateAsset(clip,path);saved=clip;}
   foreach(string role in new[]{"Player","Enemy1","SirBlack"}){var c=AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/NexoterraWorld/Locomotion/Imported/"+role+"_Animations.controller");foreach(var state in c.layers[0].stateMachine.states.Select(s=>s.state).Where(s=>s.name=="RollForward"||s.name=="RollBackward")){state.motion=saved;EditorUtility.SetDirty(state);}EditorUtility.SetDirty(c);}
   Object.DestroyImmediate(overrides);
  }finally{Object.DestroyImmediate(go);}
  AssetDatabase.SaveAssets();
 }
 static void Water(){
  string path="Assets/00_Scenes/Campaign/Nexoterra_Agua.unity";var active=SceneManager.GetActiveScene();var scene=SceneManager.GetSceneByPath(path);bool opened=!scene.isLoaded;if(opened)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);if(scene.isDirty)throw new Exception("Guarda la escena de agua antes de reparar sus colisiones.");
  try{
   var root=scene.GetRootGameObjects().Single(g=>g.name=="Santuario original recuperado");var top=root.GetComponentsInChildren<MeshFilter>().Single(m=>m.name=="Islote_elemental_agua_cima");
   if(!root.transform.Find("Islote sólido")){
    var points=top.sharedMesh.vertices.Select(v=>top.transform.TransformPoint(v)).Distinct().ToArray();var center=new Vector3(55,3.1f,82);var rim=points.Where(v=>Vector3.Distance(v,center)>2).OrderBy(v=>Mathf.Atan2(v.z-center.z,v.x-center.x)).ToArray();
    var vertices=rim.Concat(rim.Select(v=>new Vector3(v.x,-.5f,v.z))).ToArray();var triangles=new System.Collections.Generic.List<int>();int n=rim.Length;
    for(int i=0;i<n;i++){int j=(i+1)%n;triangles.AddRange(new[]{i,j,n+i,j,n+j,n+i});if(i>0&&i<n-1)triangles.AddRange(new[]{0,i+1,i,n,n+i,n+i+1});}
    var mesh=new Mesh{name="Islote sólido"};mesh.vertices=vertices;mesh.triangles=triangles.ToArray();mesh.RecalculateNormals();mesh.RecalculateBounds();string meshPath="Assets/NexoterraWorld/Campaign/Restored/Islote_Solid.asset";var saved=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);if(saved){EditorUtility.CopySerialized(mesh,saved);Object.DestroyImmediate(mesh);mesh=saved;}else AssetDatabase.CreateAsset(mesh,meshPath);
    var solid=new GameObject("Islote sólido");SceneManager.MoveGameObjectToScene(solid,scene);solid.transform.SetParent(root.transform,false);var collider=solid.AddComponent<MeshCollider>();collider.sharedMesh=mesh;collider.convex=true;
   }
   foreach(var collider in root.GetComponentsInChildren<MeshCollider>())if(collider.name.StartsWith("Islote_elemental_agua"))collider.enabled=false;
   foreach(var child in root.GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("Acceso_al_islote")).ToArray())Object.DestroyImmediate(child.gameObject);
   if(!root.transform.Find("Acceso recto al islote")){
    var ramp=GameObject.CreatePrimitive(PrimitiveType.Cube);SceneManager.MoveGameObjectToScene(ramp,scene);ramp.name="Acceso recto al islote";ramp.transform.SetParent(root.transform,false);Vector3 a=new Vector3(55,.15f,45),b=new Vector3(55,3.13f,78);ramp.transform.position=(a+b)*.5f-Vector3.up*.15f;ramp.transform.rotation=Quaternion.LookRotation(b-a);ramp.transform.localScale=new Vector3(6,.3f,Vector3.Distance(a,b)+1);ramp.GetComponent<Renderer>().sharedMaterial=top.GetComponent<Renderer>().sharedMaterial;
   }
   Physics.SyncTransforms();CampaignLandmarkRestore.RebuildNavigation(scene,2);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
  }finally{if(opened)EditorSceneManager.CloseScene(scene,true);SceneManager.SetActiveScene(active);}
 }
}
