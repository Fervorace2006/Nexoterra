using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace Nexoterra.World
{
 public sealed class FrontEndMenu : MonoBehaviour
 {
  public const string MenuScene="Nexoterra_Menu",WorldScene="Nexoterra_Jardines";
  static FrontEndMenu loader;bool multiplayer,direct;string ip="127.0.0.1",port="7777";string joinCode="";bool loading;float progress;string error;Texture2D artwork,logo;
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
    NexoHud.Backdrop(artwork);NexoHud.Scale();var room=MultiplayerRoom.Open();
    NexoHud.Panel(new Rect(300,60,680,600));
    NexoHud.Label(new Rect(340,84,600,44),"MULTIJUGADOR",30,Color.white,TextAnchor.MiddleCenter);
    NexoHud.Label(new Rect(340,124,600,22),"DUELO DE LANZADORES · HASTA 4 JUGADORES",13,NexoHud.Gold,TextAnchor.MiddleCenter);
    GUI.enabled=!room.Busy&&!room.Connected;
    if(NexoUi.Button(new Rect(345,162,285,44),"INTERNET · CÓDIGO",!direct))direct=false;
    if(NexoUi.Button(new Rect(650,162,285,44),"CONEXIÓN POR IP",direct))direct=true;
    if(direct){
     NexoHud.Label(new Rect(345,224,400,24),"DIRECCIÓN DEL ANFITRIÓN",14,NexoHud.Cyan);
     NexoHud.Label(new Rect(765,224,170,24),"PUERTO UDP",14,NexoHud.Cyan);
     ip=NexoHud.Field(new Rect(345,252,395,44),ip,45);
     port=NexoHud.Field(new Rect(765,252,170,44),port,5);
     if(NexoHud.Button(new Rect(345,320,285,58),"CREAR POR IP"))room.Direct(true,ip,port);
     if(NexoHud.Button(new Rect(650,320,285,58),"CONECTAR"))room.Direct(false,ip,port);
     NexoHud.Label(new Rect(345,398,590,88),"En la misma red: usa la IP local. Desde Internet: IP pública y puerto UDP abierto en el router del anfitrión. Si no puedes abrir puertos, utiliza INTERNET · CÓDIGO.",16,NexoUi.Muted);
    }else{
     NexoHud.Label(new Rect(345,224,590,24),"CÓDIGO DE INVITACIÓN",14,NexoHud.Cyan);
     joinCode=NexoHud.Field(new Rect(345,252,590,44),joinCode,12).ToUpperInvariant();
     if(NexoHud.Button(new Rect(345,320,285,58),"CREAR SALA"))room.Host();
     if(NexoHud.Button(new Rect(650,320,285,58),"UNIRME"))room.Join(joinCode);
     NexoHud.Label(new Rect(345,398,590,65),"Juega desde redes diferentes compartiendo el código de sala. Hasta cuatro jugadores.",17,NexoUi.Muted);
    }
    GUI.enabled=true;
    NexoHud.Fill(new Rect(345,488,590,62),new Color(.02f,.09f,.11f,.9f));NexoHud.Fill(new Rect(345,488,3,62),room.Busy?NexoHud.Gold:NexoHud.Cyan);
    NexoHud.Label(new Rect(360,488,570,62),(room.Busy?"● ":"")+room.Status,16,room.Busy?NexoHud.Gold:NexoHud.Cyan,TextAnchor.MiddleLeft);
    if(NexoHud.Button(new Rect(430,570,420,54),room.Busy?"CANCELAR CONEXIÓN":"VOLVER")){room.Cancel();multiplayer=false;}

   }
   else
   {
    NexoHud.Backdrop(artwork);NexoHud.Scale();
    if(logo)GUI.DrawTexture(new Rect(330,22,620,236),logo,ScaleMode.ScaleToFit);
    NexoHud.Fill(new Rect(400,270,480,1),NexoHud.Gold*new Color(1,1,1,.6f));
    NexoHud.Label(new Rect(400,274,480,24),"EL SUBSUELO TE ESPERA, LANZADOR",13,NexoHud.Cyan,TextAnchor.MiddleCenter);
    if(NexoHud.Button(new Rect(430,310,420,60),"INICIAR HISTORIA"))GameSession.BeginNew();
    GUI.enabled=GameSession.SavedGameExists;if(NexoHud.Button(new Rect(430,385,420,60),"CONTINUAR PARTIDA")){if(!GameSession.BeginSaved(out error))Debug.LogWarning(error);}GUI.enabled=true;
    if(NexoHud.Button(new Rect(430,460,420,60),"MULTIJUGADOR"))multiplayer=true;
    if(NexoHud.Button(new Rect(430,535,420,60),"SALIR")){
#if UNITY_EDITOR
     UnityEditor.EditorApplication.isPlaying=false;
#else
     Application.Quit();
#endif
    }
    NexoHud.Label(new Rect(350,612,580,40),error??"El progreso se guarda desde el menú de pausa.",15,NexoUi.Muted,TextAnchor.MiddleCenter);
    NexoHud.Label(new Rect(24,686,400,22),"NEXOTERRA · DARK WATERS",12,new Color(.4f,.55f,.6f));
   }
   GUI.matrix=old;
  }
 }
}

