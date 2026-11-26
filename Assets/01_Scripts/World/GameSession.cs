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
   public int version=2; public int campaignRevision; public string[] collectedLoot; public string scene; public int[] defeated; public List<string> owned=new List<string>(); public bool[] goldenOwned; public int[] equipped={-1,-1,-1,-1,-1,-1};
   public int selected,food,boss,darkWater; public Vector3 position; public float health=100; public bool female,campaignComplete; public bool[] returnedElementals=new bool[5],completedLevels=new bool[6];
  }
  public static GameSession Current {get;private set;}
  public static bool Blocked=>Current&&Current.menu;
  static Data pending; static bool startNew,pendingEntrance;
  public bool Ready {get;private set;}
  public bool CampaignComplete{get;private set;}
  public bool[] ReturnedElementals{get;private set;}=new bool[5];
  public bool[] CompletedLevels{get;private set;}=new bool[6];
  public bool menu=true; bool started,confirmNew,graphics; string message="El punto de historia se guarda con GUARDAR; el inventario se actualiza al cambiarlo.";
  SlugInventory inventory; NexoterraThirdPerson player; PlayerHealth health;
  public string SavePath=>Path.Combine(Application.persistentDataPath,"nexoterra-save-v2.json");
  public bool HasSave=>File.Exists(SavePath);
  void Awake(){Current=this;inventory=GetComponent<SlugInventory>();player=GetComponent<NexoterraThirdPerson>();health=GetComponent<PlayerHealth>();}
  IEnumerator Start()
  {
   inventory.owned.Clear();inventory.goldenOwned.Clear();inventory.equipped=new[]{-1,-1,-1,-1,-1,-1};
   yield return null;Ready=true;if(pending==null)inventory.Add("Infierno");
   if(pending!=null){var data=pending;pending=null;Apply(data);if(pendingEntrance){pendingEntrance=false;player.Teleport(player.spawn);health.Restore(health.maxHealth);}Resume();}
   else if(startNew){startNew=false;Resume();}else Resume();
  }
  void OnDestroy(){if(Current==this){Current=null;Time.timeScale=1;}}
  void Update()
  {
   if(!NexoInput.Pause&&(Keyboard.current==null||!Keyboard.current.escapeKey.wasPressedThisFrame))return;
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
  static bool[] CopyCompletedLevels(bool[] source){var copy=new bool[CampaignLevel.Scenes.Length];if(source!=null)Array.Copy(source,copy,Math.Min(source.Length,copy.Length));return copy;}
  static bool IsLevelCompleted(Data data,int index)=>data!=null&&index>=0&&index<CampaignLevel.Scenes.Length&&(data.campaignComplete||data.completedLevels!=null&&index<data.completedLevels.Length&&data.completedLevels[index]);
  public static bool[] CompletedLevelSnapshot(){
   if(Current)return CopyCompletedLevels(Current.CompletedLevels);
   var levels=new bool[CampaignLevel.Scenes.Length];if(!SavedGameExists)return levels;
   try{var data=ReadSave(Path.Combine(Application.persistentDataPath,"nexoterra-save-v2.json"));levels=CopyCompletedLevels(data.completedLevels);if(data.campaignComplete)for(int i=0;i<levels.Length;i++)levels[i]=true;}catch{}
   return levels;
  }
  public static bool IsLevelCompleted(int index){
   if(index<0||index>=CampaignLevel.Scenes.Length)return false;
   if(Current)return IsLevelCompleted(new Data{completedLevels=Current.CompletedLevels,campaignComplete=Current.CampaignComplete},index);
   if(!SavedGameExists)return false;
   try{return IsLevelCompleted(ReadSave(Path.Combine(Application.persistentDataPath,"nexoterra-save-v2.json")),index);}catch{return false;}
  }
  public static void BeginNew(){pending=null;pendingEntrance=false;startNew=true;FrontEndMenu.Load(FrontEndMenu.WorldScene);}
  public static bool BeginSaved(out string error)
  {
   try {var data=ReadSave(Path.Combine(Application.persistentDataPath,"nexoterra-save-v2.json"));if(data.scene=="Nexoterra_Final")data.scene="Nexoterra_Carmesi";pending=data;startNew=false;FrontEndMenu.Load(string.IsNullOrEmpty(data.scene)?"Nexoterra_CuatroNodos":data.scene);error=null;return true;}
   catch(Exception e){error="No se pudo cargar: "+e.Message;return false;}
  }
  public static void BeginLevel(int index){
   index=Mathf.Clamp(index,0,CampaignLevel.Scenes.Length-1);bool hadSave=SavedGameExists;Data source;
   try{source=hadSave?ReadSave(Path.Combine(Application.persistentDataPath,"nexoterra-save-v2.json")):ReadCollection();}catch{source=new Data();hadSave=false;}
   if(!IsLevelCompleted(source,index))return;
   var owned=source.owned??new List<string>();if(owned.Count==0)owned.Add("Infierno");
   var equipped=source.equipped!=null&&source.equipped.Length==6?(int[])source.equipped.Clone():new[]{-1,-1,-1,-1,-1,-1};
   if(!hadSave&&!equipped.Any(i=>i>=0))for(int i=0;i<Mathf.Min(6,owned.Count);i++)equipped[i]=i;
   pending=new Data{scene=CampaignLevel.Scenes[index],campaignRevision=CampaignLevel.Revision,owned=new List<string>(owned),goldenOwned=source.goldenOwned,equipped=equipped,selected=Mathf.Clamp(source.selected,0,5),food=source.food,boss=0,health=100,returnedElementals=source.returnedElementals??new bool[5],campaignComplete=source.campaignComplete,completedLevels=CopyCompletedLevels(source.completedLevels)};
   startNew=false;pendingEntrance=true;FrontEndMenu.Load(pending.scene);
  }
  static Data ReadSave(string path)
  {
   string json=File.ReadAllText(path);var data=JsonUtility.FromJson<Data>(json);if(data==null||data.version!=2||data.owned==null||!float.IsFinite(data.position.x)||!float.IsFinite(data.position.y)||!float.IsFinite(data.position.z))throw new InvalidDataException("Archivo no válido");
   if(json.IndexOf("\"completedLevels\"",StringComparison.Ordinal)<0)data.completedLevels=InferCompletedLevels(data);
   return data;
  }
  static bool[] InferCompletedLevels(Data data){
   var completed=new bool[CampaignLevel.Scenes.Length];if(data==null)return completed;
   if(data.campaignComplete){for(int i=0;i<completed.Length;i++)completed[i]=true;return completed;}
   if(data.owned==null)return completed;
   for(int i=0;i<Mathf.Min(completed.Length,CampaignLevel.Rewards.Length);i++){string reward=CampaignLevel.Rewards[i];if(!string.IsNullOrEmpty(reward)&&data.owned.Any(id=>!string.IsNullOrEmpty(id)&&SlugCatalog.BaseSpecies(id)==reward))completed[i]=true;}
   return completed;
  }
  public Data Capture()
  {
   var boss=FindAnyObjectByType<BossEncounters>();
   return new Data{campaignRevision=CampaignLevel.Revision,collectedLoot=CampaignLevel.Current?CampaignLevel.Current.CaptureLoot():null,scene=SceneManager.GetActiveScene().name,defeated=CampaignLevel.Current?CampaignLevel.Current.CaptureDefeated():null,owned=new List<string>(inventory.owned),goldenOwned=inventory.goldenOwned.ToArray(),equipped=(int[])inventory.equipped.Clone(),darkWater=inventory.darkWater,selected=inventory.selected,food=player.food,boss=boss?boss.Completed:0,position=transform.position,health=health.Health,female=player.female.activeSelf,campaignComplete=CampaignComplete,returnedElementals=(bool[])ReturnedElementals.Clone(),completedLevels=CopyCompletedLevels(CompletedLevels)};
  }
  public void Apply(Data data)
  {
   CampaignComplete=data.campaignComplete;ReturnedElementals=data.returnedElementals!=null&&data.returnedElementals.Length==5?(bool[])data.returnedElementals.Clone():new bool[5];
   CompletedLevels=CopyCompletedLevels(data.completedLevels);if(CampaignComplete)for(int i=0;i<CompletedLevels.Length;i++)CompletedLevels[i]=true;
   inventory.darkWater=Mathf.Max(0,data.darkWater);inventory.owned.Clear();var remap=new Dictionary<int,int>();var unique=new HashSet<string>();
   inventory.goldenOwned.Clear();for(int i=0;i<data.owned.Count;i++){string id=SlugCatalog.Canonical(data.owned[i]);var definition=inventory.catalog.Find(id);if(definition==null||(definition.unique&&!unique.Add(SlugCatalog.BaseSpecies(id))))continue;remap[i]=inventory.owned.Count;inventory.owned.Add(id);inventory.goldenOwned.Add(data.goldenOwned!=null&&i<data.goldenOwned.Length&&data.goldenOwned[i]);}
   inventory.equipped=new[]{-1,-1,-1,-1,-1,-1};var used=new HashSet<int>();
   if(data.equipped!=null)for(int i=0;i<Mathf.Min(6,data.equipped.Length);i++)if(remap.TryGetValue(data.equipped[i],out int slot)&&used.Add(slot))inventory.equipped[i]=slot;
   inventory.selected=Mathf.Clamp(data.selected,0,5);player.food=Mathf.Max(0,data.food);player.adopted=inventory.Count;
   player.Teleport(data.position);health.Restore(data.health);player.female.SetActive(data.female);player.male.SetActive(!data.female);
   var boss=FindAnyObjectByType<BossEncounters>();if(boss)boss.Restore(data.boss);
   if(CampaignLevel.Current){CampaignLevel.Current.RestoreLoot(data.collectedLoot);if(data.campaignRevision==CampaignLevel.Revision)CampaignLevel.Current.RestoreDefeated(data.defeated);CampaignLevel.Current.RefreshReturnedVisual();}
  }
  public void MarkLevelCompleted(int index){
   if(index<0||index>=CampaignLevel.Scenes.Length)return;
   if(CompletedLevels==null||CompletedLevels.Length!=CampaignLevel.Scenes.Length)CompletedLevels=CopyCompletedLevels(CompletedLevels);
   CompletedLevels[index]=true;SaveLevelProgress();
  }
  void SaveLevelProgress(){
   if(ArenaSession.Current)return;
   try{
    var data=File.Exists(SavePath)?ReadSave(SavePath):Capture();data.completedLevels=CopyCompletedLevels(CompletedLevels);data.campaignComplete=CampaignComplete;data.returnedElementals=(bool[])ReturnedElementals.Clone();
    Directory.CreateDirectory(Application.persistentDataPath);string temp=SavePath+".tmp";File.WriteAllText(temp,JsonUtility.ToJson(data,true));if(File.Exists(SavePath))File.Replace(temp,SavePath,SavePath+".bak");else File.Move(temp,SavePath);
   }catch(Exception e){Debug.LogWarning("No se pudo guardar el progreso de niveles: "+e.Message);}
  }
  public void MarkCampaignComplete(){CampaignComplete=true;for(int i=0;i<CompletedLevels.Length;i++)CompletedLevels[i]=true;}
  public bool HasReturnedElemental(int index)=>index>=0&&index<ReturnedElementals.Length&&ReturnedElementals[index];
  public void MarkElementalReturned(int index){if(index>=0&&index<ReturnedElementals.Length)ReturnedElementals[index]=true;}
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
    if(unlocked?.owned!=null){if(data.owned==null)data.owned=new List<string>();var gold=data.goldenOwned?.ToList()??new List<bool>();while(gold.Count<data.owned.Count)gold.Add(false);for(int i=0;i<unlocked.owned.Count;i++){var id=unlocked.owned[i];if(string.IsNullOrEmpty(id))continue;int existing=data.owned.FindIndex(s=>SlugCatalog.BaseSpecies(s)==SlugCatalog.BaseSpecies(id));bool isGold=unlocked.goldenOwned!=null&&i<unlocked.goldenOwned.Length&&unlocked.goldenOwned[i];if(existing>=0)gold[existing]|=isGold;else{data.owned.Add(id);gold.Add(isGold);}}data.goldenOwned=gold.ToArray();}
   }
   if(data.owned==null)data.owned=new List<string>();if(data.goldenOwned==null)data.goldenOwned=new bool[data.owned.Count];if(!data.owned.Contains("Infierno")){data.owned.Insert(0,"Infierno");data.goldenOwned=data.goldenOwned.Prepend(false).ToArray();}return data;
  }
  public static void RememberSpecies(string id,bool golden=false){
   if(ArenaSession.Current)return;
   try{
    var data=ReadCollection();if(data.goldenOwned==null)data.goldenOwned=new bool[data.owned.Count];var flags=data.goldenOwned.ToList();while(flags.Count<data.owned.Count)flags.Add(false);int existing=data.owned.FindIndex(s=>SlugCatalog.BaseSpecies(s)==SlugCatalog.BaseSpecies(id));if(existing>=0){flags[existing]|=golden;}else{data.owned.Add(id);flags.Add(golden);}data.goldenOwned=flags.ToArray();
    Directory.CreateDirectory(Application.persistentDataPath);string temp=CollectionPath+".tmp";File.WriteAllText(temp,JsonUtility.ToJson(data));
    if(File.Exists(CollectionPath))File.Replace(temp,CollectionPath,CollectionPath+".bak");else File.Move(temp,CollectionPath);
   }catch(Exception e){Debug.LogWarning("No se pudo conservar la colección: "+e.Message);}
  }
  public void SaveInventory(){
   if(ArenaSession.Current||!started)return;
   try{var data=File.Exists(SavePath)?ReadSave(SavePath):Capture();data.owned=new List<string>(inventory.owned);data.goldenOwned=inventory.goldenOwned.ToArray();data.equipped=(int[])inventory.equipped.Clone();data.selected=inventory.selected;data.food=player.food;data.completedLevels=CopyCompletedLevels(CompletedLevels);data.campaignComplete=CampaignComplete;data.returnedElementals=(bool[])ReturnedElementals.Clone();Directory.CreateDirectory(Application.persistentDataPath);string temp=SavePath+".tmp";File.WriteAllText(temp,JsonUtility.ToJson(data,true));if(File.Exists(SavePath))File.Replace(temp,SavePath,SavePath+".bak");else File.Move(temp,SavePath);}
   catch(Exception e){Debug.LogWarning("No se pudo guardar la colección: "+e.Message);}
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
    NexoHud.ControlRow(l,y0+step*2,"voltereta","Ctrl","+","A","/","D");NexoHud.ControlRow(l,y0+step*3,"recoger","E");NexoHud.ControlRow(l,y0+step*4,"alimentar","F");NexoHud.ControlRow(l,y0+step*5,"gestos","B");
    NexoHud.ControlRow(r,y0,"apuntar","Clic der");NexoHud.ControlRow(r,y0+step,"lanzar","Clic izq");NexoHud.ControlRow(r,y0+step*2,"recargar","R");
    NexoHud.ControlRow(r,y0+step*3,"mochila","Alt");NexoHud.ControlRow(r,y0+step*4,"compañera","G");NexoHud.ControlRow(r,y0+step*5,"menú","Esc");}
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
    if(!HasSave){if(NexoHud.Button(new Rect(content.x,y,bw,bh),"NUEVA PARTIDA")){if(started){confirmNew=true;NexoUi.ResetFocus();}else NewGame();}y+=bh+gap;}
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
