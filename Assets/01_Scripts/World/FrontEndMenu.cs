using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace Nexoterra.World
{
 public sealed class FrontEndMenu : MonoBehaviour
 {
  public const string MenuScene="Nexoterra_Menu",WorldScene="Nexoterra_Jardines";
  static FrontEndMenu loader;bool multiplayer,direct,settings;string ip="127.0.0.1",port="7777";string joinCode="";bool loading;float progress;string error;Texture2D artwork,logo,logoCutout,menuBackground;
  void Awake(){artwork=Resources.Load<Texture2D>("Nexoterra/Loading");logo=Resources.Load<Texture2D>("Nexoterra/UI/Logo");logoCutout=Resources.Load<Texture2D>("Nexoterra/UI/Logo_Cutout");menuBackground=Resources.Load<Texture2D>("Nexoterra/UI/MenuBackground");Time.timeScale=1;Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}
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
    // Logo art at ~half size, centered slightly above the progress bar.
    if(artwork){float w=Screen.width*.5f,h=Screen.height*.42f;GUI.DrawTexture(new Rect((Screen.width-w)*.5f,Screen.height*.43f-h*.5f,w,h),artwork,ScaleMode.ScaleToFit);}
    NexoHud.Scale();NexoHud.Fill(new Rect(350,635,580,8),new Color(.12f,.19f,.23f));NexoHud.Fill(new Rect(350,635,580*progress,8),NexoHud.Cyan);
    string dots=new string('.',1+(int)(Time.unscaledTime*3)%3);NexoHud.Label(new Rect(350,660,580,35),"CARGANDO"+dots+"  "+Mathf.RoundToInt(progress*100)+"%",18,Color.white,TextAnchor.MiddleCenter);
   }
   else if(settings)
   {
    NexoHud.Backdrop(null);NexoHud.Scale();NexoUi.BeginNav();
    var panel=new Rect(344,48,592,624);NexoUi.Panel(panel);
    if(GameplayOptions.DrawSettingsPage(new Rect(panel.x+40,panel.y+28,panel.width-80,panel.height-56))){settings=false;NexoUi.ResetFocus();}
    NexoUi.EndNav();
   }
   else if(multiplayer)
   {
    NexoHud.Backdrop(null);NexoHud.Scale();NexoUi.BeginNav();var room=MultiplayerRoom.Open();
    var panel=new Rect(312,48,656,624);NexoUi.Panel(panel);
    float x=panel.x+40,w=panel.width-80,y=panel.y+28;
    NexoUi.Title(new Rect(x,y,w,70),"MULTIJUGADOR","Duelo de lanzadores · hasta 4 jugadores");y+=86;
    GUI.enabled=!room.Busy&&!room.Connected;
    int mode=NexoUi.Segmented(new Rect(x,y,w,44),new[]{"INTERNET · CÓDIGO","LOCAL · IP"},direct?1:0);direct=mode==1;y+=62;
    float half=(w-16)/2;
    if(direct){
     NexoUi.Write(new Rect(x,y,w-176,18),"DIRECCIÓN DEL ANFITRIÓN",12,NexoUi.Teal,TextAnchor.MiddleLeft,true);NexoUi.Write(new Rect(x+w-160,y,160,18),"PUERTO UDP",12,NexoUi.Teal,TextAnchor.MiddleLeft,true);y+=24;
     ip=NexoHud.Field(new Rect(x,y,w-176,46),ip,45);port=NexoHud.Field(new Rect(x+w-160,y,160,46),port,5);y+=64;
     if(NexoHud.PrimaryButton(new Rect(x,y,half,52),"CREAR POR IP"))room.Direct(true,ip,port);
     if(NexoHud.Button(new Rect(x+half+16,y,half,52),"CONECTAR"))room.Direct(false,ip,port);y+=68;
     NexoUi.Write(new Rect(x,y,w,60),"Modo local: conecta equipos en la misma red con la IP del anfitrión. Para jugar desde redes distintas, selecciona INTERNET · CÓDIGO.",13,NexoUi.Muted,TextAnchor.UpperLeft);
    }else{
     NexoUi.Write(new Rect(x,y,w,18),"CÓDIGO DE INVITACIÓN",12,NexoUi.Teal,TextAnchor.MiddleLeft,true);y+=24;
     joinCode=NexoHud.Field(new Rect(x,y,w,46),joinCode,12).ToUpperInvariant();y+=64;
     if(NexoHud.PrimaryButton(new Rect(x,y,half,52),"CREAR SALA"))room.Host();
     if(NexoHud.Button(new Rect(x+half+16,y,half,52),"UNIRME"))room.Join(joinCode);y+=68;
     NexoUi.Write(new Rect(x,y,w,60),"Juega desde redes diferentes compartiendo el código de sala.",13,NexoUi.Muted,TextAnchor.UpperLeft);
    }
    GUI.enabled=true;
    var status=new Rect(x,panel.yMax-28-50-16-56,w,56);var tone=room.Busy?NexoUi.Amber:NexoUi.Teal;
    NexoHud.Fill(status,NexoUi.Surface);NexoHud.Fill(new Rect(status.x,status.y,3,status.height),tone);
    NexoUi.Write(new Rect(status.x+16,status.y,status.width-24,status.height),(room.Busy?"● ":"")+room.Status,14,tone);
    if(NexoHud.Button(new Rect(x,panel.yMax-28-50,w,50),room.Busy?"CANCELAR CONEXIÓN":"VOLVER")||(NexoUi.BackPressed&&!room.Busy)){room.Cancel();multiplayer=false;NexoUi.ResetFocus();}
    NexoUi.EndNav();
   }
   else
   {
    // Start screen: map backdrop, logo, framed green button stack, square icon buttons in the top corners.
    if(menuBackground){GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),menuBackground,ScaleMode.ScaleAndCrop);NexoHud.Fill(new Rect(0,0,Screen.width,Screen.height),new Color(0,.02f,.04f,.25f));}
    else NexoHud.Backdrop(null);
    NexoHud.Scale();NexoUi.BeginNav();
    if(logoCutout)GUI.DrawTexture(new Rect(370,26,540,180),logoCutout,ScaleMode.ScaleToFit);
    if(NexoUi.IconButton(new Rect(24,20,84,72),"Icon_Gear")){settings=true;NexoUi.ResetFocus();}
    if(NexoUi.IconButton(new Rect(1172,20,84,72),"Icon_Close")){
#if UNITY_EDITOR
     UnityEditor.EditorApplication.isPlaying=false;
#else
     Application.Quit();
#endif
    }
    var stack=new Rect(470,300,340,268);NexoUi.Panel(stack);
    float x=stack.x+16,w=stack.width-32,h=66,gap=16,y=stack.y+16;
    if(NexoHud.Button(new Rect(x,y,w,h),"HISTORIA"))GameSession.BeginNew();y+=h;NexoHud.Fill(new Rect(x+8,y+6,w-16,4),new Color(0,0,0,.85f));y+=gap;
    GUI.enabled=GameSession.SavedGameExists;if(NexoHud.Button(new Rect(x,y,w,h),"CONTINUAR")){if(!GameSession.BeginSaved(out error))Debug.LogWarning(error);}GUI.enabled=true;y+=h;NexoHud.Fill(new Rect(x+8,y+6,w-16,4),new Color(0,0,0,.85f));y+=gap;
    if(NexoHud.Button(new Rect(x,y,w,h),"MULTIJUGADOR")){multiplayer=true;NexoUi.ResetFocus();}
    if(error!=null)NexoUi.Write(new Rect(340,stack.yMax+16,600,22),error,13,NexoUi.Danger,TextAnchor.MiddleCenter);
    NexoUi.EndNav();
   }
   GUI.matrix=old;
  }
 }
}

