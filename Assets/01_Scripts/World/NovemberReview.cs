#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using Nexoterra.World;

public sealed class NovemberReview : MonoBehaviour
{
 const string Folder="Documentation/Nexoterra/November/";
 [InitializeOnLoadMethod]
 static void Hook(){EditorApplication.update+=Tick;}
 static void Tick(){
  if(EditorApplication.isCompiling||EditorApplication.isUpdating)return;
  if(!EditorApplication.isPlayingOrWillChangePlaymode&&File.Exists("Library/NovemberReview.request")){
   File.Delete("Library/NovemberReview.request");SessionState.SetString("November.PreviousStart",AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
   EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/00_Scenes/Campaign/Nexoterra_Jardines.unity");
   SessionState.SetBool("November.Review",true);EditorApplication.isPaused=false;EditorApplication.isPlaying=true;
  }
  if(EditorApplication.isPlaying&&SessionState.GetBool("November.Review",false)&&GameSession.Current&&GameSession.Current.Ready){
   SessionState.SetBool("November.Review",false);new GameObject("Prueba temporal de noviembre").AddComponent<NovemberReview>();
  }
 }
 void Check(bool condition,string description){File.AppendAllText(Folder+"Checks.txt",(condition?"PASS ":"FAIL ")+description+Environment.NewLine);if(!condition)throw new Exception(description);}
 void Start(){Directory.CreateDirectory(Folder);File.WriteAllText(Folder+"Checks.txt","");DontDestroyOnLoad(gameObject);Application.runInBackground=true;StartCoroutine(SafeRun());}
 IEnumerator SafeRun(){
  var routine=Run();while(true){
   object step=null;bool more;
   try{more=routine.MoveNext();if(more)step=routine.Current;}
   catch(Exception e){File.AppendAllText(Folder+"Checks.txt",e+Environment.NewLine);Finish();yield break;}
   if(!more){Finish();yield break;}yield return step;
  }
 }
 void Finish(){EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>(SessionState.GetString("November.PreviousStart",""));EditorApplication.isPlaying=false;}
 IEnumerator Run(){
  yield return new WaitForSeconds(1);
  var player=GameSession.Current.GetComponent<NexoterraThirdPerson>();var health=player.GetComponent<PlayerHealth>();var ability=player.GetComponent<PlayerAbilities>();var inventory=player.GetComponent<SlugInventory>();
  foreach(var ai in FindObjectsByType<EnemyShooter>(FindObjectsSortMode.None))ai.enabled=false;

  health.Restore(100);health.CorruptionCost();Check(Mathf.Approximately(health.Health,95),"Disparo malvado cuesta exactamente 5 PV");
  foreach(string id in new[]{"Tornado","Malvada_Tornado","Elemental_Aire","Malvada_Elemental_Aire"}){ability.OnSlugLaunched(id);Check(ability.MovementMultiplier>1,"Impulso de viento "+id);}
  Check(!ability.TryDodge(1,Vector3.forward,true)&&!ability.TryDodge(-1,Vector3.forward,true),"Sin volteretas adelante ni atrás");
  var target=new GameObject("Prueba telaraña");var control=target.AddComponent<EnemyControlEffects>();
  foreach(string id in new[]{"Arana","Malvada_Arana"}){control.Apply(id,Vector3.forward);Check(Mathf.Approximately(control.RootRemaining,3)&&!control.CanMove&&control.CanShoot,"Red 3 s permite disparar: "+id);control.Advance(3.01f);Check(control.CanMove,"Red termina: "+id);}
  Destroy(target);
  foreach(var d in inventory.catalog.species.Where(d=>SlugCatalog.BaseSpecies(d.id).StartsWith("Elemental_"))){
   float minimum=SlugCatalog.BaseSpecies(d.id)=="Elemental_Energia"?10:28;
   Check(d.Combat.impactDamage>=minimum,"Daño elemental actualizado: "+d.id);
  }
  var animation=player.GetComponent<NativePlayerAnimation>();
  int jumpLayer=animation.male.GetLayerIndex("JumpLower");
  Check(jumpLayer>0&&animation.male.HasState(jumpLayer,Animator.StringToHash("JumpStart"))&&animation.male.HasState(jumpLayer,Animator.StringToHash("JumpLand"))&&!animation.male.HasState(0,Animator.StringToHash("JumpStart")),"Salto solo en capa de piernas");
  player.GetComponent<CharacterController>().Move(Vector3.up*.01f);
  player.enabled=false;var view=player.view.transform;view.position=player.transform.position+player.transform.forward*3.2f+Vector3.up*1.4f;view.LookAt(player.transform.position+Vector3.up*1f);
  animation.enabled=false;animation.male.SetLayerWeight(jumpLayer,1);animation.male.Play("JumpStart",jumpLayer,.45f);animation.male.Update(0);
  yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Folder+"Jump.png");yield return new WaitForSeconds(.3f);
  view.position=player.transform.position+player.visuals.right*3.4f+Vector3.up*1.1f;view.LookAt(player.transform.position+Vector3.up*.9f);
  foreach(var shot in new[]{("Idle",0,0f),("JumpStart",1,.95f),("JumpFall",1,.0f),("JumpFall",1,.95f),("JumpLand",1,.3f)}){
   animation.male.SetLayerWeight(jumpLayer,shot.Item2);animation.male.Play(shot.Item1=="Idle"?"Empty":shot.Item1,jumpLayer,shot.Item3);animation.male.Update(0);
   yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Folder+"J_"+shot.Item1+"_"+shot.Item3.ToString("0.00")+".png");yield return null;yield return null;}
  animation.male.SetLayerWeight(jumpLayer,0);animation.enabled=true;player.enabled=true;
  Check(animation.male.avatar.isValid&&animation.male.avatar.isHuman,"Avatar válido tras salto");
  var fixture=Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/02_Prefabs/Gameplay/Enemies/Enemy1.prefab")); // plain runtime copy, like the game spawns it
  fixture.transform.position=player.transform.position+player.transform.forward*3;fixture.GetComponent<EnemyShooter>().enabled=false;
  player.enabled=false;player.view.transform.position=fixture.transform.position+fixture.transform.forward*2.2f+fixture.transform.right*.9f+Vector3.up*1.5f;player.view.transform.LookAt(fixture.transform.position+Vector3.up*1.2f);
  yield return new WaitForSeconds(.5f);
  ScreenCapture.CaptureScreenshot(Folder+"EnemyWeapon.png");yield return new WaitForSeconds(.25f);
  var hand=fixture.GetComponentInChildren<Animator>().GetBoneTransform(HumanBodyBones.RightHand);
  if(hand){player.view.transform.position=hand.position+fixture.transform.forward*1.1f+fixture.transform.right*-.4f+Vector3.up*.25f;player.view.transform.LookAt(hand.position);yield return new WaitForSeconds(.2f);ScreenCapture.CaptureScreenshot(Folder+"EnemyHand.png");yield return new WaitForSeconds(.25f);}
  var wind=ImportedSlugVfx.Spawn("AirTornado",fixture.transform,Vector3.zero,.52f,Quaternion.identity,4);
  Check(wind&&wind.GetComponent<UnityEngine.VFX.VisualEffect>(),"Nuevo tornado VFX conectado");
  var effect=wind.GetComponent<UnityEngine.VFX.VisualEffect>();
  Check(effect.HasVector4("core color 1")&&Mathf.Abs(effect.GetVector4("core color 1").x-effect.GetVector4("core color 1").z)<.1f,"Tornado blanco/gris");
  yield return new WaitForSeconds(.6f);ScreenCapture.CaptureScreenshot(Folder+"Tornado.png");yield return new WaitForSeconds(.3f);
  Destroy(fixture);
  var menuMethod=typeof(GameSession).GetMethod("SetMenu",BindingFlags.Instance|BindingFlags.NonPublic);
  menuMethod.Invoke(GameSession.Current,new object[]{true});Check(GameSession.Blocked&&Cursor.visible,"Menú de pausa abre y libera cursor");
  yield return null;ScreenCapture.CaptureScreenshot(Folder+"Pause.png");yield return new WaitForSecondsRealtime(.3f);
  GameSession.Current.Resume();player.enabled=true;
  for(int i=0;i<2;i++){
   var room=MultiplayerRoom.Open();room.Direct(true,"127.0.0.1",(17877+i).ToString());
   float deadline=Time.realtimeSinceStartup+30;
   while(!ArenaSession.Current&&Time.realtimeSinceStartup<deadline)yield return null;
   Check(ArenaSession.Current&&room.Manager&&room.Manager.IsHost,"Anfitrión IP abre arena, ciclo "+i);
   yield return new WaitForSecondsRealtime(2);
   Check(GameSession.Current,"La arena tiene menú de sesión");
   menuMethod.Invoke(GameSession.Current,new object[]{true});
   Check(GameSession.Blocked&&Mathf.Approximately(Time.timeScale,1)&&Cursor.visible,"Pausa online sin detener red");
   room.Leave();
   deadline=Time.realtimeSinceStartup+30;
   while(SceneManager.GetActiveScene().name!=FrontEndMenu.MenuScene&&Time.realtimeSinceStartup<deadline)yield return null;
   yield return new WaitForSecondsRealtime(1);
   Check(SceneManager.GetActiveScene().name==FrontEndMenu.MenuScene&&!MultiplayerRoom.Current,"Salir al menú limpia la sala, ciclo "+i);
  }
  ScreenCapture.CaptureScreenshot(Folder+"Menu.png");yield return new WaitForSecondsRealtime(.3f);
  var front=FindAnyObjectByType<FrontEndMenu>();
  if(front){typeof(FrontEndMenu).GetField("multiplayer",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(front,true);typeof(FrontEndMenu).GetField("direct",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(front,true);yield return new WaitForSecondsRealtime(.3f);ScreenCapture.CaptureScreenshot(Folder+"Multi.png");yield return new WaitForSecondsRealtime(.3f);if(MultiplayerRoom.Current)Destroy(MultiplayerRoom.Current.gameObject);}
 }
}
#endif
