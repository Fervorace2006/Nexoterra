using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Nexoterra.World;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class CampaignWaterZones
{
 static CampaignWaterZones(){EditorApplication.update+=Poll;}
 static void Poll(){
  if(!File.Exists("Library/CampaignWater.request")||EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
  File.Delete("Library/CampaignWater.request");try{Build();File.WriteAllText("Library/CampaignWater.result","PASS");}catch(Exception e){File.WriteAllText("Library/CampaignWater.result",e.ToString());Debug.LogException(e);}
 }
 static T Save<T>(T data,string name) where T:Object{string path="Assets/NexoterraWorld/Campaign/"+name+".asset";var existing=AssetDatabase.LoadAssetAtPath<T>(path);if(existing){EditorUtility.CopySerialized(data,existing);Object.DestroyImmediate(data);EditorUtility.SetDirty(existing);return existing;}AssetDatabase.CreateAsset(data,path);return data;}
 public static void Build(){
  var active=SceneManager.GetActiveScene();string path="Assets/00_Scenes/Campaign/Nexoterra_Agua.unity";var scene=SceneManager.GetSceneByPath(path);bool opened=!scene.isLoaded;
  if(opened)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);else if(scene.isDirty)throw new Exception("Guarda la escena de agua antes de ampliar sus lagunas.");SceneManager.SetActiveScene(scene);
  try{
   var roots=scene.GetRootGameObjects();var ground=roots.SelectMany(r=>r.GetComponentsInChildren<MeshFilter>()).Single(m=>m.name=="Suelo del bioma");var mesh=Object.Instantiate(ground.sharedMesh);var vertices=mesh.vertices;
   var centers=new[]{new Vector3(38,0,-20),new Vector3(-28,0,65)};var radii=new[]{14f,13f};
   for(int n=0;n<centers.Length;n++)for(int i=0;i<vertices.Length;i++){float distance=Vector2.Distance(new Vector2(vertices[i].x,vertices[i].z),new Vector2(centers[n].x,centers[n].z));if(distance<radii[n])vertices[i].y=Mathf.Min(vertices[i].y,-1.7f*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(radii[n]-5,radii[n],distance))));}
   mesh.vertices=vertices;mesh.RecalculateNormals();mesh.RecalculateBounds();mesh=Save(mesh,"Ground_Agua_Expanded");ground.sharedMesh=mesh;ground.GetComponent<MeshCollider>().sharedMesh=mesh;
   foreach(var r in roots)if(r.name.StartsWith("Laguna adicional "))Object.DestroyImmediate(r);
   for(int n=0;n<centers.Length;n++){
    var lake=new Mesh();var v=new List<Vector3>{centers[n]+Vector3.down*.12f};var tris=new List<int>();
    for(int i=0;i<64;i++){float angle=i*Mathf.PI/32;v.Add(centers[n]+new Vector3(Mathf.Cos(angle)*(radii[n]-1),-.12f,Mathf.Sin(angle)*(radii[n]-1)));tris.AddRange(new[]{0,(i+1)%64+1,i+1});}
    lake.SetVertices(v);lake.SetTriangles(tris,0);lake.SetUVs(0,v.Select(p=>new Vector2(p.x*.05f,p.z*.05f)).ToList());lake.RecalculateNormals();
    var go=new GameObject("Laguna adicional "+n);go.AddComponent<MeshFilter>().sharedMesh=Save(lake,"Water_Extra_"+n);go.AddComponent<MeshRenderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/NexoterraWorld/Biomes/Materials/Agua_azul.mat");
    foreach(var root in roots){if(!root)continue;var prop=root.GetComponent<WorldDestructible>();if(!root.GetComponent<MushroomFood>()&&!root.GetComponent<MushroomGlow>()&&!root.GetComponent<SlugSpawnPoint>()&&!(prop&&prop.kind!=WorldDestructible.Kind.CeilingRock))continue;Vector3 delta=root.transform.position-centers[n];delta.y=0;if(delta.magnitude>=radii[n]+2)continue;Vector3 p=centers[n]+(delta.sqrMagnitude>.01f?delta.normalized:Vector3.right)*(radii[n]+3);int x=Mathf.Clamp(Mathf.RoundToInt((p.x+125)/2),0,125),z=Mathf.Clamp(Mathf.RoundToInt((p.z+125)/2),0,125);p.y=vertices[z*126+x].y;root.transform.position=p;}
   }
   Physics.SyncTransforms();var sources=new List<NavMeshBuildSource>();NavMeshBuilder.CollectSources(new Bounds(Vector3.zero,new Vector3(250,50,250)),1,NavMeshCollectGeometry.PhysicsColliders,0,new List<NavMeshBuildMarkup>(),sources);
   sources.RemoveAll(s=>s.component&&(s.component.gameObject.scene!=scene||s.component.GetComponentInParent<WorldDestructible>()||s.component.GetComponentInParent<EnemyCombatant>()||s.component.GetComponentInParent<NexoterraThirdPerson>()||s.component.name=="Techo"));
   var settings=NavMesh.GetSettingsByIndex(0);settings.agentRadius=.18f;settings.agentHeight=.5f;settings.agentClimb=.35f;settings.agentSlope=45;
   var data=NavMeshBuilder.BuildNavMeshData(settings,sources,new Bounds(Vector3.zero,new Vector3(250,50,250)),Vector3.zero,Quaternion.identity);if(!data)throw new Exception("No se pudo reconstruir la navegación acuática");
   roots.SelectMany(r=>r?r.GetComponentsInChildren<SlugNavigation>():Array.Empty<SlugNavigation>()).Single().data=Save(data,"Navigation_Agua_Expanded");
   EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
  }finally{if(opened)EditorSceneManager.CloseScene(scene,true);if(active.IsValid()&&active.isLoaded)SceneManager.SetActiveScene(active);}
 }
}
