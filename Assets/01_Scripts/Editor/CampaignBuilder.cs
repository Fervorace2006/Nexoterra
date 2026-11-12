using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Nexoterra.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class CampaignBuilder
{
 const string Folder="Assets/NexoterraWorld/Campaign";
 const string Source="Assets/00_Scenes/Nexoterra_CuatroNodos.unity";
 static readonly Vector3[] Camps={new Vector3(275,0,-270),new Vector3(-225,0,5),new Vector3(-210,0,195),new Vector3(190,0,170),new Vector3(105,0,-48),new Vector3(-230,0,-220)};
 static readonly Color[] Colors={new Color(.30f,.45f,.22f),new Color(.58f,.78f,.82f),new Color(.57f,.40f,.23f),new Color(.31f,.14f,.10f),new Color(.18f,.43f,.39f),new Color(.25f,.12f,.16f)};
 static CampaignBuilder(){EditorApplication.update+=Poll;}
 static void Poll(){
  if(File.Exists("Library/CampaignMenu.request")&&!EditorApplication.isPlayingOrWillChangePlaymode&&!EditorApplication.isCompiling&&!EditorApplication.isUpdating){File.Delete("Library/CampaignMenu.request");UseMenu();File.WriteAllText("Library/CampaignMenu.result","PASS");}
  if(!File.Exists("Library/CampaignBuild.request")||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
  File.Delete("Library/CampaignBuild.request");
  try{Build();File.WriteAllText("Library/CampaignBuild.result","PASS");}catch(Exception e){File.WriteAllText("Library/CampaignBuild.result",e.ToString());Debug.LogException(e);}
 }
 [MenuItem("Nexoterra/Historia/Iniciar Play desde el menú")]
 static void UseMenu(){EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/00_Scenes/Nexoterra_Menu.unity");}
 static T Save<T>(T value,string name) where T:Object{
  string path=Folder+"/"+name;var old=AssetDatabase.LoadAssetAtPath<T>(path);
  if(old){EditorUtility.CopySerialized(value,old);Object.DestroyImmediate(value);EditorUtility.SetDirty(old);return old;}
  AssetDatabase.CreateAsset(value,path);return value;
 }
 static Material Mat(string name,Color color){
  var m=new Material(Shader.Find("Universal Render Pipeline/Lit"));m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",.15f);
  return Save(m,name+".mat");
 }
 static float Height(float x,float z,int biome){
  float edge=Mathf.Clamp01((125-Mathf.Max(Mathf.Abs(x),Mathf.Abs(z)))/12);
  float h=(Mathf.PerlinNoise(x*.025f+40,z*.025f+17)-.5f)*1.4f*edge;
  // Dry entrance, encounter clearings and sanctuary landing.
  if(z>55||z< -92)h=0;
  if((biome==4||biome==5)&&Vector2.Distance(new Vector2(x,z),new Vector2(-65,8))<27){
   float d=Vector2.Distance(new Vector2(x,z),new Vector2(-65,8));h-=2.6f*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(17,27,d)));
  }
  return h;
 }
 static GameObject MeshObject(string name,Mesh mesh,Material material,bool collision){
  var go=new GameObject(name);go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=material;
  if(collision)go.AddComponent<MeshCollider>().sharedMesh=mesh;return go;
 }
 static Mesh Ground(int biome){
  int n=125;var v=new Vector3[(n+1)*(n+1)];var uv=new Vector2[v.Length];var triangles=new List<int>();
  for(int z=0;z<=n;z++)for(int x=0;x<=n;x++){int k=z*(n+1)+x;float px=x*2-125,pz=z*2-125;v[k]=new Vector3(px,Height(px,pz,biome),pz);uv[k]=new Vector2(px*.1f,pz*.1f);if(x<n&&z<n)triangles.AddRange(new[]{k,k+n+1,k+1,k+1,k+n+1,k+n+2});}
  var m=new Mesh();m.vertices=v;m.colors=Enumerable.Repeat(Color.white,v.Length).ToArray();m.uv=uv;m.SetTriangles(triangles,0);m.RecalculateNormals();m.RecalculateBounds();return m;
 }
 static GameObject Cube(string name,Vector3 p,Vector3 scale,Material material,bool collider=true){
  var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.position=p;go.transform.localScale=scale;go.GetComponent<Renderer>().sharedMaterial=material;if(!collider)Object.DestroyImmediate(go.GetComponent<Collider>());return go;
 }
 static Mesh ShiftMesh(Mesh original,Transform source,Vector3 offset){
  var mesh=Object.Instantiate(original);mesh.vertices=original.vertices.Select(v=>source.TransformPoint(v)+offset).ToArray();mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
 }
 public static void Build(){
  Directory.CreateDirectory(Folder);Directory.CreateDirectory("Assets/00_Scenes/Campaign");
  var originalActive=SceneManager.GetActiveScene();var source=SceneManager.GetSceneByPath(Source);bool opened=!source.isLoaded;if(opened)source=EditorSceneManager.OpenScene(Source,OpenSceneMode.Additive);
  var objects=source.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).ToArray();
  var player=objects.Select(t=>t.GetComponent<NexoterraThirdPerson>()).First(p=>p);
  var catalog=player.GetComponent<SlugInventory>().catalog;

  var meshes=objects.Select(t=>t.GetComponent<MeshFilter>()).Where(m=>m&&m.sharedMesh).ToArray();
  var mushrooms=objects.Select(t=>t.GetComponent<MushroomFood>()).Where(m=>m).ToArray();
  var props=objects.Select(t=>t.GetComponent<WorldDestructible>()).Where(m=>m).ToArray();
  var spawns=objects.Select(t=>t.GetComponent<SlugSpawnPoint>()).Where(m=>m).ToArray();
  var enemyPrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/02_Prefabs/Gameplay/Enemies/Enemy1.prefab");
  var bossPrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/02_Prefabs/Gameplay/Enemies/SirBlack.prefab");
  var roof=AssetDatabase.LoadAssetAtPath<Material>("Assets/NexoterraWorld/MapRepair/Techo_cafe_claro.mat");
  var black=Mat("TunnelDark",new Color(.004f,.004f,.006f));
  var generated=new List<string>();
  for(int level=0;level<CampaignLevel.Scenes.Length;level++){
   int biome=new[]{0,1,4,2,3,5}[level];
   var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);SceneManager.SetActiveScene(scene);
   try{
    var info=new GameObject("Objetivo de historia").AddComponent<CampaignLevel>();info.index=level;info.catalog=catalog;
    info.introduction=level==0?"Llegaste a Nexoterra. Recoge hongos con E y alimenta a Doc con F. Derrota a Sir Black al fondo, cura la Energía con Doc y recupérala. M abre el mapa.":level==5?"Sir Black protege el acceso al corazón oscuro. Derrota a su patrulla y cruza el túnel final.":level==6?"Las cinco elementales están a salvo. Derrota al verdadero Sir Black para liberar Nexoterra.":"Sir Black retiene la elemental de este bioma. Derrota a todos, purifica su recompensa con Doc o Energía y busca el túnel.";
    RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.56f,.59f,.65f);RenderSettings.ambientEquatorColor=new Color(.48f,.44f,.40f);RenderSettings.ambientGroundColor=new Color(.36f,.32f,.29f);
    RenderSettings.fog=true;RenderSettings.fogColor=new Color(.12f,.14f,.16f);RenderSettings.fogMode=FogMode.ExponentialSquared;RenderSettings.fogDensity=.004f;
    var light=new GameObject("Luz de caverna").AddComponent<Light>();light.type=LightType.Directional;light.intensity=.85f;light.shadows=LightShadows.Soft;light.transform.rotation=Quaternion.Euler(55,-35,0);
    var floor=AssetDatabase.LoadAssetAtPath<Material>("Assets/NexoterraWorld/Habitats/Materials/Superficie_"+biome+".mat");MeshObject("Suelo del bioma",Save(Ground(biome),"Ground_"+biome+".asset"),floor,true);
    Cube("Techo",new Vector3(0,25,0),new Vector3(250,2,250),roof);
    Cube("Pared oeste",new Vector3(-126,12,0),new Vector3(2,26,250),roof);
    Cube("Pared este",new Vector3(126,12,0),new Vector3(2,26,250),roof);
    Cube("Pared entrada",new Vector3(0,12,-126),new Vector3(250,26,2),roof);
    Cube("Pared salida izquierda",new Vector3(-24,12,126),new Vector3(202,26,2),roof);
    Cube("Pared salida derecha",new Vector3(104,12,126),new Vector3(42,26,2),roof);
    Cube("Arco sobre túnel",new Vector3(80,16,126),new Vector3(6,18,2),roof);
    Cube("Fondo oscuro del túnel",new Vector3(80,3,125),new Vector3(6,6,.25f),black,false);
    Cube("Túnel izquierdo",new Vector3(76,3,120),new Vector3(2,6,10),roof);
    Cube("Túnel derecho",new Vector3(84,3,120),new Vector3(2,6,10),roof);
    Cube("Túnel techo",new Vector3(80,6.5f,120),new Vector3(10,1,10),roof);
    info.exit=new GameObject("Salida al próximo bioma").transform;info.exit.position=new Vector3(80,.2f,122);
    info.entrance=new GameObject("Entrada").transform;info.entrance.position=new Vector3(-85,.3f,-110);
    // Preserve the rig, weapon attachments, animations and input components from the working scene.
    var avatar=Object.Instantiate(player.gameObject);avatar.name="Jugador";avatar.transform.SetParent(null);avatar.transform.position=info.entrance.position;
    var hero=avatar.GetComponent<NexoterraThirdPerson>();hero.spawn=info.entrance.position;hero.travelPoints=Array.Empty<Transform>();hero.food=Mathf.Max(5,hero.food);
    var camera=Object.Instantiate(player.view.gameObject);camera.transform.SetParent(null);hero.view=camera.GetComponent<Camera>();
    var native=avatar.GetComponent<NativePlayerAnimation>();if(native)native.weapon=native.maleMuzzle?native.maleMuzzle.parent:native.weapon;
    if(level<5){
     Vector3 destination=new Vector3(82,0,64),offset=destination-Camps[biome];
     foreach(var mf in meshes.Where(m=>new[]{"Terraza_"+biome,"Cornisa_"+biome,"Subida_"+biome,"Puente_"+biome,"Sendero_entrada_"+biome}.Any(prefix=>m.name.StartsWith(prefix)))){
      var mesh=Save(ShiftMesh(mf.sharedMesh,mf.transform,offset),"Level"+biome+"_"+mf.name+".asset");
      var go=MeshObject(mf.name,mesh,mf.GetComponent<Renderer>().sharedMaterial,false);
      var col=mf.GetComponent<MeshCollider>();if(col){var target=go.AddComponent<MeshCollider>();target.sharedMesh=col.sharedMesh==mf.sharedMesh?mesh:Save(ShiftMesh(col.sharedMesh,mf.transform,offset),"Collider"+biome+"_"+mf.name+".asset");}
     }
    }
    // Place decorations and food without scaling character or creature rigs.
    var random=new System.Random(1147+biome);
    for(int i=0;i<100;i++){
     float x=(float)random.NextDouble()*228-114,z=(float)random.NextDouble()*210-105;if(z>45&&x>30||Height(x,z,biome)<-.2f)continue;
     var food=Object.Instantiate(mushrooms[i%mushrooms.Length].gameObject);food.transform.SetParent(null);food.transform.position=new Vector3(x,Height(x,z,biome),z);
    }
    var trees=meshes.Where(m=>m.name.StartsWith("Hongo_tallo_"+biome+"_")&&m.transform.parent&&m.transform.parent.name=="Hongo arboreo").Select(m=>m.transform.parent).Distinct().ToArray();
    for(int i=0;i<45&&trees.Length>0;i++){
     float x=(float)random.NextDouble()*222-111,z=(float)random.NextDouble()*210-105;if(z>45&&x>30||Height(x,z,biome)<-.2f)continue;
     var tree=Object.Instantiate(trees[i%trees.Length].gameObject);tree.transform.SetParent(null);tree.transform.position=new Vector3(x,Height(x,z,biome),z);
    }
    var rocks=props.Where(p=>p.kind==WorldDestructible.Kind.RockBarrier).ToArray();
    for(int i=0;i<28&&rocks.Length>0;i++){
     float x=(float)random.NextDouble()*220-110,z=(float)random.NextDouble()*200-100;if(z>45&&x>30||Height(x,z,biome)<-.2f)continue;
     var rock=Object.Instantiate(rocks[i%rocks.Length].gameObject);rock.transform.SetParent(null);rock.transform.position=new Vector3(x,Height(x,z,biome),z);
    }
    for(int i=0;i<16;i++){var sourceProp=props.Where(p=>p.kind==WorldDestructible.Kind.CeilingRock).ElementAt(i%props.Count(p=>p.kind==WorldDestructible.Kind.CeilingRock));var prop=Object.Instantiate(sourceProp.gameObject);prop.transform.SetParent(null);prop.transform.position=new Vector3((float)random.NextDouble()*210-105,24,(float)random.NextDouble()*200-100);}
    if(biome==4||biome==5){
     var mesh=new Mesh();var verts=new List<Vector3>{new Vector3(-65,-.35f,8)};var tris=new List<int>();
     for(int i=0;i<64;i++){float angle=i*Mathf.PI/32;verts.Add(new Vector3(-65+Mathf.Cos(angle)*25,-.35f,8+Mathf.Sin(angle)*25));tris.AddRange(new[]{0,(i+1)%64+1,i+1});}
     mesh.SetVertices(verts);mesh.SetUVs(0,verts.Select(v=>new Vector2(v.x*.05f,v.z*.05f)).ToList());mesh.SetTriangles(tris,0);mesh.RecalculateNormals();
     var material=AssetDatabase.LoadAssetAtPath<Material>(biome==5?"Assets/NexoterraWorld/Biomes/Materials/Agua_sucia_carmesi.mat":"Assets/NexoterraWorld/Biomes/Materials/Agua_azul.mat");
     MeshObject("Lago del bioma",Save(mesh,"Lake_"+biome+".asset"),material,false);
    }
    var habitat=spawns.First(s=>s.localSpecies!=null&&s.localSpecies.Length>0&&s.biome==new[]{"Jardines del Nexo","Cavernas de hielo","Tierras antiguas","Caldera volcanica","Galeria acuatica","Cienaga carmesi"}[biome]);
    for(int i=0;i<10;i++){
     float x=-92+(i%5)*43,z=-83+(i/5)*114;var spawn=new GameObject("Babosas del bioma").AddComponent<SlugSpawnPoint>();
     spawn.transform.position=new Vector3(x,Height(x,z,biome),z);spawn.species=habitat.species;spawn.biome=habitat.biome;spawn.creaturePrefab=habitat.creaturePrefab;spawn.localSpecies=habitat.localSpecies;spawn.population=3;spawn.activeDistance=135;
    }
    // Guarantee access to the purifier before the first guardian.
    if(level==0){var spawn=new GameObject("Doc junto a la entrada").AddComponent<SlugSpawnPoint>();spawn.species="Gon_Doc";spawn.biome=habitat.biome;spawn.creaturePrefab=spawns.First(s=>s.species=="Gon_Doc").creaturePrefab;spawn.transform.position=info.entrance.position+new Vector3(4,0,5);spawn.population=1;}
    info.rewardPoint=new GameObject("Recompensa del guardián").transform;CampaignEnemyInstaller.ConfigureScene(info);
    BakeNavigation(level);
    string path="Assets/00_Scenes/Campaign/"+CampaignLevel.Scenes[level]+".unity";EditorSceneManager.SaveScene(scene,path);generated.Add(path);
   }finally{EditorSceneManager.CloseScene(scene,true);}
  }
  CampaignWaterZones.Build();BuildArena(generated[0]);generated.Add("Assets/00_Scenes/Campaign/Nexoterra_Arena.unity");
  var builds=new List<EditorBuildSettingsScene>{new EditorBuildSettingsScene("Assets/00_Scenes/Nexoterra_Menu.unity",true),new EditorBuildSettingsScene(Source,true)};
  builds.AddRange(generated.Select(p=>new EditorBuildSettingsScene(p,true)));EditorBuildSettings.scenes=builds.ToArray();
  AssetDatabase.SaveAssets();if(originalActive.IsValid()&&originalActive.isLoaded)SceneManager.SetActiveScene(originalActive);if(opened)EditorSceneManager.CloseScene(source,true);
 }
 static void BuildArena(string garden){
  var scene=EditorSceneManager.OpenScene(garden,OpenSceneMode.Additive);SceneManager.SetActiveScene(scene);
  var roots=scene.GetRootGameObjects();foreach(var root in roots){foreach(var enemy in root.GetComponentsInChildren<EnemyCombatant>())Object.DestroyImmediate(enemy.gameObject);if(!root)continue;foreach(var spawn in root.GetComponentsInChildren<SlugSpawnPoint>())Object.DestroyImmediate(spawn.gameObject);if(!root)continue;foreach(var food in root.GetComponentsInChildren<MushroomFood>())Object.DestroyImmediate(food.gameObject);if(!root)continue;foreach(var campaign in root.GetComponentsInChildren<CampaignLevel>())Object.DestroyImmediate(campaign.gameObject);}
  new GameObject("Arena multijugador").AddComponent<ArenaSession>();
  EditorSceneManager.SaveScene(scene,"Assets/00_Scenes/Campaign/Nexoterra_Arena.unity");EditorSceneManager.CloseScene(scene,true);
 }
 static void BakeNavigation(int level){
  Physics.SyncTransforms();var sources=new List<NavMeshBuildSource>();
  NavMeshBuilder.CollectSources(new Bounds(Vector3.zero,new Vector3(250,50,250)),1,NavMeshCollectGeometry.PhysicsColliders,0,new List<NavMeshBuildMarkup>(),sources);
  sources.RemoveAll(s=>s.component&&(s.component.gameObject.scene!=SceneManager.GetActiveScene()||s.component.GetComponentInParent<WorldDestructible>()||s.component.GetComponentInParent<EnemyCombatant>()||s.component.GetComponentInParent<NexoterraThirdPerson>()||s.component.name=="Techo"));
  var settings=NavMesh.GetSettingsByIndex(0);settings.agentRadius=.18f;settings.agentHeight=.5f;settings.agentClimb=.35f;settings.agentSlope=45;
  var data=NavMeshBuilder.BuildNavMeshData(settings,sources,new Bounds(Vector3.zero,new Vector3(250,50,250)),Vector3.zero,Quaternion.identity);
  if(!data)throw new Exception("Navegación "+level);new GameObject("Navegación").AddComponent<SlugNavigation>().data=Save(data,"Navigation_"+level+".asset");
 }
 static void AddEnergyVariant(SlugCatalog catalog){
  if(catalog.Find("Malvada_Elemental_Energia")!=null)return;
  var original=catalog.Find("Elemental_Energia");var normal=Object.Instantiate(original.normal);normal.name="Energía malvada";var aura=normal.AddComponent<CorruptionConversionVfx>();Object.DestroyImmediate(aura);
  foreach(var r in normal.GetComponentsInChildren<Renderer>()){var materials=r.sharedMaterials;for(int i=0;i<materials.Length;i++){var m=Object.Instantiate(materials[i]);if(m.HasProperty("_BaseColor"))m.SetColor("_BaseColor",new Color(.85f,.1f,.23f));materials[i]=Save(m,"EnergyCorrupt_"+r.name+"_"+i+".mat");}r.sharedMaterials=materials;}
  var prefab=PrefabUtility.SaveAsPrefabAsset(normal,Folder+"/Malvada_Elemental_Energia.prefab");Object.DestroyImmediate(normal);
  var variant=new SlugDefinition{id="Malvada_Elemental_Energia",displayName="Energía malvada",normal=prefab,transformed=original.transformed,portrait=original.portrait,unique=true,accent=Color.red,combat=SlugCombatProfile.Default(original.id),combatConfigured=true};
  catalog.species=catalog.species.Concat(new[]{variant}).ToArray();EditorUtility.SetDirty(catalog);
 }
}






