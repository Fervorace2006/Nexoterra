#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Nexoterra.World;

public sealed class UpgradeReviewRunner:MonoBehaviour
{
 const string Folder="Documentation/Nexoterra/Upgrade/";
 void Check(bool ok,string name){File.AppendAllText(Folder+"Runtime.txt",(ok?"PASS ":"FAIL ")+name+"\n");if(!ok)throw new Exception(name);}
 void Start(){StartCoroutine(SafeRun());}
 IEnumerator SafeRun(){var routine=Review();while(true){bool next;object step=null;try{next=routine.MoveNext();if(next)step=routine.Current;}catch(Exception e){File.AppendAllText(Folder+"Runtime.txt","FAIL "+e+"\n");EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>(SessionState.GetString("UpgradeReview.Start",""));EditorApplication.isPlaying=false;yield break;}if(!next)yield break;yield return step;}}
 IEnumerator Review(){
  DontDestroyOnLoad(gameObject);Directory.CreateDirectory(Folder);File.WriteAllText(Folder+"Runtime.txt","");yield return new WaitForSeconds(2);
  var player=GameSession.Current.GetComponent<NexoterraThirdPerson>();var inv=player.GetComponent<SlugInventory>();var c=CampaignLevel.Current;
  Check(!inv.catalog.Find("Infierno").IsElemental&&inv.catalog.Find("Infierno").unique,"Infierno única y no elemental");
  Check(inv.catalog.Find("Malvada_Elemental_Energia").id=="Elemental_Energia","Energía no tiene versión malvada");
  var enemy=c.enemies[0];var ai=enemy.GetComponent<EnemyShooter>();ai.enabled=false;enemy.ReceiveDamage(20);
  player.Teleport(enemy.transform.position+new Vector3(0,.2f,-10));yield return new WaitForSeconds(.2f);
  player.enabled=false;player.view.transform.position=player.transform.position+new Vector3(3,4,-5);player.view.transform.LookAt(enemy.transform.position+Vector3.up);
  yield return new WaitForSeconds(.6f);ScreenCapture.CaptureScreenshot(Folder+"HUD_Combat.png");yield return new WaitForSeconds(.4f);
  Check(enemy.portrait&&player.GetComponent<NexoHud>().malePortrait,"retratos ilustrados asignados");
  enemy.ReceiveDamage(10000);var loot=FindObjectsByType<EnemySlugLoot>(FindObjectsSortMode.None).Where(l=>l.key.StartsWith("0:")).ToArray();Check(loot.Length==3,"enemigo libera tres babosas");var positions=loot.Select(l=>l.transform.position).ToArray();
  yield return new WaitForSeconds(9);
  Check(enemy.GetComponentsInChildren<Renderer>().All(r=>!r.enabled),"cuerpo desaparece después de ocho segundos");
  Check(loot.All(l=>l&&l.gameObject.activeInHierarchy),"recompensas sobreviven al cuerpo");
  Check(loot.Where((l,i)=>Vector3.Distance(l.transform.position,positions[i])>.6f).Any(),"babosas liberadas caminan fuera del radio antiguo");
  c.guardian.ReceiveDamage(10000);Check(WildSlug.Active.Any(s=>c.IsReward(s)&&s.species=="Elemental_Energia"),"secuaz suelta Energía limpia");
  var abilities=player.GetComponent<PlayerAbilities>();abilities.dodgeCooldown=0;
  player.enabled=true;player.Teleport(c.entrance.position);yield return new WaitForSeconds(.3f);
  foreach(int side in new[]{1,-1})Check(!abilities.TryDodge(side,Vector3.forward,true),"sin volteretas adelante/atrás "+side);
  player.enabled=false;player.Teleport(new Vector3(-80,.3f,-50));var motor=player.GetComponent<CharacterController>();
  var returning=new GameObject("Prueba regreso corriendo").AddComponent<SlugProjectile>();returning.transform.position=new Vector3(-80,.5f,-68);returning.Launch(inv.catalog.Find("Infierno"),Vector3.forward,inv);returning.BeginReturn();float began=Time.time;
  while(returning&&!returning.HasArrived&&Time.time-began<10){motor.Move((Vector3.forward*player.sprintSpeed+Vector3.down*2)*Time.deltaTime);yield return null;}
  Check(!returning||returning.HasArrived,"babosa alcanza al jugador corriendo a "+player.sprintSpeed+" m/s");
  // Exercise the online pause branch without making or charging for a Relay allocation.
  var arena=new GameObject("Contexto de prueba multijugador").AddComponent<ArenaSession>();arena.enabled=false;
  typeof(GameSession).GetMethod("SetMenu",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(GameSession.Current,new object[]{true});
  Check(GameSession.Blocked&&Time.timeScale==1,"menú multijugador bloquea controles sin pausar la sesión");
  ScreenCapture.CaptureScreenshot(Folder+"Pause_Online.png");yield return new WaitForSeconds(.3f);GameSession.Current.Resume();Destroy(arena.gameObject);
  foreach(string scene in new[]{"Nexoterra_Agua","Nexoterra_Volcan"}){
   SceneManager.LoadScene(scene);yield return new WaitForSeconds(3);c=CampaignLevel.Current;player=GameSession.Current.GetComponent<NexoterraThirdPerson>();player.enabled=false;
   foreach(var e in c.enemies)e.GetComponent<EnemyShooter>().enabled=false;
   bool water=scene=="Nexoterra_Agua";player.view.transform.position=water?new Vector3(14,18,39):new Vector3(-10,23,0);player.view.transform.LookAt(water?new Vector3(55,2,82):new Vector3(45,9,65));
   yield return new WaitForSeconds(.5f);ScreenCapture.CaptureScreenshot(Folder+(water?"Water_Sanctuary.png":"Volcano_Arena.png"));yield return new WaitForSeconds(.4f);
   Check(GameObject.Find("Santuario original recuperado"),"santuario original presente "+scene);
   Check(Physics.Raycast(c.guardian.transform.position+Vector3.up,Vector3.down,3,1,QueryTriggerInteraction.Ignore),"guardián apoyado en santuario "+scene);
   if(water){
    inv=player.GetComponent<SlugInventory>();player.Teleport(new Vector3(55,.3f,41));motor=player.GetComponent<CharacterController>();motor.Move(Vector3.down*.3f);Physics.SyncTransforms();
    var route=new UnityEngine.AI.NavMeshPath();Check(UnityEngine.AI.NavMesh.CalculatePath(new Vector3(55,3.15f,80),player.transform.position,UnityEngine.AI.NavMesh.AllAreas,route)&&route.status==UnityEngine.AI.NavMeshPathStatus.PathComplete,"ruta completa desde islote hasta orilla");
    returning=new GameObject("Prueba regreso desde fuente").AddComponent<SlugProjectile>();returning.transform.position=new Vector3(55,3.5f,80);returning.Launch(inv.catalog.Find("Infierno"),Vector3.back,inv);returning.BeginReturn();began=Time.time;
    while(returning&&!returning.HasArrived&&Time.time-began<18)yield return null;
    Check(!returning||returning.HasArrived,"babosa regresa desde el santuario de agua");
    Check(c.guardian.transform.position.y>2.8f,"secuaz permanece sobre la fuente");
   }
  }
  File.AppendAllText(Folder+"Runtime.txt","COMPLETE\n");EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>(SessionState.GetString("UpgradeReview.Start",""));EditorApplication.isPlaying=false;
 }
}

#endif
