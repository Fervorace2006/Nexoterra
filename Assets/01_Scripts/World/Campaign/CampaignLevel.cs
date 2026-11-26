using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace Nexoterra.World
{
 public sealed class CampaignLevel : MonoBehaviour
 {
  public static CampaignLevel Current {get;private set;}
  public const int Revision=3;
  public static readonly string[] Scenes={"Nexoterra_Jardines","Nexoterra_Hielo","Nexoterra_Agua","Nexoterra_Tierra","Nexoterra_Volcan","Nexoterra_Carmesi","Nexoterra_Final"};
  public static readonly string[] Titles={"Jardines del Nexo","Cavernas de hielo","Galería acuática","Tierras antiguas","Caldera volcánica","Ciénaga carmesí","Nexoterra Final"};
  public static readonly string[] Rewards={"Elemental_Energia","Elemental_Aire","Elemental_Agua","Elemental_Tierra","Elemental_Fuego","",""};
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
  void Awake(){index=Mathf.Clamp(index,0,Scenes.Length-1);Current=this;introUntil=Time.unscaledTime+18;EnsureAltarColliders();}
  void Start(){
   player=FindAnyObjectByType<NexoterraThirdPerson>();inventory=player.GetComponent<SlugInventory>();
   for(int i=0;i<enemies.Length;i++){int slot=i;var e=enemies[i];if(!e)continue;var ai=e.GetComponent<EnemyShooter>();if(ai)ai.campaignMember=true;e.onDeath.AddListener(()=>Defeated(slot));}
   if(index==4){ArrangeVolcanoOutpost();ArrangeVolcanoLava();}
   RefreshReturnedVisual();
  }
  void EnsureAltarColliders(){
   foreach(var mesh in FindObjectsByType<MeshFilter>()){
    if(!mesh||!mesh.sharedMesh)continue;string objectName=mesh.gameObject.name;
    if(!objectName.StartsWith("Santuario_")&&!objectName.StartsWith("Altar_")&&!objectName.StartsWith("Templo_"))continue;
    var collider=mesh.GetComponent<MeshCollider>();if(!collider)collider=mesh.gameObject.AddComponent<MeshCollider>();
    collider.sharedMesh=null;collider.convex=false;collider.isTrigger=false;collider.sharedMesh=mesh.sharedMesh;
   }
   Physics.SyncTransforms();
  }
  void ArrangeVolcanoLava(){
   var volcano=GameObject.Find("Volcan_caldera");var lava=GameObject.Find("Lava_en_crater");if(!volcano||!lava)return;
   Vector3 volcanoPosition=volcano.transform.localPosition,lavaPosition=lava.transform.localPosition;
   lava.transform.localPosition=new Vector3(volcanoPosition.x,lavaPosition.y,volcanoPosition.z);
   var mesh=lava.GetComponent<MeshFilter>();if(!mesh||!mesh.sharedMesh)return;
   var surface=lava.GetComponent<MeshCollider>();if(!surface)surface=lava.AddComponent<MeshCollider>();
   surface.sharedMesh=null;surface.convex=false;surface.isTrigger=false;surface.sharedMesh=mesh.sharedMesh;
   var hazard=lava.GetComponent<LavaHazard>();if(!hazard)hazard=lava.AddComponent<LavaHazard>();hazard.surface=surface;
   Physics.SyncTransforms();
  }
  readonly List<Material> outpostMaterials=new List<Material>();
  void ArrangeVolcanoOutpost(){
   if(!trapGuard)return;
   Vector3 p=new Vector3(72,0,4);if(Physics.Raycast(p+Vector3.up*80,Vector3.down,out var hit,160,SlugHop.WorldMask,QueryTriggerInteraction.Ignore))p.y=hit.point.y;
   trapGuard.transform.position=p+Vector3.up*.1f;
   var root=new GameObject("Avanzada volcánica de Sir Black");root.transform.position=p;
   var mat=OutpostMaterial(new Color(.12f,.16f,.18f));var trim=OutpostMaterial(new Color(.52f,.25f,.09f));
   AddBlock(root.transform,"Muro posterior",new Vector3(0,2.2f,6),new Vector3(18,4.4f,1),mat);
   AddBlock(root.transform,"Muro lateral oeste",new Vector3(-8.5f,2.2f,0),new Vector3(1,4.4f,12),mat);
   AddBlock(root.transform,"Muro lateral este",new Vector3(8.5f,2.2f,0),new Vector3(1,4.4f,12),mat);
   AddBlock(root.transform,"Torre baja",new Vector3(-8.5f,5.2f,5),new Vector3(3,1.6f,3),trim);
   AddBlock(root.transform,"Torre baja",new Vector3(8.5f,5.2f,5),new Vector3(3,1.6f,3),trim);
   AddBlock(root.transform,"Plataforma de vigilancia",new Vector3(0,4.4f,6),new Vector3(20,.55f,3),trim);
  }
  Material OutpostMaterial(Color color){var shader=Shader.Find("Universal Render Pipeline/Lit");if(!shader)shader=Shader.Find("Standard");var m=new Material(shader);m.color=color;outpostMaterials.Add(m);return m;}
  static void AddBlock(Transform parent,string name,Vector3 position,Vector3 scale,Material material){var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localScale=scale;go.GetComponent<Renderer>().sharedMaterial=material;}
  void OnDestroy(){if(Current==this)Current=null;foreach(var m in outpostMaterials)if(m)Destroy(m);outpostMaterials.Clear();}
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
   GameSession.Current.MarkLevelCompleted(index);
   if(index==Scenes.Length-1){GameSession.Current.MarkCampaignComplete();GameSession.Current.SaveGame();FrontEndMenu.Load(FrontEndMenu.MenuScene);}
   else GameSession.Current.Travel(Scenes[index+1]);
  }
  public bool TryReturnElemental(){
   if(!player||!GameSession.Current||!GameSession.Current.CampaignComplete||index>=GameSession.Current.ReturnedElementals.Length)return false;
   string species=Rewards[index];if(string.IsNullOrEmpty(species)||GameSession.Current.HasReturnedElemental(index))return false;
   if(!inventory||!inventory.HasSpecies(species))return false;
   Vector3 site=ReturnSite();if(Vector3.Distance(player.transform.position,site)>3){return false;}
   GameSession.Current.MarkElementalReturned(index);RefreshReturnedVisual();GameSession.Current.SaveGame();player.Notify("Elemental devuelta a su bioma. Conservas tu copia.");return true;
  }
  Vector3 ReturnSite(){var p=entrance?entrance.position+new Vector3(4,0,4):transform.position;if(Physics.Raycast(p+Vector3.up*12,Vector3.down,out var hit,24,SlugHop.WorldMask,QueryTriggerInteraction.Ignore))p.y=hit.point.y;return p;}
  GameObject returnDisplay;
  public void RefreshReturnedVisual(){
   if(returnDisplay){Destroy(returnDisplay);returnDisplay=null;}
   if(!GameSession.Current||!GameSession.Current.CampaignComplete||index>=5)return;
   string id=Rewards[index];var def=catalog?catalog.Find(id):null;if(def==null||!def.normal)return;
   returnDisplay=new GameObject("Elemental asentada · "+id);returnDisplay.transform.position=ReturnSite();
   var basePart=GameObject.CreatePrimitive(PrimitiveType.Cylinder);basePart.name="Santuario de retorno";basePart.transform.SetParent(returnDisplay.transform,false);basePart.transform.localPosition=Vector3.zero;basePart.transform.localScale=new Vector3(1.25f,.12f,1.25f);Destroy(basePart.GetComponent<Collider>());
   var marker=GameObject.CreatePrimitive(PrimitiveType.Sphere);marker.name="Luz del santuario";marker.transform.SetParent(returnDisplay.transform,false);marker.transform.localPosition=Vector3.up*.48f;marker.transform.localScale=Vector3.one*.24f;Destroy(marker.GetComponent<Collider>());var markerRenderer=marker.GetComponent<Renderer>();var markerBlock=new MaterialPropertyBlock();markerRenderer.GetPropertyBlock(markerBlock);markerBlock.SetColor("_Color",new Color(.2f,.9f,.86f));markerBlock.SetColor("_BaseColor",new Color(.2f,.9f,.86f));markerRenderer.SetPropertyBlock(markerBlock);
   if(!GameSession.Current.HasReturnedElemental(index))return;
   var body=Instantiate(def.normal,returnDisplay.transform);foreach(var c in body.GetComponentsInChildren<Collider>())Destroy(c);foreach(var b in body.GetComponentsInChildren<Rigidbody>()){b.isKinematic=true;b.detectCollisions=false;}foreach(var wild in body.GetComponentsInChildren<WildSlug>())wild.enabled=false;WildSlug.Fit(returnDisplay.transform,body.transform);body.transform.localPosition+=Vector3.up*.18f;
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
   if(GameSession.Current&&GameSession.Current.CampaignComplete&&index<5)checklist.Add((GameSession.Current.HasReturnedElemental(index)?"Elemental devuelta":"Devuelve "+SlugCatalog.BaseSpecies(Rewards[index])+" a su bioma · acércate y pulsa F",GameSession.Current.HasReturnedElemental(index)));
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
    if(done)NexoUi.DrawKit(new Rect(box.x-1,box.y-2,17,13),"Icon_Check");
    NexoUi.Write(new Rect(box.xMax+10,y,inner.width-50,row),text,13,done?NexoUi.Muted:active?Color.white:NexoUi.Muted,TextAnchor.MiddleLeft,active);
   }
   if(returnDisplay&&GameSession.Current&&index<5&&!GameSession.Current.HasReturnedElemental(index)&&inventory.HasSpecies(Rewards[index])&&Vector3.Distance(player.transform.position,ReturnSite())<=4)NexoUi.Write(new Rect(410,535,460,34),"PULSA F PARA DEVOLVER LA ELEMENTAL A SU BIOMA · CONSERVAS TU COPIA",14,Color.white,TextAnchor.MiddleCenter,true);
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


