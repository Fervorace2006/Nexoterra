using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
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
   public int version=2; public List<string> owned=new List<string>(); public int[] equipped={-1,-1,-1,-1,-1,-1};
   public int selected,food,boss; public Vector3 position; public float health=100; public bool female;
  }
  public static GameSession Current {get;private set;}
  public static bool Blocked=>Current&&Current.menu;
  static Data pending; static bool startNew;
  public bool Ready {get;private set;}
  public bool menu=true; bool started,confirmNew; string message="El progreso se guarda solo al pulsar GUARDAR.";
  SlugInventory inventory; NexoterraThirdPerson player; PlayerHealth health;
  public string SavePath=>Path.Combine(Application.persistentDataPath,"nexoterra-save-v2.json");
  public bool HasSave=>File.Exists(SavePath);
  void Awake(){Current=this;inventory=GetComponent<SlugInventory>();player=GetComponent<NexoterraThirdPerson>();health=GetComponent<PlayerHealth>();}
  IEnumerator Start()
  {
   inventory.owned.Clear();inventory.equipped=new[]{-1,-1,-1,-1,-1,-1};
   yield return null;Ready=true;
   if(pending!=null){var data=pending;pending=null;Apply(data);Resume();}
   else if(startNew){startNew=false;Resume();}else Resume();
  }
  void OnDestroy(){if(Current==this){Current=null;Time.timeScale=1;}}
  void Update()
  {
   if(Keyboard.current==null||!Keyboard.current.escapeKey.wasPressedThisFrame)return;
   if(inventory.IsOpen){inventory.SetOpen(false);return;}
   if(started)SetMenu(!menu);
  }
  void SetMenu(bool value){menu=value;Time.timeScale=value?0:1;Cursor.lockState=value?CursorLockMode.None:CursorLockMode.Locked;Cursor.visible=value;confirmNew=false;}
  public void Resume(){started=true;SetMenu(false);}
  public void NewGame(){pending=null;startNew=true;Time.timeScale=1;ReloadScene();}
  static void ReloadScene(){FrontEndMenu.Load(FrontEndMenu.WorldScene);}
  public static bool SavedGameExists=>File.Exists(Path.Combine(Application.persistentDataPath,"nexoterra-save-v2.json"));
  public static void BeginNew(){pending=null;startNew=true;FrontEndMenu.Load(FrontEndMenu.WorldScene);}
  public static bool BeginSaved(out string error)
  {
   try {var data=ReadSave(Path.Combine(Application.persistentDataPath,"nexoterra-save-v2.json"));pending=data;startNew=false;FrontEndMenu.Load(FrontEndMenu.WorldScene);error=null;return true;}
   catch(Exception e){error="No se pudo cargar: "+e.Message;return false;}
  }
  static Data ReadSave(string path)
  {
   var data=JsonUtility.FromJson<Data>(File.ReadAllText(path));if(data==null||data.version!=2||data.owned==null||!float.IsFinite(data.position.x)||!float.IsFinite(data.position.y)||!float.IsFinite(data.position.z))throw new InvalidDataException("Archivo no válido");return data;
  }
  public Data Capture()
  {
   var boss=FindAnyObjectByType<BossEncounters>();
   return new Data{owned=new List<string>(inventory.owned),equipped=(int[])inventory.equipped.Clone(),selected=inventory.selected,food=player.food,boss=boss?boss.Completed:0,position=transform.position,health=health.Health,female=player.female.activeSelf};
  }
  public void Apply(Data data)
  {
   inventory.owned.Clear();var remap=new Dictionary<int,int>();var unique=new HashSet<string>();
   for(int i=0;i<data.owned.Count;i++){string id=SlugCatalog.Canonical(data.owned[i]);var definition=inventory.catalog.Find(id);if(definition==null||(definition.unique&&!unique.Add(id)))continue;remap[i]=inventory.owned.Count;inventory.owned.Add(id);}
   inventory.equipped=new[]{-1,-1,-1,-1,-1,-1};var used=new HashSet<int>();
   if(data.equipped!=null)for(int i=0;i<Mathf.Min(6,data.equipped.Length);i++)if(remap.TryGetValue(data.equipped[i],out int slot)&&used.Add(slot))inventory.equipped[i]=slot;
   inventory.selected=Mathf.Clamp(data.selected,0,5);player.food=Mathf.Max(0,data.food);player.adopted=inventory.Count;
   player.Teleport(data.position);health.Restore(data.health);player.female.SetActive(data.female);player.male.SetActive(!data.female);
   var boss=FindAnyObjectByType<BossEncounters>();if(boss)boss.Restore(data.boss);
  }
  public void SaveGame()
  {
   if(!started){message="Inicia una partida antes de guardar.";return;}
   try{Directory.CreateDirectory(Application.persistentDataPath);string temp=SavePath+".tmp";File.WriteAllText(temp,JsonUtility.ToJson(Capture(),true));if(File.Exists(SavePath))File.Replace(temp,SavePath,SavePath+".bak");else File.Move(temp,SavePath);message="Partida guardada · "+DateTime.Now.ToString("HH:mm");}
   catch(Exception e){message="No se pudo guardar: "+e.Message;Debug.LogWarning(message);}
  }
  public void LoadGame()
  {
   try{var data=JsonUtility.FromJson<Data>(File.ReadAllText(SavePath));if(data==null||data.version!=2||data.owned==null||!float.IsFinite(data.position.x)||!float.IsFinite(data.position.y)||!float.IsFinite(data.position.z))throw new InvalidDataException("Archivo no válido");pending=data;startNew=false;Time.timeScale=1;ReloadScene();}
   catch(Exception e){message="No se pudo cargar: "+e.Message;}
  }
  void OnGUI()
  {
   if(!menu)return;var matrix=GUI.matrix;NexoHud.Scale();
   float scale=Mathf.Min(Screen.width/1280f,Screen.height/720f);
   NexoHud.Fill(new Rect(-(Screen.width/scale-1280)*.5f,-(Screen.height/scale-720)*.5f,Screen.width/scale,Screen.height/scale),new Color(.005f,.025f,.04f,.8f));
   NexoHud.Panel(new Rect(60,60,480,590));NexoHud.Label(new Rect(90,92,420,65),"NEXOTERRA",48,Color.white);
   NexoHud.Label(new Rect(94,164,410,32),started?"EXPLORACIÓN EN PAUSA":"CRÓNICAS DEL SUBSUELO",18,NexoHud.Cyan);
   NexoHud.Label(new Rect(94,215,400,95),"Seis biomas. Criaturas únicas.\nTu colección comienza explorando.",21,Color.white);
   NexoHud.Label(new Rect(94,470,400,120),"WASD · caminar   Mayús · correr\nRatón derecho · apuntar\nRatón izquierdo · disparar\nB · mochila   R · recargar   Esc · menú",16,new Color(.65f,.8f,.83f));
   NexoHud.Panel(new Rect(640,92,480,554));float y=120;
   if(confirmNew){NexoHud.Label(new Rect(670,y,420,110),"¿Empezar una partida nueva?\nEl progreso sin guardar se perderá.\nTu archivo guardado se conserva.",19,Color.white);y+=125;if(NexoHud.Button(new Rect(672,y,416,58),"EMPEZAR VACÍO"))NewGame();y+=72;if(NexoHud.Button(new Rect(672,y,416,58),"VOLVER"))confirmNew=false;}
   else
   {
    if(started){if(NexoHud.Button(new Rect(672,y,416,58),"REANUDAR"))Resume();y+=72;}
    else {GUI.enabled=HasSave;if(NexoHud.Button(new Rect(672,y,416,58),"CONTINUAR"))LoadGame();GUI.enabled=true;y+=72;}
    if(NexoHud.Button(new Rect(672,y,416,58),"NUEVA PARTIDA")){if(started)confirmNew=true;else NewGame();}y+=72;
    GUI.enabled=started;if(NexoHud.Button(new Rect(672,y,416,58),"GUARDAR PARTIDA"))SaveGame();GUI.enabled=true;y+=72;
    GUI.enabled=HasSave;if(NexoHud.Button(new Rect(672,y,416,58),"CARGAR GUARDADO"))LoadGame();GUI.enabled=true;y+=72;
    if(NexoHud.Button(new Rect(672,y,416,58),"MENÚ PRINCIPAL")){FrontEndMenu.Load(FrontEndMenu.MenuScene);}
    y+=72;

   }
   NexoHud.Label(new Rect(665,570,430,64),message,15,NexoHud.Cyan);GUI.matrix=matrix;
  }
 }
}
