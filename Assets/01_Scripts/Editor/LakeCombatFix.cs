using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Nexoterra.World;
using Object=UnityEngine.Object;
namespace Nexoterra.Editor {
[InitializeOnLoad] public static class LakeCombatFix {
 const string Folder="Assets/NexoterraWorld/MapRepair";
 static LakeCombatFix(){EditorApplication.update+=Poll;}
 static void Poll(){if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||!File.Exists("Library/LakeCombat.request"))return;string task=File.ReadAllText("Library/LakeCombat.request").Trim();File.Delete("Library/LakeCombat.request");try{if(task=="capture")Capture();else Build();}catch(Exception e){File.WriteAllText("Library/LakeCombat.result.txt","FAIL\n"+e);Debug.LogException(e);}}
 public static void Build(){
  var current=UnityEngine.SceneManagement.SceneManager.GetActiveScene();if(current.path==NexoterraBiomeBuilder.ScenePath&&current.isDirty)EditorSceneManager.SaveScene(current);
  EditorSceneManager.OpenScene(NexoterraBiomeBuilder.ScenePath);File.Copy(NexoterraBiomeBuilder.ScenePath,"Library/BeforeLakeCombat_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".unity",true);
  var report=new StringBuilder();
  foreach(string n in new[]{"Acceso_al_islote","Orillas_laguna_santuario","Islote_elemental_agua_cima"}){
   var f=GameObject.Find(n).GetComponent<MeshFilter>();var collider=f.GetComponent<MeshCollider>();if(!collider)collider=f.gameObject.AddComponent<MeshCollider>();
   var mesh=NexoterraMapRepair.Solid(f.sharedMesh,n.EndsWith("_cima")?3.7f:n.StartsWith("Acceso")?.55f:.65f);
   string path=Folder+"/Solid_"+n+".asset";var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(old){EditorUtility.CopySerialized(mesh,old);Object.DestroyImmediate(mesh);EditorUtility.SetDirty(old);}else{AssetDatabase.CreateAsset(mesh,path);old=mesh;}
   collider.sharedMesh=null;collider.sharedMesh=old;collider.convex=false;collider.isTrigger=false;
   report.AppendLine("Sólido cerrado: "+n);
  }
  var lake=GameObject.Find("Laguna_del_santuario").GetComponent<Renderer>();
  string matPath=Folder+"/Agua_santuario.mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(matPath);if(!mat){mat=new Material(Shader.Find("Nexoterra/Sanctuary Water"));AssetDatabase.CreateAsset(mat,matPath);}mat.shader=Shader.Find("Nexoterra/Sanctuary Water");var b=lake.bounds;mat.SetVector("_Center",new Vector4(b.center.x,b.center.z,b.extents.x,b.extents.z));EditorUtility.SetDirty(mat);lake.sharedMaterial=mat;
  var spawns=Object.FindObjectsByType<SlugSpawnPoint>().Where(s=>SlugCatalog.Canonical(s.species)=="Infierno").OrderBy(s=>(s.transform.position-new Vector3(190,0,177)).sqrMagnitude).ToArray();
  if(spawns.Length==0)throw new Exception("No hay spawn Infierno");spawns[0].unique=true;spawns[0].population=1;for(int i=1;i<spawns.Length;i++)Object.DestroyImmediate(spawns[i].gameObject);
  report.AppendLine("Infierno: único spawn volcánico; no reaparece tras domesticarlo.");
  const string webPath="Assets/NexoterraWorld/Expansion/Web.prefab";var webRoot=PrefabUtility.LoadPrefabContents(webPath);
  try{var f=webRoot.GetComponentInChildren<MeshFilter>();var bounds=f.sharedMesh.bounds;f.transform.localRotation=Quaternion.identity;var scale=new Vector3(2.8f/bounds.size.x,1.15f/bounds.size.y,2.8f/bounds.size.z);f.transform.localScale=scale;f.transform.localPosition=-Vector3.Scale(new Vector3(bounds.center.x,bounds.min.y,bounds.center.z),scale);foreach(var c in webRoot.GetComponentsInChildren<Collider>())Object.DestroyImmediate(c);PrefabUtility.SaveAsPrefabAsset(webRoot,webPath);}finally{PrefabUtility.UnloadPrefabContents(webRoot);}
  var web=AssetDatabase.LoadAssetAtPath<GameObject>(webPath);
  foreach(var control in Object.FindObjectsByType<EnemyControlEffects>())control.webPrefab=web;
  foreach(var player in Object.FindObjectsByType<PlayerHealth>()){var control=player.GetComponent<EnemyControlEffects>();if(!control)control=player.gameObject.AddComponent<EnemyControlEffects>();control.webPrefab=web;}
  Physics.SyncTransforms();NexoterraMapRepair.BakeNavigation();
  Validate(report);EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());AssetDatabase.SaveAssets();
  Directory.CreateDirectory("Documentation/Nexoterra/LakeCombat");File.WriteAllText("Documentation/Nexoterra/LakeCombat/Checks.txt",report.ToString());Capture();File.WriteAllText("Library/LakeCombat.result.txt","PASS\n"+report);
 }
 static void Validate(StringBuilder report){
  foreach(string name in new[]{"Acceso_al_islote","Islote_elemental_agua_cima"}){
   var f=GameObject.Find(name).GetComponent<MeshFilter>();var mc=f.GetComponent<MeshCollider>();var v=f.sharedMesh.vertices;var tr=f.sharedMesh.triangles;int count=0;
   for(int i=0;i<tr.Length;i+=Math.Max(3,(tr.Length/30/3)*3)){
    Vector3 p=f.transform.TransformPoint((v[tr[i]]+v[tr[i+1]]+v[tr[i+2]])/3);float depth=name.EndsWith("_cima")?3.7f:.55f;
    if(!mc.Raycast(new Ray(p+Vector3.up*1,Vector3.down),out _,2)||!mc.Raycast(new Ray(p-Vector3.up*(depth+1),Vector3.up),out _,2))throw new Exception("Colisión superior/inferior fallida: "+name+" "+p);
    count++;
   }report.AppendLine("PASS rayos arriba/abajo "+name+": "+count);
  }
  var e=new GameObject("Test estados");try{var control=e.AddComponent<EnemyControlEffects>();control.webPrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/NexoterraWorld/Expansion/Web.prefab");control.Apply("Arana",Vector3.forward);if(control.CanMove||!control.CanShoot||control.RootRemaining!=5.5f)throw new Exception("Telaraña bloquea incorrectamente");report.AppendLine("PASS telaraña: sin movimiento, puede disparar, duración 5.5s");var shooter=e.AddComponent<EnemyShooter>();var ids=Enumerable.Range(0,6).Select(i=>shooter.NextSpecies()).Distinct().ToArray();if(ids.Length!=6)throw new Exception("Sin variedad enemiga");report.AppendLine("PASS seis especies enemigas: "+string.Join(", ",ids));}finally{Object.DestroyImmediate(e);}
  if(Object.FindObjectsByType<SlugSpawnPoint>().Count(s=>SlugCatalog.Canonical(s.species)=="Infierno")!=1)throw new Exception("Infierno duplicado");report.AppendLine("PASS una Infierno");
 }
 public static void Capture(){
  var cam=Camera.main;var pos=cam.transform.position;var rot=cam.transform.rotation;var fov=cam.fieldOfView;var far=cam.farClipPlane;var data=cam.GetUniversalAdditionalCameraData();bool shadows=data.renderShadows,post=data.renderPostProcessing;data.renderShadows=false;data.renderPostProcessing=false;
  var rt=new RenderTexture(1400,850,24);rt.Create();var old=RenderTexture.active;GameObject web=null;
  void Shot(string name,Vector3 position,Vector3 target){cam.transform.position=position;cam.transform.LookAt(target);cam.fieldOfView=58;cam.farClipPlane=220;RenderPipeline.SubmitRenderRequest(cam,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});RenderPipeline.SubmitRenderRequest(cam,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});RenderTexture.active=rt;var tex=new Texture2D(1400,850,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1400,850),0,0);tex.Apply();File.WriteAllBytes("Documentation/Nexoterra/LakeCombat/"+name+".png",tex.EncodeToPNG());Object.DestroyImmediate(tex);}
  try{
   Shot("Lago",new Vector3(160,12,-44),new Vector3(133,1,-18));
   var enemy=Object.FindObjectsByType<EnemyCombatant>().First(e=>e.GetComponent<EnemyShooter>()&&!e.GetComponent<EnemyShooter>().boss);var p=enemy.transform.position;web=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/NexoterraWorld/Expansion/Web.prefab"),p+Vector3.up*.035f,Quaternion.identity);Shot("Telarana",p+new Vector3(3,2.6f,4),p+Vector3.up*.7f);
  }finally{if(web)Object.DestroyImmediate(web);RenderTexture.active=old;rt.Release();Object.DestroyImmediate(rt);cam.transform.position=pos;cam.transform.rotation=rot;cam.fieldOfView=fov;cam.farClipPlane=far;data.renderShadows=shadows;data.renderPostProcessing=post;}
 }
}}
