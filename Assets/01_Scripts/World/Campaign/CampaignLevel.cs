using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace Nexoterra.World
{
 public sealed class CampaignLevel : MonoBehaviour
 {
  public static CampaignLevel Current {get;private set;}
  public const int Revision=2;
  public static readonly string[] Scenes={"Nexoterra_Jardines","Nexoterra_Hielo","Nexoterra_Agua","Nexoterra_Tierra","Nexoterra_Volcan","Nexoterra_Carmesi"};
  public static readonly string[] Titles={"Jardines del Nexo","Cavernas de hielo","Galería acuática","Tierras antiguas","Caldera volcánica","Ciénaga carmesí"};
  public static readonly string[] Rewards={"Elemental_Energia","Elemental_Aire","Elemental_Agua","Elemental_Tierra","Elemental_Fuego",""};
  public int index;
  public Transform entrance,exit,rewardPoint;
  public EnemyCombatant[] enemies;
  public EnemyCombatant guardian;
  public EnemyCombatant trapGuard,ambushBoss;
  public SlugCatalog catalog;
  public string introduction;
  readonly HashSet<int> defeated=new HashSet<int>();
  readonly HashSet<string> collectedLoot=new HashSet<string>();
  public bool LootCollected(string key)=>collectedLoot.Contains(key);
  public void CollectLoot(string key){if(!string.IsNullOrEmpty(key))collectedLoot.Add(key);}
  public string[] CaptureLoot()=>collectedLoot.ToArray();
  public void RestoreLoot(string[] keys){collectedLoot.Clear();if(keys!=null)foreach(var key in keys)collectedLoot.Add(key);}
  WildSlug reward;
  bool transitioning;
  float introUntil;
  NexoterraThirdPerson player;
  SlugInventory inventory;
  public int Remaining=>enemies.Count(e=>e&&(!e.gameObject.activeInHierarchy||!e.IsDead));
  public bool GuardianDefeated=>guardian&&guardian.gameObject.activeInHierarchy&&guardian.IsDead;
  public bool Cleared=>Remaining==0&&(string.IsNullOrEmpty(Rewards[index])||inventory&&inventory.owned.Contains(Rewards[index])&&GuardianDefeated);
  void Awake(){index=Mathf.Clamp(index,0,Scenes.Length-1);Current=this;introUntil=Time.unscaledTime+18;}
  void Start(){
   player=FindAnyObjectByType<NexoterraThirdPerson>();inventory=player.GetComponent<SlugInventory>();
   for(int i=0;i<enemies.Length;i++){int slot=i;var e=enemies[i];if(!e)continue;var ai=e.GetComponent<EnemyShooter>();if(ai)ai.campaignMember=true;e.onDeath.AddListener(()=>Defeated(slot));}
  }
  void OnDestroy(){if(Current==this)Current=null;}
  void Defeated(int slot){
   defeated.Add(slot);
   if(enemies[slot]==trapGuard&&ambushBoss){ambushBoss.gameObject.SetActive(true);player.Notify("¡Era una trampa! Sir Black tiene la elemental de fuego. Derrota al verdadero guardián.");introUntil=Time.unscaledTime+12;}
   if(enemies[slot]==guardian)EnsureReward();
  }
  void EnsureReward(){
   string id=Rewards[index];if(string.IsNullOrEmpty(id)||reward||inventory&&inventory.HasSpecies(id))return;
   var definition=catalog.Find(index==0?id:"Malvada_"+id);if(definition==null)return;
   reward=EnemySlugLoot.Spawn(definition,guardian?guardian.transform.position:rewardPoint.position,"elemental:"+index);
   player.Notify(index==0?"Energía está libre. Aliméntala con F para obtenerla.":"El guardián soltó la elemental. Cúrala con Doc o Energía y aliméntala con F.");
  }
  public int[] CaptureDefeated()=>defeated.ToArray();
  public void RestoreDefeated(int[] slots){
   if(slots==null)return;
   foreach(int slot in slots)if(slot>=0&&slot<enemies.Length&&enemies[slot]){defeated.Add(slot);enemies[slot].ReceiveDamage(enemies[slot].maxHealth+1);}
   if(GuardianDefeated)EnsureReward();
  }
  void Update(){
   if(!player||GameSession.Blocked||transitioning)return;
   if(Vector3.Distance(player.transform.position,exit.position)>3)return;
   if(!Cleared){player.Notify(Remaining>0?"Derrota a los "+Remaining+" enemigos restantes.":index==0?"Alimenta a Energía con F antes de continuar.":"Cura y domestica la elemental antes de continuar.");return;}
   transitioning=true;
   if(index==Scenes.Length-1){GameSession.Current.SaveGame();FrontEndMenu.Load(FrontEndMenu.MenuScene);}
   else GameSession.Current.Travel(Scenes[index+1]);
  }
  public bool IsReward(WildSlug slug)=>slug&&slug==reward;
  // Mission checklist (top-center): one line per objective, checked when done; the first open one is highlighted.
  readonly List<(string text,bool done)> checklist=new List<(string,bool)>();
  void BuildChecklist(){
   checklist.Clear();
   var patrol=enemies.Where(e=>e&&e!=guardian&&e!=ambushBoss).ToArray();int down=patrol.Count(e=>e.gameObject.activeInHierarchy&&e.IsDead);
   if(patrol.Length>0)checklist.Add(("Derrota a la patrulla  "+down+"/"+patrol.Length,down==patrol.Length));
   if(guardian)checklist.Add(("Derrota a "+guardian.displayName,GuardianDefeated));
   if(ambushBoss&&ambushBoss.gameObject.activeInHierarchy)checklist.Add(("Derrota a "+ambushBoss.displayName,ambushBoss.IsDead));
   if(!string.IsNullOrEmpty(Rewards[index]))checklist.Add((index==0?"Alimenta a Energía con F":"Cura la elemental y aliméntala con F",inventory&&inventory.owned.Contains(Rewards[index])));
   checklist.Add((index==Scenes.Length-1?"Regresa por el túnel":"Cruza el túnel al siguiente bioma",false));
  }
  void OnGUI(){
   if(!player||GameSession.Blocked||(inventory&&inventory.IsOpen))return;var before=GUI.matrix;NexoHud.Scale();
   if(Event.current.type==EventType.Layout)BuildChecklist();
   const float row=22;var inner=NexoHud.Plate(new Rect(470,14,340,40+checklist.Count*row));
   NexoUi.Write(new Rect(inner.x+12,inner.y+4,inner.width-24,20),Titles[index].ToUpperInvariant(),13,Color.white,TextAnchor.MiddleLeft,true);
   int current=checklist.FindIndex(c=>!c.done);float y=inner.y+28;
   for(int i=0;i<checklist.Count;i++,y+=row){
    var (text,done)=checklist[i];bool active=i==current;var box=new Rect(inner.x+12,y+4,14,14);
    NexoHud.Fill(box,done?NexoUi.Teal:active?NexoUi.Amber:NexoUi.Edge);NexoHud.Fill(new Rect(box.x+2,box.y+2,box.width-4,box.height-4),done?NexoUi.Teal:new Color(.02f,.05f,.06f));
    if(done)NexoUi.Write(box,"✓",12,new Color(.02f,.1f,.1f),TextAnchor.MiddleCenter,true);
    NexoUi.Write(new Rect(box.xMax+10,y,inner.width-50,row),text,13,done?NexoUi.Muted:active?Color.white:NexoUi.Muted,TextAnchor.MiddleLeft,active);
   }
   GUI.matrix=before;
  }
  public void DrawMap(GUIStyle label,GUIStyle title){
   float x=Screen.width*.5f-240,y=Screen.height*.5f-240;GUI.Box(new Rect(x-16,y-45,512,545),GUIContent.none);
   GUI.Label(new Rect(x,y-35,480,30),Titles[index]+" · 250 × 250 m",title);NexoHud.Fill(new Rect(x,y,480,480),new Color(.09f,.18f,.18f,.98f));
   Vector2 Point(Vector3 p)=>new Vector2(x+Mathf.InverseLerp(-125,125,p.x)*480,y+(1-Mathf.InverseLerp(-125,125,p.z))*480);
   foreach(var e in enemies)if(e&&!e.IsDead){var p=Point(e.transform.position);NexoHud.Fill(new Rect(p.x-3,p.y-3,6,6),Color.red);}
   var pos=Point(player.transform.position);NexoHud.Fill(new Rect(pos.x-4,pos.y-4,8,8),Color.cyan);
   var gate=Point(exit.position);var previous=GUI.color;GUI.color=Color.yellow;GUI.Label(new Rect(gate.x-12,gate.y-22,30,30),"▲",title);GUI.Label(new Rect(gate.x-75,gate.y+8,150,30),"TÚNEL",label);GUI.color=previous;
  }
 }
}


