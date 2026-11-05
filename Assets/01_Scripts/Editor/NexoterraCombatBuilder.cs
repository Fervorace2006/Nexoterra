using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Nexoterra.World;
using Geo=Nexoterra.Editor.NexoterraCavernBuilder.Geometry;
namespace Nexoterra.Editor
{
 [InitializeOnLoad]
 public static class NexoterraCombatBuilder
 {
  const string Folder="Assets/NexoterraWorld/Combat";
  static readonly Vector3[] Camps={new Vector3(275,0,-270),new Vector3(-225,0,5),new Vector3(-210,0,195),new Vector3(190,0,170),new Vector3(105,0,-48),new Vector3(-230,0,-220)};
  static NexoterraCombatBuilder(){EditorApplication.update+=Poll;}
  static void Poll(){if(EditorApplication.isCompiling||EditorApplication.isUpdating||!File.Exists("Library/NexoterraCombat.request"))return;if(EditorApplication.isPlayingOrWillChangePlaymode){EditorApplication.isPlaying=false;return;}File.Delete("Library/NexoterraCombat.request");try{Build();}catch(Exception e){File.WriteAllText("Library/NexoterraCombat.result.txt","FAIL\n"+e);Debug.LogException(e);}}
  public static void BatchBuild(){EditorSceneManager.OpenScene(NexoterraBiomeBuilder.ScenePath);Build();}
  [MenuItem("Nexoterra/Combate/Instalar enemigos destructibles y animacion")]
  public static void Build()
  {
   if(UnityEngine.Object.FindAnyObjectByType<RiggedCharacterModel>()){NexoterraRiggedModels.Build();return;}
   var scene=SceneManager.GetActiveScene();if(scene.path!=NexoterraBiomeBuilder.ScenePath||EditorApplication.isPlaying)throw new Exception("Abrir CuatroNodos fuera de Play.");
   EditorSceneManager.SaveScene(scene);File.Copy(scene.path,"Library/NexoterraAntesCombate_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".unity");
   Directory.CreateDirectory(Folder);AssetDatabase.Refresh();NexoterraVfxSetup.Ensure();
   var world=scene.GetRootGameObjects().First(g=>g.name.StartsWith("NEXOTERRA"));RaiseRoof(world.transform);
   if(!world.transform.Find("Objetos destructibles"))Props(world.transform);
   var normalPrefab=EnemyPrefab(false);var bossPrefab=EnemyPrefab(true);
   if(!world.transform.Find("Enemigos y encuentros"))Enemies(world.transform,normalPrefab,bossPrefab);
   var player=UnityEngine.Object.FindAnyObjectByType<NexoterraThirdPerson>();if(!player.GetComponent<PlayerHealth>())player.gameObject.AddComponent<PlayerHealth>();
   var animator=player.GetComponent<PlayerAnimationDriver>();if(!animator)animator=player.gameObject.AddComponent<PlayerAnimationDriver>();animator.male=HumanoidRigBuilder.Build(player.male,"Militar");animator.female=HumanoidRigBuilder.Build(player.female,"Comando");
   player.visuals.name="Personajes con rig provisional";
   AssetDatabase.SaveAssets();Physics.SyncTransforms();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
   var props=UnityEngine.Object.FindObjectsByType<WorldDestructible>();var enemies=UnityEngine.Object.FindObjectsByType<EnemyShooter>();
   if(props.Length!=42||enemies.Length!=11||!animator.male||!animator.female)throw new Exception("Cantidad inesperada o rig ausente: props="+props.Length+" enemies="+enemies.Length+" male="+(bool)animator.male+" female="+(bool)animator.female);
   File.WriteAllText("Library/NexoterraCombat.result.txt","PASS\n42 destructibles: 18 picos, 12 barreras, 12 cilindros.\nReaparicion: 300 segundos.\n11 enemigos normales; 3 encuentros secuenciales con un solo jefe.\nTecho elevado 1.5 m adicional.\nRig provisional de 17 huesos en ambos jugadores y enemigos.\n");
  }
  static float Floor(Vector3 p)
  {
   float result=-2;foreach(var hit in Physics.RaycastAll(new Vector3(p.x,70,p.z),Vector3.down,90,1,QueryTriggerInteraction.Ignore))if(hit.normal.y>.6f&&!hit.collider.name.StartsWith("Techo")&&!hit.collider.name.StartsWith("Estalactitas")&&!hit.collider.name.Contains("boveda")&&!hit.collider.GetComponentInParent<WorldDestructible>())result=Mathf.Max(result,hit.point.y);return result;
  }
  static float Roof(Vector3 p)
  {
   float result=40;foreach(var hit in Physics.RaycastAll(p+Vector3.up*2,Vector3.up,60,1,QueryTriggerInteraction.Ignore))if(hit.collider.name.StartsWith("Techo"))result=Mathf.Min(result,hit.point.y);return result==40?p.y+18:result;
  }
  static void RaiseRoof(Transform world)
  {
   const string marker="Boveda adicional +1.5 m combate";if(world.Find(marker))return;
   foreach(var f in world.GetComponentsInChildren<MeshFilter>(true))
   {
    if(f.name.StartsWith("Techo_")||f.name.StartsWith("Estalactitas_")||f.name=="Union_bovedas")f.transform.position+=Vector3.up*1.5f;
    if(f.name=="Pared_exterior_640"){float old=f.transform.localScale.y,delta=1.5f/f.sharedMesh.bounds.size.y;var scale=f.transform.localScale;scale.y+=delta;f.transform.localScale=scale;f.transform.localPosition-=Vector3.up*(f.sharedMesh.bounds.min.y*delta);}
   }
   new GameObject(marker).transform.SetParent(world,false);Physics.SyncTransforms();
  }
  static Material Material(string name,Color color)
  {
   string path=Folder+"/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(m)return m;m=new Material(Shader.Find("Nexoterra/Cavern Surface"));m.SetColor("_BaseColor",color);m.SetFloat("_Strata",.8f);AssetDatabase.CreateAsset(m,path);return m;
  }
  static void Props(Transform world)
  {
   var group=new GameObject("Objetos destructibles").transform;group.SetParent(world,false);
   var rock=Material("Piedra_fragil",new Color(.58f,.4f,.22f));var barrel=Material("Cilindro_explosivo",new Color(.48f,.085f,.04f));var band=Material("Advertencia_cilindro",new Color(1,.65f,.04f));
   for(int zone=0;zone<6;zone++)for(int i=0;i<7;i++)
   {
    Vector3 p=Camps[zone]+(i<3?new Vector3(-18+i*23,0,40):i<5?new Vector3(-35+(i-3)*57,0,-23):new Vector3(-42+(i-5)*83,0,67));p.y=Floor(p)+.03f;
    var kind=i<3?WorldDestructible.Kind.CeilingRock:i<5?WorldDestructible.Kind.RockBarrier:WorldDestructible.Kind.Barrel;
    var go=new GameObject(kind+"_"+zone+"_"+i);go.transform.SetParent(group,false);
    float length=i==1?4.3f:2.8f;
    if(kind==WorldDestructible.Kind.CeilingRock){float roof=Roof(p);length=Mathf.Min(length,roof-p.y-3);p.y=roof-.12f;}
    go.transform.position=p;
    if(kind==WorldDestructible.Kind.Barrel)
    {
     var body=GameObject.CreatePrimitive(PrimitiveType.Cylinder);body.transform.SetParent(go.transform,false);body.transform.localPosition=Vector3.up*.9f;body.transform.localScale=new Vector3(1.2f,.9f,1.2f);body.GetComponent<Renderer>().sharedMaterial=barrel;
     foreach(float y in new[]{.2f,1.6f}){var stripe=GameObject.CreatePrimitive(PrimitiveType.Cylinder);stripe.transform.SetParent(go.transform,false);stripe.transform.localPosition=Vector3.up*y;stripe.transform.localScale=new Vector3(1.23f,.08f,1.23f);stripe.GetComponent<Renderer>().sharedMaterial=band;UnityEngine.Object.DestroyImmediate(stripe.GetComponent<Collider>());}
    }
    else
    {
     var geo=new Geo();if(kind==WorldDestructible.Kind.CeilingRock)geo.Spire(Vector3.zero,.85f,length,Color.white,true);else for(int n=-1;n<=1;n++)geo.Spire(new Vector3(n*.7f,0,0),.65f,1.7f+.3f*(n+1),Color.white,false);
     var mesh=geo.Mesh();AssetDatabase.CreateAsset(mesh,Folder+"/"+go.name+".asset");go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=rock;go.AddComponent<MeshCollider>().sharedMesh=mesh;
    }
    var prop=go.AddComponent<WorldDestructible>();prop.kind=kind;prop.rockLength=length;prop.blastRadius=kind==WorldDestructible.Kind.Barrel?6:2.8f;prop.damage=kind==WorldDestructible.Kind.Barrel?85:35;prop.respawnSeconds=300;
   }
  }
  static GameObject EnemyPrefab(bool boss)
  {
   string name=boss?"SirBlack":"Enemy1";var go=new GameObject(name);var visual=new GameObject("Visual").transform;visual.SetParent(go.transform,false);
   var asset=AssetDatabase.LoadAssetAtPath<GameObject>(boss?"Assets/02_Prefabs/Boss_Sir_Black.prefab":"Assets/02_Prefabs/Enemy1.prefab");if(!asset)throw new Exception("Falta prefab "+name);
   var model=(GameObject)PrefabUtility.InstantiatePrefab(asset);PrefabUtility.UnpackPrefabInstance(model,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);model.transform.SetParent(visual,false);model.transform.localPosition=Vector3.zero;model.transform.localScale=Vector3.one;
   var renderers=model.GetComponentsInChildren<Renderer>();var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);float scale=(boss?2.2f:1.9f)/bounds.size.y;model.transform.localScale=Vector3.one*scale;model.transform.localPosition=new Vector3(-bounds.center.x,-bounds.min.y,-bounds.center.z)*scale;
   foreach(var c in model.GetComponentsInChildren<Collider>())UnityEngine.Object.DestroyImmediate(c);
   var health=go.AddComponent<EnemyCombatant>();health.displayName=boss?"Sir Black":"Enemy 1";health.maxHealth=boss?420:100;health.visualRoot=visual;
   var cc=go.AddComponent<CharacterController>();cc.height=2;cc.center=Vector3.up;cc.radius=.4f;cc.slopeLimit=45;cc.stepOffset=.3f;
   var shooter=go.AddComponent<EnemyShooter>();shooter.boss=boss;shooter.catalog=AssetDatabase.LoadAssetAtPath<SlugCatalog>("Assets/NexoterraWorld/Slugs/Catalog.asset");shooter.shotCooldown=boss?2.4f:3.5f;shooter.pose=HumanoidRigBuilder.Build(visual.gameObject,name+"_Upright");shooter.model=shooter.pose.transform;
   var mount=new GameObject("Blaster").transform;mount.SetParent(visual,false);mount.localPosition=new Vector3(.2f,1.28f,.35f);shooter.weapon=mount;
   var weaponAsset=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/03_Models/Blast/Blast.fbx");var weapon=UnityEngine.Object.Instantiate(weaponAsset);weapon.transform.SetParent(mount,false);weapon.transform.localPosition=Vector3.zero;weapon.transform.localRotation=Quaternion.identity;weapon.transform.localScale=Vector3.one;
   var wr=weapon.GetComponentsInChildren<Renderer>();var wb=wr[0].bounds;foreach(var r in wr)wb.Encapsulate(r.bounds);float ws=.34f/wb.size.y;Vector3 localCenter=mount.InverseTransformPoint(wb.center);weapon.transform.localScale=Vector3.one*ws;weapon.transform.localPosition=new Vector3(-localCenter.x,-(localCenter.y-wb.size.y*.5f),-localCenter.z)*ws;
   foreach(var collider in weapon.GetComponentsInChildren<Collider>())UnityEngine.Object.DestroyImmediate(collider);
   var saved=PrefabUtility.SaveAsPrefabAsset(go,Folder+"/"+name+".prefab");UnityEngine.Object.DestroyImmediate(go);return saved;
  }
  static void Enemies(Transform world,GameObject normal,GameObject boss)
  {var group=new GameObject("Enemigos y encuentros").transform;group.SetParent(world,false);
   for(int zone=0;zone<6;zone++)for(int n=0;n<(zone==0?1:2);n++)
   {
    Vector3 p=Camps[zone]+(n==0?new Vector3(zone==0?30:45,0,zone==0?60:40):new Vector3(-55,0,-15));p.y=Floor(p)+.1f;var enemy=(GameObject)PrefabUtility.InstantiatePrefab(normal);enemy.transform.SetParent(group,false);enemy.transform.position=p;
   }
   var encounters=group.gameObject.AddComponent<BossEncounters>();encounters.bossPrefab=boss;encounters.encounterPoints=new Transform[3];Vector3[] points={new Vector3(-290,0,-290),new Vector3(-190,0,-285),new Vector3(-285,0,-160)};
   for(int i=0;i<3;i++){var marker=new GameObject("Sir Black · encuentro "+(i+1)).transform;marker.SetParent(group,false);Vector3 p=points[i];p.y=Floor(p)+.1f;marker.position=p;encounters.encounterPoints[i]=marker;}
  }
 }
}
