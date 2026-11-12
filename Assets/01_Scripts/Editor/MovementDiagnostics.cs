using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Nexoterra.World;
using Object=UnityEngine.Object;
[InitializeOnLoad]
public static class MovementDiagnostics
{
 static MovementDiagnostics(){EditorApplication.update+=Tick;}
 static void Tick(){if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating||!File.Exists("Library/MovementDiagnostics.request"))return;File.Delete("Library/MovementDiagnostics.request");try{Run();}catch(Exception e){File.WriteAllText("Library/MovementDiagnostics.result",e.ToString());}}
 static void Run(){
  var report=new StringBuilder();var root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/02_Prefabs/Gameplay/Player.prefab"));root.transform.position=Vector3.zero;
  var clip=AssetDatabase.LoadAllAssetsAtPath("Assets/Characters/Animations/Quaternius/SirBlack/Roll.fbx").OfType<AnimationClip>().First(c=>!c.name.StartsWith("__preview"));foreach(var b in AnimationUtility.GetCurveBindings(clip))if(b.propertyName.StartsWith("Root")||b.propertyName.StartsWith("Motion")||b.propertyName.Contains("Scale")){var curve=AnimationUtility.GetEditorCurve(clip,b);report.AppendLine("CURVE "+b.path+" / "+b.propertyName+" start="+curve.Evaluate(0)+" end="+curve.Evaluate(clip.length));}
  try{foreach(var script in root.GetComponentsInChildren<MonoBehaviour>(true))script.enabled=false;var driver=root.GetComponent<NativePlayerAnimation>();foreach(var animator in new[]{driver.male,driver.female}){animator.gameObject.SetActive(true);animator.Rebind();animator.Update(0);animator.SetLayerWeight(1,0);
   foreach(string state in new[]{"Idle","RollLeft","RollForward","RollBackward"})for(int i=0;i<=8;i++){animator.Play(state,0,i/8f);animator.Update(.0001f);var head=animator.GetBoneTransform(HumanBodyBones.Head);var hips=animator.GetBoneTransform(HumanBodyBones.Hips);var renderer=animator.GetComponentInChildren<SkinnedMeshRenderer>();var mesh=new Mesh();renderer.BakeMesh(mesh);var bounds=mesh.bounds;report.AppendLine(animator.name+" "+state+" t="+(i/8f)+" hips="+hips.position+" head="+head.position+" mesh="+bounds+" root="+animator.transform.position+" scale="+animator.transform.lossyScale);Object.DestroyImmediate(mesh);}
  }}finally{Object.DestroyImmediate(root);}
  var active=SceneManager.GetActiveScene();string path="Assets/00_Scenes/Campaign/Nexoterra_Agua.unity";var scene=SceneManager.GetSceneByPath(path);bool opened=!scene.isLoaded;if(opened)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
  try{Physics.SyncTransforms();foreach(var hit in Physics.RaycastAll(new Vector3(55,15,82),Vector3.down,40,1,QueryTriggerInteraction.Ignore))report.AppendLine("WATER center hit "+hit.collider.name+" point="+hit.point+" normal="+hit.normal);foreach(var go in scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<MeshFilter>()))if(go.name.Contains("Islote")||go.name.Contains("Acceso_al")){var m=go.sharedMesh;report.AppendLine(go.name+" collider="+(bool)go.GetComponent<Collider>()+" bounds="+m.bounds+" normals y="+m.normals.Min(n=>n.y)+".."+m.normals.Max(n=>n.y));}}
  finally{if(opened)EditorSceneManager.CloseScene(scene,true);SceneManager.SetActiveScene(active);}
  File.WriteAllText("Library/MovementDiagnostics.result",report.ToString());
 }
}
