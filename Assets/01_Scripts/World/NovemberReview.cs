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
  {// Companion: released with G follows inside the ring and comes back on G. Wild slugs share one size.
   var heights=WildSlug.Active.Where(w=>w&&w.transform.childCount>0).Select(w=>{Bounds bb=default;bool any=false;foreach(var r in w.transform.GetChild(0).GetComponentsInChildren<Renderer>()){if(!r.enabled||r is ParticleSystemRenderer)continue;if(!any){bb=r.bounds;any=true;}else bb.Encapsulate(r.bounds);}return any?bb.size.y:WildSlug.Height;}).ToArray();
   if(heights.Length>0)Check(heights.Max()-heights.Min()<.08f,"Babosas salvajes del mismo tamaño ("+heights.Min().ToString("0.00")+"-"+heights.Max().ToString("0.00")+" m)");
   var player0=GameSession.Current.GetComponent<NexoterraThirdPerson>();var inv0=player0.GetComponent<SlugInventory>();var cc=player0.GetComponent<CorruptionCompanion>();
   inv0.selected=System.Array.FindIndex(inv0.equipped,e=>e>=0);cc.Release();yield return new WaitForSeconds(.3f);var loose=cc.companion;Check(loose,"Babosa soltada con G");
   var start=player0.transform.position;player0.Teleport(start+new Vector3(24,0,0));yield return new WaitForSeconds(7);
   Vector3 gap=player0.transform.position-loose.transform.position;gap.y=0;Check(gap.magnitude<cc.followRadius+.5f,"Babosa sigue dentro del radio ("+gap.magnitude.ToString("0.0")+" m)");
   cc.Recall();float until=Time.time+10;while(cc.companion&&Time.time<until)yield return null;if(cc.companion)File.AppendAllText(Folder+"Checks.txt","INFO babosa en "+cc.companion.transform.position+" jugador "+player0.transform.position+Environment.NewLine);Check(!cc.companion,"Babosa vuelve al inventario con G");
   player0.Teleport(start);yield return new WaitForSeconds(.3f);}

  health.Restore(100);health.CorruptionCost();Check(Mathf.Approximately(health.Health,95),"Disparo malvado cuesta exactamente 5 PV");
  foreach(string id in new[]{"Tornado","Malvada_Tornado","Elemental_Aire","Malvada_Elemental_Aire"}){ability.OnSlugLaunched(id);Check(ability.MovementMultiplier>1,"Impulso de viento "+id);}
  File.AppendAllText(Folder+"Checks.txt","INFO voltereta longitudinal habilitada="+ability.TryDodge(1,Vector3.forward,true)+Environment.NewLine);
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
  {// Menu backdrop: a clean shot of our own map (HUD hidden).
   var hud=new Behaviour[]{player.GetComponent<NexoHud>(),inventory,FindAnyObjectByType<CampaignLevel>(),player};foreach(var h in hud)if(h)h.enabled=false;
   player.view.transform.position=new Vector3(-40,6,-100);player.view.transform.LookAt(new Vector3(15,4,5));yield return new WaitForSeconds(.4f);
   ScreenCapture.CaptureScreenshot("Assets/Resources/Nexoterra/UI/MenuBackground.png");yield return new WaitForSeconds(.4f);foreach(var h in hud)if(h&&h!=player)h.enabled=true;}
  foreach(var shot in new[]{(new Vector3(98,7,-98),new Vector3(125,19,-125),"Corner"),(new Vector3(0,12,-60),new Vector3(-125,22,-20),"Ceiling"),(new Vector3(0,75,-150),new Vector3(0,0,10),"Overview")}){
   player.view.transform.position=shot.Item1;player.view.transform.LookAt(shot.Item2);yield return new WaitForSeconds(.3f);ScreenCapture.CaptureScreenshot(Folder+shot.Item3+".png");yield return new WaitForSeconds(.3f);}
  inventory.SetOpen(true);yield return new WaitForSecondsRealtime(.4f);ScreenCapture.CaptureScreenshot(Folder+"Inventory.png");yield return new WaitForSecondsRealtime(.3f);inventory.SetOpen(false);
  {// Homing: a slug launched 18 degrees off target curves into it within 1.5 s.
   var e=fixture.GetComponent<EnemyCombatant>();var cc=fixture.GetComponent<CharacterController>();cc.enabled=false;fixture.transform.position=player.transform.position+player.transform.forward*12;cc.enabled=true;
   yield return null;float hp0=e.Health;var origin=player.transform.position+Vector3.up*1.3f;
   var go=new GameObject("Prueba teledirigida");go.layer=2;go.transform.position=origin;var shot=go.AddComponent<SlugProjectile>();
   shot.Launch(inventory.catalog.Find("Carnero")??inventory.catalog.Find("Infierno"),Quaternion.Euler(0,18,0)*(fixture.transform.position+Vector3.up-origin).normalized,inventory);shot.HomingTarget=fixture.transform;
   float until=Time.time+2;while(Time.time<until&&e.Health>=hp0)yield return null;
   Check(e.Health<hp0,"Babosa teledirigida alcanza al objetivo");if(go)Destroy(go);}
  Destroy(fixture);
  var menuMethod=typeof(GameSession).GetMethod("SetMenu",BindingFlags.Instance|BindingFlags.NonPublic);
  menuMethod.Invoke(GameSession.Current,new object[]{true});Check(GameSession.Blocked&&Cursor.visible,"Menú de pausa abre y libera cursor");
  yield return null;ScreenCapture.CaptureScreenshot(Folder+"Pause.png");yield return new WaitForSecondsRealtime(.3f);
  {var gfx=typeof(GameSession).GetField("graphics",BindingFlags.Instance|BindingFlags.NonPublic);gfx.SetValue(GameSession.Current,true);yield return null;yield return new WaitForSecondsRealtime(.2f);ScreenCapture.CaptureScreenshot(Folder+"PauseGraphics.png");yield return new WaitForSecondsRealtime(.3f);gfx.SetValue(GameSession.Current,false);}
  GameSession.Current.Resume();player.enabled=true;
  for(int i=0;i<2;i++){
   var room=MultiplayerRoom.Open();room.Direct(true,"127.0.0.1",(17877+i).ToString());
   float deadline=Time.realtimeSinceStartup+30;
   while(!ArenaSession.Current&&Time.realtimeSinceStartup<deadline)yield return null;
   Check(ArenaSession.Current&&room.Manager&&room.Manager.IsHost,"Anfitrión IP abre arena, ciclo "+i);{float wait=Time.realtimeSinceStartup+6;while(room.Status.Contains("abriendo")&&Time.realtimeSinceStartup<wait)yield return null;File.AppendAllText(Folder+"Checks.txt","INFO "+room.Status+Environment.NewLine);}
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
  if(front){typeof(FrontEndMenu).GetField("multiplayer",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(front,true);typeof(FrontEndMenu).GetField("direct",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(front,true);yield return new WaitForSecondsRealtime(.3f);ScreenCapture.CaptureScreenshot(Folder+"Multi.png");yield return new WaitForSecondsRealtime(.3f);typeof(FrontEndMenu).GetField("multiplayer",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(front,false);typeof(FrontEndMenu).GetField("settings",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(front,true);yield return new WaitForSecondsRealtime(.3f);ScreenCapture.CaptureScreenshot(Folder+"MenuSettings.png");yield return new WaitForSecondsRealtime(.3f);typeof(FrontEndMenu).GetField("settings",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(front,false);if(MultiplayerRoom.Current)Destroy(MultiplayerRoom.Current.gameObject);}
 }
}
#endif
