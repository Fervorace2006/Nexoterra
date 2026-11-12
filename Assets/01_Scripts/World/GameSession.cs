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
  public bool menu=true; bool started,confirmNew; string message="El progreso se guarda solo al pulsar GUARDAR.";
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
   if(inventory.IsOpen){inventory.SetOpen(false);return;}
   SetMenu(!menu);
  }
  void SetMenu(bool value){menu=value;Time.timeScale=value&&!ArenaSession.Current?0:1;Cursor.lockState=value?CursorLockMode.None:CursorLockMode.Locked;Cursor.visible=value;confirmNew=false;}
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
   if(!menu)return;var matrix=GUI.matrix;NexoHud.Scale();
   float scale=Mathf.Min(Screen.width/1280f,Screen.height/720f);
   NexoHud.Fill(new Rect(-(Screen.width/scale-1280)*.5f,-(Screen.height/scale-720)*.5f,Screen.width/scale,Screen.height/scale),new Color(.005f,.025f,.04f,.8f));
   NexoHud.Panel(new Rect(60,60,480,590));var logo=Resources.Load<Texture2D>("Nexoterra/UI/Logo");if(logo)GUI.DrawTexture(new Rect(90,80,420,120),logo,ScaleMode.ScaleToFit);
   NexoHud.Fill(new Rect(94,214,412,1),NexoHud.Gold*new Color(1,1,1,.6f));
   NexoHud.Label(new Rect(94,226,410,28),started?"EXPLORACIÓN EN PAUSA":"CRÓNICAS DEL SUBSUELO",18,NexoHud.Cyan);
   NexoHud.Label(new Rect(94,350,400,22),"CONTROLES",13,NexoHud.Gold);NexoHud.Fill(new Rect(94,372,412,1),NexoHud.Gold*new Color(1,1,1,.4f));
   NexoHud.Label(new Rect(94,262,400,80),"Seis biomas. Criaturas únicas.\nTu colección comienza explorando.",19,Color.white);
   {float l=94,r=300,y0=384;NexoHud.ControlRow(l,y0,"caminar","W","A","S","D");NexoHud.ControlRow(l,y0+36,"correr","Mayús");NexoHud.ControlRow(l,y0+72,"saltar","Espacio");NexoHud.ControlRow(l,y0+108,"voltereta","Ctrl","+","A","/","D");NexoHud.ControlRow(l,y0+144,"recoger","E");NexoHud.ControlRow(l,y0+180,"alimentar","F");NexoHud.ControlRow(r,y0,"apuntar","Clic der");NexoHud.ControlRow(r,y0+36,"lanzar","Clic izq");NexoHud.ControlRow(r,y0+72,"recargar","R");NexoHud.ControlRow(r,y0+108,"mochila","B");NexoHud.ControlRow(r,y0+144,"mapa","M");NexoHud.ControlRow(r,y0+180,"menú","Esc");}
   NexoHud.Panel(new Rect(640,92,480,554));float y=120;
   if(ArenaSession.Current){if(NexoHud.Button(new Rect(672,180,416,58),"REANUDAR"))Resume();if(NexoHud.Button(new Rect(672,270,416,58),"SALIR AL MENÚ PRINCIPAL")){Resume();if(MultiplayerRoom.Current)MultiplayerRoom.Current.Leave();else FrontEndMenu.Load(FrontEndMenu.MenuScene);}NexoHud.Label(new Rect(672,360,416,80),"La partida en línea continúa mientras abres el menú.",18,NexoHud.Cyan);GUI.matrix=matrix;return;}
   if(confirmNew){NexoHud.Label(new Rect(670,y,420,110),"¿Empezar una partida nueva?\nEl progreso sin guardar se perderá.\nTu archivo guardado se conserva.",19,Color.white);y+=125;if(NexoHud.Button(new Rect(672,y,416,58),"EMPEZAR CON INFIERNO"))NewGame();y+=72;if(NexoHud.Button(new Rect(672,y,416,58),"VOLVER"))confirmNew=false;}
   else
   {
    if(started){if(NexoHud.Button(new Rect(672,y,416,58),"REANUDAR"))Resume();y+=72;}
    else {GUI.enabled=HasSave;if(NexoHud.Button(new Rect(672,y,416,58),"CONTINUAR"))LoadGame();GUI.enabled=true;y+=72;}
    if(NexoHud.Button(new Rect(672,y,416,58),"NUEVA PARTIDA")){if(started)confirmNew=true;else NewGame();}y+=72;
    GUI.enabled=started;if(NexoHud.Button(new Rect(672,y,416,58),"GUARDAR PARTIDA"))SaveGame();GUI.enabled=true;y+=72;
    GUI.enabled=HasSave;if(NexoHud.Button(new Rect(672,y,416,58),"CARGAR GUARDADO"))LoadGame();GUI.enabled=true;y+=72;
    if(NexoHud.Button(new Rect(672,y,416,58),"MENÚ PRINCIPAL")){if(MultiplayerRoom.Current){MultiplayerRoom.Current.Leave();return;}FrontEndMenu.Load(FrontEndMenu.MenuScene);}
    y+=72;

   }
   NexoHud.Label(new Rect(665,570,430,64),message,15,NexoHud.Cyan);GUI.matrix=matrix;
  }
 }
}


