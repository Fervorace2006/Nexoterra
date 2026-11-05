using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEditor;
using UnityEditor.SceneManagement;
using Nexoterra.World;
namespace Nexoterra.Editor
{
 [InitializeOnLoad] public static class NexoterraMapRepair
 {
  const string Folder="Assets/NexoterraWorld/MapRepair";
  static NexoterraMapRepair(){EditorApplication.update+=Poll;}
  static void Poll(){if(EditorApplication.isCompiling||EditorApplication.isUpdating||!File.Exists("Library/MapRepair.request"))return;if(EditorApplication.isPlayingOrWillChangePlaymode){EditorApplication.isPlaying=false;return;}File.Delete("Library/MapRepair.request");try{Build();}catch(Exception e){File.WriteAllText("Library/MapRepair.result.txt","FAIL\n"+e);Debug.LogException(e);}}
  static Material Mat(string name,Color color,bool emission=false){string path=Folder+"/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(!m){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",.12f);if(emission){m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",Color.black);}EditorUtility.SetDirty(m);return m;}
  static T Save<T>(T asset,string path) where T:UnityEngine.Object {var old=AssetDatabase.LoadAssetAtPath<T>(path);if(old){EditorUtility.CopySerialized(asset,old);UnityEngine.Object.DestroyImmediate(asset);EditorUtility.SetDirty(old);return old;}AssetDatabase.CreateAsset(asset,path);return asset;}
  public static void Build()
  {
   AssetDatabase.Refresh();var current=UnityEngine.SceneManagement.SceneManager.GetActiveScene();if(current.path==NexoterraBiomeBuilder.ScenePath&&current.isDirty)EditorSceneManager.SaveScene(current);EditorSceneManager.OpenScene(NexoterraBiomeBuilder.ScenePath);Directory.CreateDirectory(Folder);File.Copy(NexoterraBiomeBuilder.ScenePath,"Library/BeforeMapRepair_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".unity",true);
   var brown=Mat("Caverna_cafe",new Color(.29f,.17f,.09f));var highlighted=Mat("Pico_destructible",new Color(.36f,.24f,.12f),true);int solids=0,roofs=0;
   foreach(var filter in UnityEngine.Object.FindObjectsByType<MeshFilter>())
   {
    string n=filter.name;
    if(n.StartsWith("Techo_")||n.StartsWith("Estalactitas_")||n.StartsWith("Pared_")||n=="Union_bovedas")
    {var r=filter.GetComponent<Renderer>();if(r){r.sharedMaterial=brown;roofs++;}}
    bool deck=(n.StartsWith("Subida_")||n.StartsWith("Puente_")||n.StartsWith("Sendero_entrada_")||n.EndsWith("_cima"))&&!n.EndsWith("_base")&&!n.EndsWith("_barandas");
    if(!deck)continue;var collider=filter.GetComponent<MeshCollider>();if(!collider)collider=filter.gameObject.AddComponent<MeshCollider>();
    var mesh=Solid(filter.sharedMesh,.45f);collider.sharedMesh=Save(mesh,Folder+"/Solid_"+n+".asset");collider.convex=false;collider.isTrigger=false;solids++;
   }
   // Spread 48 additional rocks over six biomes, stable positions on repeated installation.
   var props=UnityEngine.Object.FindObjectsByType<WorldDestructible>();var source=props.First(p=>p.kind==WorldDestructible.Kind.CeilingRock);
   var extras=GameObject.Find("Picos destructibles adicionales");if(!extras)extras=new GameObject("Picos destructibles adicionales");
   Vector3[] camps={new Vector3(275,0,-270),new Vector3(-225,0,5),new Vector3(-210,0,195),new Vector3(190,0,170),new Vector3(105,0,-48),new Vector3(-230,0,-220)};
   Physics.SyncTransforms();
   for(int zone=0;zone<6;zone++)for(int i=0;i<8;i++)
   {
    string name="Pico_extra_"+zone+"_"+i;if(extras.transform.Find(name))continue;
    float angle=i*Mathf.PI/4;Vector3 p=camps[zone]+new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*(16+(i%3)*11);float ground=Ground(p,45);
    var roof=Physics.RaycastAll(new Vector3(p.x,ground+2,p.z),Vector3.up,70,1,QueryTriggerInteraction.Ignore).Where(h=>h.collider.name.StartsWith("Techo_")).OrderBy(h=>h.distance).FirstOrDefault();
    if(!roof.collider)continue;var go=UnityEngine.Object.Instantiate(source.gameObject,extras.transform);go.name=name;go.transform.position=new Vector3(p.x,roof.point.y-.05f,p.z);
   }
   foreach(var prop in UnityEngine.Object.FindObjectsByType<WorldDestructible>())
   {
    if(prop.kind==WorldDestructible.Kind.CeilingRock){if(!prop.GetComponent<DestructibleHighlight>())prop.gameObject.AddComponent<DestructibleHighlight>();foreach(var r in prop.GetComponentsInChildren<Renderer>())r.sharedMaterial=highlighted;prop.blastRadius=3.8f;}
    if(prop.kind==WorldDestructible.Kind.Barrel){prop.blastRadius=8;prop.damage=110;}
   }
   var energy=UnityEngine.Object.FindObjectsByType<SlugSpawnPoint>().First(s=>s.species=="Elemental_Energia");var summit=UnityEngine.Object.FindObjectsByType<MeshFilter>().First(f=>f.name=="Cornisa_0_cima").GetComponent<Renderer>().bounds;
   energy.transform.position=new Vector3(summit.center.x,summit.max.y+.05f,summit.center.z);energy.unique=true;energy.population=1;
   // Common species live below, outside the solid terraces rather than buried inside them.
   foreach(var spawn in UnityEngine.Object.FindObjectsByType<SlugSpawnPoint>().Where(s=>!s.unique&&!s.species.StartsWith("Elemental_")))
    if(TryGroundSpawn(spawn.transform.position,out var position))spawn.transform.position=position;
   Physics.SyncTransforms();BakeNavigation();
   EditorSceneManager.MarkSceneDirty(energy.gameObject.scene);EditorSceneManager.SaveScene(energy.gameObject.scene);AssetDatabase.SaveAssets();File.WriteAllText("Library/MapRepair.result.txt","PASS\nSuperficies sólidas: "+solids+"\nTechos/paredes cafés: "+roofs+"\nPicos adicionales: "+extras.transform.childCount+"\nEnergía en cornisa inicial. Navegación horneada.");
  }
  static bool TryGroundSpawn(Vector3 origin,out Vector3 position)
  {
   position=origin;
   for(int ring=0;ring<9;ring++)for(int i=0;i<(ring==0?1:12);i++)
   {
    float angle=i*Mathf.PI/6;Vector3 p=origin+new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*ring*3;
    var ground=Physics.RaycastAll(new Vector3(p.x,65,p.z),Vector3.down,90,1,QueryTriggerInteraction.Ignore).Where(h=>h.normal.y>.6f&&(h.collider.name.StartsWith("Suelo_")||h.collider.name=="Volcan_caldera"||h.collider.name.StartsWith("Aguja_hielo_"))).OrderByDescending(h=>h.point.y).FirstOrDefault();
    if(!ground.collider||ground.point.y<0)continue;p.y=ground.point.y+.03f;
    bool covered=Physics.RaycastAll(p+Vector3.up*.3f,Vector3.up,30,1,QueryTriggerInteraction.Ignore).Any(h=>h.collider.name.StartsWith("Terraza_")||h.collider.name.StartsWith("Cornisa_")||h.collider.name.StartsWith("Subida_")||h.collider.name.StartsWith("Puente_"));
    if(covered||Physics.CheckSphere(p+Vector3.up*.25f,.16f,1,QueryTriggerInteraction.Ignore))continue;position=p;return true;
   }
   return false;
  }
  static float Ground(Vector3 p,float y){float highest=-10;foreach(var hit in Physics.RaycastAll(new Vector3(p.x,y,p.z),Vector3.down,80,1,QueryTriggerInteraction.Ignore))if(hit.normal.y>.6f&&!hit.collider.name.StartsWith("Techo")&&!hit.collider.name.StartsWith("Estalactitas")&&!hit.collider.GetComponentInParent<WorldDestructible>())highest=Mathf.Max(highest,hit.point.y);return highest;}
  public static Mesh Solid(Mesh source,float depth)
  {
   var vertices=new List<Vector3>();var indices=new Dictionary<Vector3Int,int>();var remap=new int[source.vertexCount];var original=source.vertices;
   for(int i=0;i<original.Length;i++){var p=original[i];var key=Vector3Int.RoundToInt(p*10000);if(!indices.TryGetValue(key,out int index)){index=vertices.Count;indices[key]=index;vertices.Add(p);}remap[i]=index;}
   int count=vertices.Count;vertices.AddRange(vertices.Select(p=>p-Vector3.up*depth).ToArray());var triangles=new List<int>();var edges=new Dictionary<(int,int),(int a,int b,int count)>();var src=source.triangles;
   void Edge(int a,int b){var key=(Mathf.Min(a,b),Mathf.Max(a,b));if(edges.TryGetValue(key,out var e))edges[key]=(e.a,e.b,e.count+1);else edges[key]=(a,b,1);}
   for(int i=0;i<src.Length;i+=3){int a=remap[src[i]],b=remap[src[i+1]],c=remap[src[i+2]];triangles.AddRange(new[]{a,b,c,c+count,b+count,a+count});Edge(a,b);Edge(b,c);Edge(c,a);}
   foreach(var e in edges.Values)if(e.count==1)triangles.AddRange(new[]{e.b,e.a,e.a+count,e.b,e.a+count,e.b+count});
   var mesh=new Mesh{name="Colisión sólida",indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();return mesh;
  }
  public static void BakeNavigation()
  {
   var sources=new List<NavMeshBuildSource>();NavMeshBuilder.CollectSources(new Bounds(new Vector3(0,25,0),new Vector3(642,100,642)),1,NavMeshCollectGeometry.PhysicsColliders,0,new List<NavMeshBuildMarkup>(),sources);
   sources.RemoveAll(s=>s.component&&(s.component.GetComponentInParent<WorldDestructible>()||s.component.GetComponentInParent<EnemyCombatant>()||s.component.GetComponentInParent<NexoterraThirdPerson>()||s.component.GetComponentInParent<WildSlug>()||s.component.name.StartsWith("Techo")||s.component.name.StartsWith("Estalactitas")||s.component.name=="Union_bovedas"));
   var settings=NavMesh.GetSettingsByIndex(0);settings.agentRadius=.18f;settings.agentHeight=.5f;settings.agentClimb=.35f;settings.agentSlope=48;settings.overrideVoxelSize=true;settings.voxelSize=.15f;settings.overrideTileSize=true;settings.tileSize=256;
   var data=NavMeshBuilder.BuildNavMeshData(settings,sources,new Bounds(new Vector3(0,20,0),new Vector3(642,70,642)),Vector3.zero,Quaternion.identity);if(!data)throw new Exception("No se pudo construir navegación");data=Save(data,Folder+"/SlugRoutes.asset");
   var nav=UnityEngine.Object.FindAnyObjectByType<SlugNavigation>();if(!nav)nav=new GameObject("Navegación de babosas").AddComponent<SlugNavigation>();nav.data=data;
  }
 }
}

