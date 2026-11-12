using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace Nexoterra.World
{
 public sealed class FrontEndMenu : MonoBehaviour
 {
  public const string MenuScene="Nexoterra_Menu",WorldScene="Nexoterra_Jardines";
  static FrontEndMenu loader;bool multiplayer;string joinCode="";bool loading;float progress;string error;Texture2D artwork,logo;
  void Awake(){artwork=Resources.Load<Texture2D>("Nexoterra/Loading");logo=Resources.Load<Texture2D>("Nexoterra/UI/Logo");Time.timeScale=1;Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}
  public static void Load(string scene)
  {
   if(loader&&loader.loading)return;
   if(!loader)loader=FindAnyObjectByType<FrontEndMenu>();
   if(!loader)loader=new GameObject("Transición de escena").AddComponent<FrontEndMenu>();
   DontDestroyOnLoad(loader.gameObject);loader.StartCoroutine(loader.Transition(scene));
  }
  IEnumerator Transition(string scene)
  {
   loading=true;progress=0;Time.timeScale=1;float began=Time.realtimeSinceStartup;yield return null;
   var operation=SceneManager.LoadSceneAsync(scene,LoadSceneMode.Single);if(operation==null){loading=false;yield break;}operation.allowSceneActivation=false;
   while(operation.progress<.9f||Time.realtimeSinceStartup-began<.75f){progress=operation.progress/.9f;yield return null;}
   progress=1;yield return null;operation.allowSceneActivation=true;
   while(!operation.isDone)yield return null;
   if(scene==WorldScene)while(GameSession.Current&&!GameSession.Current.Ready)yield return null;
   loader=null;Destroy(gameObject);
  }
  void OnGUI()
  {
   GUI.depth=-1000;var old=GUI.matrix;GUI.matrix=Matrix4x4.identity;NexoHud.Fill(new Rect(0,0,Screen.width,Screen.height),Color.black);
   if(loading)
   {
    if(artwork)GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),artwork,ScaleMode.ScaleToFit);
    NexoHud.Scale();NexoHud.Fill(new Rect(350,635,580,8),new Color(.12f,.19f,.23f));NexoHud.Fill(new Rect(350,635,580*progress,8),NexoHud.Cyan);
    string dots=new string('.',1+(int)(Time.unscaledTime*3)%3);NexoHud.Label(new Rect(350,660,580,35),"CARGANDO"+dots+"  "+Mathf.RoundToInt(progress*100)+"%",18,Color.white,TextAnchor.MiddleCenter);
   }
   else if(multiplayer)
   {
    NexoHud.Scale();var room=MultiplayerRoom.Open();NexoHud.Label(new Rect(350,100,580,60),"MULTIJUGADOR · DUELO",32,Color.white,TextAnchor.MiddleCenter);
    GUI.enabled=!room.Busy&&!room.Connected;
    if(NexoHud.Button(new Rect(430,225,420,60),"CREAR SALA"))room.Host();
    joinCode=GUI.TextField(new Rect(430,315,420,40),joinCode,12);
    if(NexoHud.Button(new Rect(430,380,420,60),"ENTRAR CON CÓDIGO"))room.Join(joinCode);
    if(NexoHud.Button(new Rect(430,465,420,60),"VOLVER"))multiplayer=false;GUI.enabled=true;
    NexoHud.Label(new Rect(260,555,760,100),room.Status,17,NexoHud.Cyan,TextAnchor.MiddleCenter);
   }
   else
   {
    NexoHud.Scale();if(logo)GUI.DrawTexture(new Rect(300,28,680,238),logo,ScaleMode.ScaleToFit);
    if(NexoHud.Button(new Rect(430,280,420,64),"INICIAR HISTORIA"))GameSession.BeginNew();
    GUI.enabled=GameSession.SavedGameExists;if(NexoHud.Button(new Rect(430,365,420,64),"CONTINUAR PARTIDA")){if(!GameSession.BeginSaved(out error))Debug.LogWarning(error);}GUI.enabled=true;
    if(NexoHud.Button(new Rect(430,450,420,64),"MULTIJUGADOR"))multiplayer=true;
    if(NexoHud.Button(new Rect(430,535,420,64),"SALIR")){
#if UNITY_EDITOR
     UnityEditor.EditorApplication.isPlaying=false;
#else
     Application.Quit();
#endif
    }
    NexoHud.Label(new Rect(350,620,580,60),error??"El progreso se guarda desde el menú de pausa.",16,new Color(.6f,.7f,.75f),TextAnchor.MiddleCenter);
   }
   GUI.matrix=old;
  }
 }
}

