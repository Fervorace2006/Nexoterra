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

public static class CampaignLandmarkRestore
{
 static readonly Vector3[] Camps={new Vector3(275,0,-270),new Vector3(-225,0,5),new Vector3(-210,0,195),new Vector3(190,0,170),new Vector3(105,0,-48),new Vector3(-230,0,-220)};
 const string Folder="Assets/NexoterraWorld/Campaign/Restored";
 static T Save<T>(T value,string name) where T:Object{string path=Folder+"/"+name+".asset";var old=AssetDatabase.LoadAssetAtPath<T>(path);if(old){EditorUtility.CopySerialized(value,old);Object.DestroyImmediate(value);EditorUtility.SetDirty(old);return old;}AssetDatabase.CreateAsset(value,path);return value;}
 public static void Run(){
  Directory.CreateDirectory(Folder);AssetDatabase.Refresh();var active=SceneManager.GetActiveScene();string original="Assets/00_Scenes/Nexoterra_CuatroNodos.unity";var source=SceneManager.GetSceneByPath(original);bool opened=!source.isLoaded;if(opened)source=EditorSceneManager.OpenScene(original,OpenSceneMode.Additive);
  try{
   var meshes=source.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<MeshFilter>(true)).Where(m=>m.sharedMesh&&m.GetComponent<MeshRenderer>()).ToArray();
   for(int level=0;level<5;level++){
    int biome=new[]{0,1,4,2,3}[level];string path="Assets/00_Scenes/Campaign/"+CampaignLevel.Scenes[level]+".unity";
    var scene=SceneManager.GetSceneByPath(path);bool close=!scene.isLoaded;if(close)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);if(scene.isDirty)throw new Exception("Guarda la escena antes de recuperar el santuario: "+path);SceneManager.SetActiveScene(scene);
    try{
     var roots=scene.GetRootGameObjects();if(roots.Any(r=>r.name=="Santuario original recuperado"))continue;
     string[] prefixes={"Terraza_"+biome,"Cornisa_"+biome,"Subida_"+biome,"Puente_"+biome,"Sendero_entrada_"+biome};
     foreach(var root in roots.Where(r=>prefixes.Any(p=>r.name.StartsWith(p))))Object.DestroyImmediate(root);
     var group=new GameObject("Santuario original recuperado");
     bool Select(MeshFilter m)=>level==4?new[]{"Volcan_caldera","Lava_en_crater","Colada_de_lava","Santuario_3"}.Contains(m.name):level==2?m.name.StartsWith("Islote_elemental_agua")||m.name.StartsWith("Acceso_al_islote")||new[]{"Orillas_laguna_santuario","Laguna_del_santuario","Santuario_4"}.Contains(m.name):prefixes.Any(p=>m.name.StartsWith(p))||m.name=="Santuario_"+biome;
     Vector3 Map(Vector3 point){
      if(level==4){Vector3 delta=point-new Vector3(190,0,230);float r=new Vector2(delta.x,delta.z).magnitude;float radius=r<9?r*2:18+(r-9)*30/39;float k=r>.001f?radius/r:1;return new Vector3(45+delta.x*k*1.2f,point.y,65+delta.z*k*1.2f);}
      if(level==2){var delta=point-new Vector3(133,0,-18);return new Vector3(55+delta.x*1.65f,point.y,82+delta.z*1.65f);}
      return point-Camps[biome]+new Vector3(60,0,45);
     }
     foreach(var mf in meshes.Where(Select)){
      var mesh=Object.Instantiate(mf.sharedMesh);mesh.vertices=mesh.vertices.Select(v=>Map(mf.transform.TransformPoint(v))).ToArray();mesh.RecalculateNormals();mesh.RecalculateBounds();mesh=Save(mesh,level+"_"+mf.name);
      var go=new GameObject(mf.name);go.transform.SetParent(group.transform,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterials=mf.GetComponent<MeshRenderer>().sharedMaterials;
      if(mf.GetComponent<Collider>())go.AddComponent<MeshCollider>().sharedMesh=mesh;
     }
     if(level==4){var mat=group.GetComponentsInChildren<MeshRenderer>().First().sharedMaterial;Bridge(group.transform,new Vector3(45,.15f,5),new Vector3(45,14.7f,44),6,mat);}
     Physics.SyncTransforms();
     var info=roots.SelectMany(r=>r?r.GetComponentsInChildren<CampaignLevel>():Array.Empty<CampaignLevel>()).Single();
     Vector3 fight=level==4?new Vector3(45,14.8f,44):level==2?new Vector3(55,3.25f,82):new Vector3(25,0,95);
     if(level!=4&&level!=2){foreach(var hit in Physics.RaycastAll(fight+Vector3.up*22,Vector3.down,30,1,QueryTriggerInteraction.Ignore))if(hit.collider.transform.IsChildOf(group.transform)&&hit.normal.y>.9f)fight.y=Mathf.Max(fight.y,hit.point.y+.15f);}
     var minion=info.enemies.First(e=>e.name.Contains("Secuaz"));minion.transform.position=fight;PrefabUtility.RecordPrefabInstancePropertyModifications(minion.transform);
     if(level==4){info.guardian.transform.position=new Vector3(45,14.8f,86.5f);PrefabUtility.RecordPrefabInstancePropertyModifications(info.guardian.transform);}
     info.rewardPoint.position=info.guardian.transform.position;
     // Avoid spawning mushrooms and roaming populations underneath the restored arena.
     foreach(var root in roots.Where(r=>r&&(r.GetComponent<MushroomFood>()||r.GetComponent<MushroomGlow>()||r.GetComponent<SlugSpawnPoint>()))){var p=root.transform.position;if((level==4&&Vector2.Distance(new Vector2(p.x,p.z),new Vector2(45,65))<59)||(level==2&&Vector2.Distance(new Vector2(p.x,p.z),new Vector2(55,82))<37)){p.x=-65+Mathf.Repeat(p.x,28);p.z=Mathf.Clamp(p.z,-85,85);p.y=.2f;root.transform.position=p;}}
     RebuildNavigation(scene,level);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
    }finally{if(close)EditorSceneManager.CloseScene(scene,true);}
   }
   AssetDatabase.SaveAssets();
  }finally{if(opened)EditorSceneManager.CloseScene(source,true);if(active.IsValid()&&active.isLoaded)SceneManager.SetActiveScene(active);}
 }
 static void Bridge(Transform parent,Vector3 start,Vector3 end,float width,Material material){Vector3 direction=end-start;var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name="Acceso recto al cráter";go.transform.SetParent(parent,false);go.transform.position=(start+end)*.5f-Vector3.up*.15f;go.transform.rotation=Quaternion.LookRotation(direction);go.transform.localScale=new Vector3(width,.3f,direction.magnitude+1);go.GetComponent<Renderer>().sharedMaterial=material;}
 public static void RebuildNavigation(Scene scene,int level){Physics.SyncTransforms();var sources=new List<NavMeshBuildSource>();NavMeshBuilder.CollectSources(new Bounds(Vector3.zero,new Vector3(250,50,250)),1,NavMeshCollectGeometry.PhysicsColliders,0,new List<NavMeshBuildMarkup>(),sources);sources.RemoveAll(s=>s.component&&(s.component.gameObject.scene!=scene||s.component.GetComponentInParent<WorldDestructible>()||s.component.GetComponentInParent<EnemyCombatant>()||s.component.GetComponentInParent<NexoterraThirdPerson>()||s.component.name=="Techo"));var settings=NavMesh.GetSettingsByIndex(0);settings.agentRadius=.18f;settings.agentHeight=.5f;settings.agentClimb=.35f;settings.agentSlope=45;var data=NavMeshBuilder.BuildNavMeshData(settings,sources,new Bounds(Vector3.zero,new Vector3(250,50,250)),Vector3.zero,Quaternion.identity);if(!data)throw new Exception("No se pudo construir navegación");scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<SlugNavigation>()).Single().data=Save(data,"Navigation_"+level);}
}
