using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
namespace Nexoterra.World
{
 [DefaultExecutionOrder(-200)]
 public sealed class GameSession : MonoBehaviour
 {
  [Serializable] public sealed class Data
  {
   public int version=2; public int campaignRevision; public string[] collectedLoot; public string scene; public int[] defeated; public List<string> owned=new List<string>(); public int[] equipped={-1,-1,-1,-1,-1,-1};
   public int selected,food,boss,darkWater; public Vector3 position; public float health=100; public bool female;
  }
  public static GameSession Current {get;private set;}
  public static bool Blocked=>Current&&Current.menu;
  static Data pending; static bool startNew,pendingEntrance;
  public bool Ready {get;private set;}
  public bool menu=true; bool started,confirmNew,graphics; string message="El progreso se guarda solo al pulsar GUARDAR.";
  SlugInventory inventory; NexoterraThirdPerson player; PlayerHealth health;
  public string SavePath=>Path.Combine(Application.persistentDataPath,"nexoterra-save-v2.json");
  public bool HasSave=>File.Exists(SavePath);
  void Awake(){Current=this;inventory=GetComponent<SlugInventory>();player=GetComponent<NexoterraThirdPerson>();health=GetComponent<PlayerHealth>();}
  IEnumerator Start()
  {
   inventory.owned.Clear();inventory.equipped=new[]{-1,-1,-1,-1,-1,-1};
   yield return null;Ready=true;if(pending==null)inventory.Add("Infierno");
   if(pending!=null){var data=pending;pending=null;Apply(data);if(pendingEntrance){pendingEntrance=false;player.Teleport(player.spawn);health.Restore(health.maxHealth);}Resume();}
   else if(startNew){startNew=false;Resume();}else Resume();
  }
  void OnDestroy(){if(Current==this){Current=null;Time.timeScale=1;}}
  void Update()
  {
   if(Keyboard.current==null||!Keyboard.current.escapeKey.wasPressedThisFrame)return;
   var emotes=GetComponent<PlayerEmotes>();if(emotes&&emotes.IsOpen){emotes.Cancel();return;}
   if(inventory.IsOpen){inventory.SetOpen(false);return;}
   if(menu&&(graphics||confirmNew)){graphics=false;confirmNew=false;return;}
   SetMenu(!menu);
  }
  void SetMenu(bool value){menu=value;Time.timeScale=value&&!ArenaSession.Current?0:1;Cursor.lockState=value?CursorLockMode.None:CursorLockMode.Locked;Cursor.visible=value;confirmNew=false;graphics=false;NexoUi.ResetFocus();}
  public void Resume(){started=true;SetMenu(false);}
  public void NewGame(){pending=null;pendingEntrance=false;startNew=true;Time.timeScale=1;ReloadScene();}
  static void ReloadScene(){FrontEndMenu.Load(FrontEndMenu.WorldScene);}
  public static bool SavedGameExists=>File.Exists(Path.Combine(Application.persistentDataPath,"nexoterra-save-v2.json"));
  public static void BeginNew(){pending=null;pendingEntrance=false;startNew=true;FrontEndMenu.Load(FrontEndMenu.WorldScene);}
  public static bool BeginSaved(out string error)
  {
   try {var data=ReadSave(Path.Combine(Application.persistentDataPath,"nexoterra-save-v2.json"));if(data.scene=="Nexoterra_Final")data.scene="Nexoterra_Carmesi";pending=data;startNew=false;FrontEndMenu.Load(string.IsNullOrEmpty(data.scene)?"Nexoterra_CuatroNodos":data.scene);error=null;return true;}
   catch(Exception e){error="No se pudo cargar: "+e.Message;return false;}
  }
  static Data ReadSave(string path)
  {
   var data=JsonUtility.FromJson<Data>(File.ReadAllText(path));if(data==null||data.version!=2||data.owned==null||!float.IsFinite(data.position.x)||!float.IsFinite(data.position.y)||!float.IsFinite(data.position.z))throw new InvalidDataException("Archivo no válido");return data;
  }
  public Data Capture()
  {
   var boss=FindAnyObjectByType<BossEncounters>();
   return new Data{campaignRevision=CampaignLevel.Revision,collectedLoot=CampaignLevel.Current?CampaignLevel.Current.CaptureLoot():null,scene=SceneManager.GetActiveScene().name,defeated=CampaignLevel.Current?CampaignLevel.Current.CaptureDefeated():null,owned=new List<string>(inventory.owned),equipped=(int[])inventory.equipped.Clone(),darkWater=inventory.darkWater,selected=inventory.selected,food=player.food,boss=boss?boss.Completed:0,position=transform.position,health=health.Health,female=player.female.activeSelf};
  }
  public void Apply(Data data)
  {
   inventory.darkWater=Mathf.Max(0,data.darkWater);inventory.owned.Clear();var remap=new Dictionary<int,int>();var unique=new HashSet<string>();
   for(int i=0;i<data.owned.Count;i++){string id=SlugCatalog.Canonical(data.owned[i]);var definition=inventory.catalog.Find(id);if(definition==null||(definition.unique&&!unique.Add(SlugCatalog.BaseSpecies(id))))continue;remap[i]=inventory.owned.Count;inventory.owned.Add(id);}
   inventory.equipped=new[]{-1,-1,-1,-1,-1,-1};var used=new HashSet<int>();
   if(data.equipped!=null)for(int i=0;i<Mathf.Min(6,data.equipped.Length);i++)if(remap.TryGetValue(data.equipped[i],out int slot)&&used.Add(slot))inventory.equipped[i]=slot;
   inventory.selected=Mathf.Clamp(data.selected,0,5);player.food=Mathf.Max(0,data.food);player.adopted=inventory.Count;
   player.Teleport(data.position);health.Restore(data.health);player.female.SetActive(data.female);player.male.SetActive(!data.female);
   var boss=FindAnyObjectByType<BossEncounters>();if(boss)boss.Restore(data.boss);
   if(CampaignLevel.Current){CampaignLevel.Current.RestoreLoot(data.collectedLoot);if(data.campaignRevision==CampaignLevel.Revision)CampaignLevel.Current.RestoreDefeated(data.defeated);}
  }
  public void Travel(string next)
  {
   var data=Capture();data.scene=next;data.defeated=null;data.collectedLoot=null;data.boss=0;pending=data;pendingEntrance=true;
   FrontEndMenu.Load(next);
  }
  static string CollectionPath=>Path.Combine(Application.persistentDataPath,"nexoterra-collection-v1.json");
  public static Data ReadCollection(){
   var data=SavedGameExists?ReadSave(Path.Combine(Application.persistentDataPath,"nexoterra-save-v2.json")):new Data();
   if(File.Exists(CollectionPath)){
    var unlocked=JsonUtility.FromJson<Data>(File.ReadAllText(CollectionPath));
    if(unlocked?.owned!=null)data.owned.AddRange(unlocked.owned);
   }
   data.owned=data.owned.Where(id=>!string.IsNullOrEmpty(id)).GroupBy(SlugCatalog.BaseSpecies).Select(g=>g.Last()).ToList();
   if(!data.owned.Contains("Infierno"))data.owned.Insert(0,"Infierno");return data;
  }
  public static void RememberSpecies(string id){
   if(ArenaSession.Current)return;
   try{
    var data=ReadCollection();data.owned.RemoveAll(s=>SlugCatalog.BaseSpecies(s)==SlugCatalog.BaseSpecies(id));data.owned.Add(id);
    Directory.CreateDirectory(Application.persistentDataPath);string temp=CollectionPath+".tmp";File.WriteAllText(temp,JsonUtility.ToJson(data));
    if(File.Exists(CollectionPath))File.Replace(temp,CollectionPath,CollectionPath+".bak");else File.Move(temp,CollectionPath);
   }catch(Exception e){Debug.LogWarning("No se pudo conservar la colección: "+e.Message);}
  }
  public void SaveGame()
  {
   if(ArenaSession.Current){message="La colección de historia se conserva en el multijugador.";return;}
   if(!started){message="Inicia una partida antes de guardar.";return;}
   try{Directory.CreateDirectory(Application.persistentDataPath);string temp=SavePath+".tmp";File.WriteAllText(temp,JsonUtility.ToJson(Capture(),true));if(File.Exists(SavePath))File.Replace(temp,SavePath,SavePath+".bak");else File.Move(temp,SavePath);message="Partida guardada · "+DateTime.Now.ToString("HH:mm");}
   catch(Exception e){message="No se pudo guardar: "+e.Message;Debug.LogWarning(message);}
  }
  public void LoadGame()
  {
   if(!BeginSaved(out string error))message=error;
  }
  void OnGUI()
  {
   if(!menu)return;var matrix=GUI.matrix;
   NexoHud.Fill(new Rect(0,0,Screen.width,Screen.height),new Color(.005f,.02f,.03f,.82f));
   NexoHud.Scale();NexoUi.BeginNav();
   // Left: identity + controls reference.
   var left=new Rect(72,56,500,608);NexoUi.Panel(left);
   var logo=Resources.Load<Texture2D>("Nexoterra/UI/Logo");if(logo)GUI.DrawTexture(new Rect(left.x+40,left.y+24,left.width-80,104),logo,ScaleMode.ScaleToFit);
   NexoUi.Write(new Rect(left.x+32,left.y+140,left.width-64,22),started?"EXPLORACIÓN EN PAUSA":"CRÓNICAS DEL SUBSUELO",14,NexoUi.Teal,TextAnchor.MiddleLeft,true);
   NexoUi.Write(new Rect(left.x+32,left.y+164,left.width-64,44),"Seis biomas. Criaturas únicas. Tu colección comienza explorando.",15,NexoUi.Text,TextAnchor.UpperLeft);
   NexoUi.Section(new Rect(left.x+32,left.y+222,left.width-64,22),"CONTROLES");
   {float l=left.x+32,r=left.x+262,y0=left.y+256,step=36;
    NexoHud.ControlRow(l,y0,"caminar","W","A","S","D");NexoHud.ControlRow(l,y0+step,"correr","Mayús");
    NexoHud.ControlRow(l,y0+step*2,"voltereta","Ctrl","W/A/D");NexoHud.ControlRow(l,y0+step*3,"recoger","E");NexoHud.ControlRow(l,y0+step*4,"alimentar","F");NexoHud.ControlRow(l,y0+step*5,"gestos","B");
    NexoHud.ControlRow(r,y0,"apuntar","Clic der");NexoHud.ControlRow(r,y0+step,"lanzar","Clic izq");NexoHud.ControlRow(r,y0+step*2,"recargar","R");
    NexoHud.ControlRow(r,y0+step*3,"mochila","Alt");NexoHud.ControlRow(r,y0+step*4,"mapa","M");NexoHud.ControlRow(r,y0+step*5,"menú","Esc");}
   // Right: actions, or one of its sub-pages.
   var right=new Rect(616,56,592,608);NexoUi.Panel(right);
   var content=new Rect(right.x+40,right.y+28,right.width-80,right.height-56);
   if(graphics){if(GameplayOptions.DrawSettingsPage(content))graphics=false;NexoUi.EndNav();GUI.matrix=matrix;return;}
   float bw=content.width,bh=52,gap=12,y=content.y+86;
   if(ArenaSession.Current)
   {
    NexoUi.Title(new Rect(content.x,content.y,bw,70),"PARTIDA EN LÍNEA","La sala sigue activa mientras ves este menú.");
    if(NexoHud.PrimaryButton(new Rect(content.x,y,bw,bh),"REANUDAR"))Resume();y+=bh+gap;
    if(NexoHud.Button(new Rect(content.x,y,bw,bh),"CALIDAD GRÁFICA")){graphics=true;NexoUi.ResetFocus();}y+=bh+gap;
    if(NexoHud.Button(new Rect(content.x,y,bw,bh),"SALIR AL MENÚ PRINCIPAL")){Resume();if(MultiplayerRoom.Current)MultiplayerRoom.Current.Leave();else FrontEndMenu.Load(FrontEndMenu.MenuScene);}
   }
   else if(confirmNew)
   {
    NexoUi.Title(new Rect(content.x,content.y,bw,70),"¿NUEVA PARTIDA?","El progreso sin guardar se perderá. Tu archivo guardado se conserva.");
    if(NexoHud.PrimaryButton(new Rect(content.x,y,bw,bh),"EMPEZAR CON INFIERNO"))NewGame();y+=bh+gap;
    if(NexoHud.Button(new Rect(content.x,y,bw,bh),"VOLVER")||NexoUi.BackPressed){confirmNew=false;NexoUi.ResetFocus();}
   }
   else
   {
    NexoUi.Title(new Rect(content.x,content.y,bw,70),started?"PAUSA":"NEXOTERRA",started?"El mundo se detiene mientras decides.":"Comienza o continúa tu historia.");
    if(started){if(NexoHud.PrimaryButton(new Rect(content.x,y,bw,bh),"REANUDAR"))Resume();}
    else{GUI.enabled=HasSave;if(NexoHud.PrimaryButton(new Rect(content.x,y,bw,bh),"CONTINUAR"))LoadGame();GUI.enabled=true;}
    y+=bh+gap;
    if(NexoHud.Button(new Rect(content.x,y,bw,bh),"NUEVA PARTIDA")){if(started){confirmNew=true;NexoUi.ResetFocus();}else NewGame();}y+=bh+gap;
    float half=(bw-gap)/2;
    GUI.enabled=started;if(NexoHud.Button(new Rect(content.x,y,half,bh),"GUARDAR"))SaveGame();GUI.enabled=true;
    GUI.enabled=HasSave;if(NexoHud.Button(new Rect(content.x+half+gap,y,half,bh),"CARGAR"))LoadGame();GUI.enabled=true;y+=bh+gap;
    if(NexoHud.Button(new Rect(content.x,y,bw,bh),"CALIDAD GRÁFICA")){graphics=true;NexoUi.ResetFocus();}y+=bh+gap;
    if(NexoHud.Button(new Rect(content.x,y,bw,bh),"MENÚ PRINCIPAL")){if(MultiplayerRoom.Current){MultiplayerRoom.Current.Leave();NexoUi.EndNav();GUI.matrix=matrix;return;}FrontEndMenu.Load(FrontEndMenu.MenuScene);}
   }
   // Status line pinned to the bottom of the action panel.
   var status=new Rect(content.x,content.yMax-40,content.width,40);NexoHud.Fill(new Rect(status.x,status.y,3,status.height),NexoUi.Teal);
   NexoUi.Write(new Rect(status.x+14,status.y,status.width-14,status.height),message,13,NexoUi.Muted);
   NexoUi.EndNav();GUI.matrix=matrix;
  }
 }
}
