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
  void OnGUI(){
   if(!player||GameSession.Blocked||(inventory&&inventory.IsOpen))return;var before=GUI.matrix;NexoHud.Scale();
   bool intro=Time.unscaledTime<introUntil;
   var inner=NexoHud.Plate(new Rect(446,14,388,intro?172:78));
   NexoHud.Label(new Rect(inner.x+10,inner.y+2,inner.width-20,24),Titles[index].ToUpperInvariant(),16,Color.white,TextAnchor.MiddleCenter);
   NexoHud.Label(new Rect(inner.x+10,inner.y+24,inner.width-20,16),Remaining>0?Remaining+" ENEMIGOS RESTANTES":"ZONA DESPEJADA",11,NexoHud.Orange,TextAnchor.MiddleCenter);
   string objective=Cleared?(index==Scenes.Length-1?"Victoria. Regresa por el túnel.":"Túnel abierto · sigue la flecha amarilla"):Remaining>0?(index==4&&ambushBoss&&ambushBoss.gameObject.activeSelf?"¡Trampa! Sir Black tiene la elemental de fuego.":index==5?"Derrota a Enemy2, al secuaz y a Sir Black 2.":"Derrota a la patrulla y al secuaz."):index==0?"Alimenta a Energía con F para obtenerla.":"Cura la elemental con Doc/Energía y aliméntala con F.";
   NexoHud.Label(new Rect(inner.x+10,inner.y+40,inner.width-20,24),objective,13,new Color(.55f,1,.95f),TextAnchor.MiddleCenter);
   if(intro)NexoHud.Label(new Rect(inner.x+12,inner.y+68,inner.width-24,92),introduction,13,Color.white,TextAnchor.UpperCenter);GUI.matrix=before;
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


