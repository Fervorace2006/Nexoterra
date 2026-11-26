using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Nexoterra.World
{
 public sealed class ArenaSession : MonoBehaviour
 {
  [Serializable] public sealed class Packet {
   public string kind,species; public ulong id,other;public Vector3 position,direction;public Quaternion rotation;
   public string name;public bool female,transformed,evading,targeted,golden;public float health,time0,time1,upper,eventTime;public int state0,state1; public string[] collection,goldenCollection;
  }
  const string NameKey="Nexoterra.PlayerName";
  public static string PlayerName{get{var n=PlayerPrefs.GetString(NameKey,"");return string.IsNullOrWhiteSpace(n)?"Lanzador":n;}set{var n=(value??"").Trim();if(n.Length>16)n=n.Substring(0,16);PlayerPrefs.SetString(NameKey,n);PlayerPrefs.Save();}}
  static string Clean(string n){n=(n??"").Trim();if(n.Length>16)n=n.Substring(0,16);return n.Length==0?"Lanzador":n;}
  // One distinct colour per player for the name tags.
  static readonly Color[] TagColors={new Color(.3f,1,.95f),new Color(1,.62f,.15f),new Color(.55f,1,.3f),new Color(1,.4f,.9f),new Color(1,.95f,.3f),new Color(.55f,.65f,1f)};
  sealed class Peer {
   public string name="";
   public Vector3 velocity,lastPosePosition;public float lastPoseTime=-1,viewTime;   // for latency-compensated prediction
   public string[] collection,goldenCollection;public GameObject proxy;public Vector3 position;public Quaternion rotation;
   public float health=100,lastShot=-10,deadUntil,burnUntil,burnRate,lastMove;
   public bool female,visualFemale,evading;public Animator animator;
   // Remote view smoothing: last received pose, estimated velocity and animation states.
   public Vector3 viewTarget;public Quaternion viewRotation=Quaternion.identity;public float lastView=-1;public int shown0,shown1;
   public readonly Dictionary<string,float> busyUntil=new Dictionary<string,float>();
  }
  public static ArenaSession Current {get;private set;}
  public bool LocalPaused {get;private set;}
  readonly Dictionary<ulong,Peer> peers=new Dictionary<ulong,Peer>();
  NetworkManager network;
  NexoterraThirdPerson player;SlugInventory inventory;PlayerHealth health;
  float sendAt,helloAt;bool registered;
  SlugProjectile predictedHostShot;
  public bool IsServer=>network&&network.IsServer;
  public ulong LocalClientId=>network?network.LocalClientId:ulong.MaxValue;
  public bool TryGetRivalPosition(ulong id,out Vector3 position){if(peers.TryGetValue(id,out var peer)&&peer.proxy){position=peer.proxy.transform.position;return true;}position=default;return false;}
  public IEnumerable<KeyValuePair<ulong,Vector3>> RivalPositions(){foreach(var pair in peers)if(pair.Key!=LocalClientId&&pair.Value.proxy)yield return new KeyValuePair<ulong,Vector3>(pair.Key,pair.Value.proxy.transform.position);}
  public bool IsRivalEvading(ulong id)=>peers.TryGetValue(id,out var peer)&&peer.evading;
  void Awake(){Current=this;}
  System.Collections.IEnumerator Start(){
   player=FindAnyObjectByType<NexoterraThirdPerson>();inventory=player.GetComponent<SlugInventory>();health=player.GetComponent<PlayerHealth>();
   yield return null;
   PrepareArenaEnvironment();
   var room=MultiplayerRoom.Current;
   if(!room||!room.Connected){FrontEndMenu.Load(FrontEndMenu.MenuScene);yield break;}
   network=room.Manager;
   network.CustomMessagingManager.RegisterNamedMessageHandler("arena",Receive);registered=true;
   network.OnClientDisconnectCallback+=RemovePeer;
   var own=player.gameObject.AddComponent<ArenaTarget>();own.clientId=network.LocalClientId;
   GameSession.Data data=null;try{data=GameSession.ReadCollection();}catch(Exception e){Debug.LogWarning("Colección no disponible: "+e.Message);}
   if(data!=null){
    inventory.owned=data.owned.Where(id=>inventory.catalog.Find(id)!=null).ToList();
    inventory.goldenOwned=new List<bool>();for(int i=0;i<inventory.owned.Count;i++)inventory.goldenOwned.Add(data.goldenOwned!=null&&i<data.goldenOwned.Length&&data.goldenOwned[i]);
    inventory.equipped=new[]{-1,-1,-1,-1,-1,-1};for(int i=0;i<Mathf.Min(6,inventory.owned.Count);i++)inventory.equipped[i]=i;
   }else{inventory.owned.Clear();inventory.goldenOwned.Clear();inventory.equipped=new[]{-1,-1,-1,-1,-1,-1};inventory.Add("Infierno");}
   inventory.persist=false;
   SendToServer(new Packet{kind="hello",name=PlayerName,collection=inventory.owned.Distinct().ToArray(),goldenCollection=GoldenSpecies()});
   player.Teleport(Spawn(network.LocalClientId));health.Restore(health.maxHealth);
  }
  static Vector3 Spawn(ulong id){Vector3[] points={new Vector3(-85,.3f,-90),new Vector3(85,.3f,85),new Vector3(-85,.3f,85),new Vector3(85,.3f,-90)};return points[(int)(id%4)];}
  void OnDestroy(){
   if(registered&&network){network.CustomMessagingManager.UnregisterNamedMessageHandler("arena");network.OnClientDisconnectCallback-=RemovePeer;}
   foreach(var peer in peers.Values)if(peer.proxy)Destroy(peer.proxy);
   if(Current==this)Current=null;
  }
  void PrepareArenaEnvironment(){
   foreach(var t in FindObjectsByType<Transform>(FindObjectsSortMode.None))if(t&&t.parent==null&&(t.name.Contains("Santuario")||t.name.Contains("Elemental")||t.name.Contains("Recompensa del guardián")))Destroy(t.gameObject);
  }
  void RemovePeer(ulong id){
   if(peers.TryGetValue(id,out var peer)){if(peer.proxy)Destroy(peer.proxy);peers.Remove(id);}
   if(IsServer)Broadcast(new Packet{kind="leave",id=id});
  }
  void Send(ulong to,Packet packet,NetworkDelivery delivery=NetworkDelivery.ReliableSequenced){
   if(!network||!network.IsListening)return;
   string json=JsonUtility.ToJson(packet);
   using(var writer=new FastBufferWriter(4096,Allocator.Temp)){writer.WriteValueSafe(json);network.CustomMessagingManager.SendNamedMessage("arena",to,writer,delivery);}
  }
  void SendToServer(Packet packet,NetworkDelivery delivery=NetworkDelivery.ReliableSequenced){if(IsServer)HandleServer(network.LocalClientId,packet);else Send(NetworkManager.ServerClientId,packet,delivery);}
  void Broadcast(Packet packet,NetworkDelivery delivery=NetworkDelivery.ReliableSequenced){foreach(var id in network.ConnectedClientsIds){if(id==network.LocalClientId)HandleClient(packet);else Send(id,packet,delivery);}}
  void Receive(ulong sender,FastBufferReader reader){
   try{reader.ReadValueSafe(out string json);if(json.Length>12000)return;var packet=JsonUtility.FromJson<Packet>(json);if(packet==null)return;
    if(IsServer)HandleServer(sender,packet);else if(sender==NetworkManager.ServerClientId)HandleClient(packet);
   }catch(Exception e){Debug.LogWarning("Mensaje de arena descartado: "+e.Message);}
  }
  void HandleServer(ulong sender,Packet packet){
   if(packet.kind=="hello"){
    if(peers.ContainsKey(sender)){Send(sender,new Packet{kind="join",name=peers[sender].name,id=sender,position=peers[sender].position,health=peers[sender].health});return;}
    var owned=(packet.collection??Array.Empty<string>()).Where(id=>inventory.catalog.Find(id)!=null).Distinct().Take(64).ToArray();
    peers[sender]=new Peer{name=Clean(packet.name),collection=owned,goldenCollection=packet.goldenCollection??Array.Empty<string>(),position=Spawn(sender),rotation=Quaternion.identity,lastMove=Time.unscaledTime};
    foreach(var pair in peers)Send(sender,new Packet{kind="join",name=pair.Value.name,id=pair.Key,position=pair.Value.position,rotation=pair.Value.rotation,health=pair.Value.health});
    Broadcast(new Packet{kind="join",name=peers[sender].name,id=sender,position=Spawn(sender),rotation=Quaternion.identity,health=100});return;
   }
   if(!peers.TryGetValue(sender,out var peer))return;
   if(packet.kind=="pose"){
    if(!Finite(packet.position)||!Finite(new Vector3(packet.rotation.x,packet.rotation.y,packet.rotation.z))||!float.IsFinite(packet.rotation.w))return;
    float elapsed=Mathf.Clamp(Time.unscaledTime-peer.lastMove,.01f,.5f);peer.lastMove=Time.unscaledTime;
    if(peer.health<=0)return;
    Vector3 clamped=packet.position;clamped.x=Mathf.Clamp(clamped.x,-122,122);clamped.z=Mathf.Clamp(clamped.z,-122,122);clamped.y=Mathf.Clamp(clamped.y,-5,25);
    float allowedStep=elapsed*22f+.5f;
    peer.position=Vector3.Distance(peer.position,clamped)>allowedStep?Vector3.MoveTowards(peer.position,clamped,allowedStep):clamped;peer.rotation=packet.rotation;peer.female=packet.female;peer.evading=packet.evading;
    packet.id=sender;packet.position=peer.position;packet.health=peer.health;Broadcast(packet,NetworkDelivery.UnreliableSequenced);return;
   }
   if(packet.kind=="shot"){
    if(peer.health<=0||Time.unscaledTime-peer.lastShot<.7f||!peer.collection.Contains(packet.species)||!Finite(packet.direction)||packet.direction.sqrMagnitude<.5f)return;
    if(peer.busyUntil.TryGetValue(packet.species,out float available)&&Time.unscaledTime<available)return;
    var definition=inventory.catalog.Find(packet.species);if(definition==null)return;
    peer.lastShot=Time.unscaledTime;peer.busyUntil[packet.species]=Time.unscaledTime+1.4f;
    packet.id=sender;packet.direction.Normalize();packet.eventTime=Mathf.Clamp(packet.eventTime,(float)network.ServerTime.TimeAsFloat-1f,(float)network.ServerTime.TimeAsFloat);
    if(!packet.targeted||packet.other==sender||!peers.ContainsKey(packet.other))packet.targeted=false;
    packet.golden=packet.golden&&peer.goldenCollection!=null&&peer.goldenCollection.Contains(packet.species);
    if(!Finite(packet.position)||Vector3.Distance(packet.position,peer.position)>3)packet.position=peer.position+Vector3.up*1.3f;
    if(definition.id=="Gon_Doc"||definition.id=="Elemental_Energia")peer.health=Mathf.Min(100,peer.health+(definition.id=="Gon_Doc"?25:32));
    if(definition.id.StartsWith("Malvada_"))peer.health=Mathf.Max(1,peer.health-5);
    if(sender==network.LocalClientId&&predictedHostShot)predictedHostShot.arenaAuthority=true;else Launch(packet,true);Broadcast(packet,NetworkDelivery.Reliable);return;
   }
  }
  static bool Finite(Vector3 p)=>float.IsFinite(p.x)&&float.IsFinite(p.y)&&float.IsFinite(p.z);
  // Every frame: interpolate remote players toward their newest server-authoritative pose.
  void SmoothRemotePlayers(){
   float k=1-Mathf.Exp(-22f*Time.unscaledDeltaTime);
   foreach(var pair in peers){
    var peer=pair.Value;if(pair.Key==network.LocalClientId||!peer.proxy||peer.lastView<0)continue;
    var t=peer.proxy.transform;
    // Never extrapolate a dodge past the newest authoritative pose: that made the
    // proxy lurch ahead and then visibly correct backwards when the roll ended.
    // Show where the peer is *now*: newest pose + velocity × packet age (capped, and zero while rolling).
    float age=Mathf.Clamp((float)network.ServerTime.TimeAsFloat-peer.viewTime,0,.15f);
    var target=peer.viewTarget+peer.velocity*age;
    t.position=(t.position-target).sqrMagnitude>100?target:Vector3.Lerp(t.position,target,k);
    t.rotation=Quaternion.Slerp(t.rotation,peer.viewRotation,k);
   }
  }
  void HandleClient(Packet packet){
   if(packet.kind=="leave"){if(peers.TryGetValue(packet.id,out var old)&&old.proxy)Destroy(old.proxy);if(!IsServer)peers.Remove(packet.id);return;}
   if(packet.kind=="join"||packet.kind=="pose"){
    if(!peers.TryGetValue(packet.id,out var peer)){peer=new Peer();peers.Add(packet.id,peer);}
    if(packet.kind=="join"&&!string.IsNullOrEmpty(packet.name))peer.name=Clean(packet.name);
    peer.position=packet.position;peer.rotation=packet.rotation;peer.health=packet.health;
    if(packet.id==network.LocalClientId){health.ApplyArenaHealth(packet.health);return;}
    if(!peer.proxy)CreateProxy(packet.id,peer);
    // Don't move the proxy here (packets arrive ~30/s); SmoothRemotePlayers interpolates every frame.
    float now=Time.unscaledTime;
    if(peer.lastView<=0){peer.proxy.transform.position=packet.position;peer.proxy.transform.rotation=packet.rotation;}
    if(packet.kind=="join")peer.proxy.transform.position=packet.position;
    if(packet.kind=="pose"){
     float dt=packet.eventTime-peer.lastPoseTime;
     if(peer.lastPoseTime>0&&dt>.005f&&dt<.5f&&!packet.evading){var v=(packet.position-peer.lastPosePosition)/dt;v.y=0;if(v.magnitude>12)v=v.normalized*12;peer.velocity=Vector3.Lerp(peer.velocity,v,.6f);}
     else if(packet.evading||dt>=.5f)peer.velocity=Vector3.zero;
     peer.lastPosePosition=packet.position;peer.lastPoseTime=packet.eventTime;peer.viewTime=packet.eventTime;
    }else{peer.velocity=Vector3.zero;peer.viewTime=(float)network.ServerTime.TimeAsFloat;}
    peer.viewTarget=packet.position;peer.viewRotation=packet.rotation;peer.lastView=now;
    if(packet.kind=="pose"&&peer.animator){
     if(peer.visualFemale!=packet.female){Destroy(peer.proxy);peer.proxy=null;peer.female=packet.female;peer.evading=packet.evading;CreateProxy(packet.id,peer);}
     // Change animation only when the remote state changes; otherwise let it play smoothly.
     if(packet.state0!=peer.shown0&&peer.animator.HasState(0,packet.state0)){peer.animator.CrossFade(packet.state0,.12f,0,packet.time0);peer.shown0=packet.state0;}
     if(peer.animator.layerCount>1){peer.animator.SetLayerWeight(1,packet.upper);if(packet.state1!=peer.shown1&&peer.animator.HasState(1,packet.state1)){peer.animator.CrossFade(packet.state1,.1f,1,packet.time1);peer.shown1=packet.state1;}}
    }return;
   }
   if(packet.kind=="shot"){if(!IsServer&&packet.id!=network.LocalClientId)Launch(packet,false);return;}
   if(packet.kind=="hit"){
    if(peers.TryGetValue(packet.id,out var peer)){peer.health=packet.health;var effect=inventory.catalog.Find(packet.species);if(peer.proxy&&effect!=null&&packet.transformed){var controls=peer.proxy.GetComponent<EnemyControlEffects>();if(SlugCatalog.BaseSpecies(effect.id)!="Elemental_Tierra")controls.CancelStone();controls.Apply(effect.id,packet.direction);if(effect.Combat.burnDuration>0){var fire=ImportedSlugVfx.BurningCharacter(peer.proxy.transform);if(fire)Destroy(fire,effect.Combat.burnDuration);}}}
    if(packet.id==network.LocalClientId){
     var def=inventory.catalog.Find(packet.species);if(def!=null)health.ReceiveSlug(def,packet.transformed,packet.direction,packet.golden);
     health.ApplyArenaHealth(packet.health);
    }return;
   }
   if(packet.kind=="respawn"){
    if(peers.TryGetValue(packet.id,out var peer)){peer.health=100;peer.position=peer.viewTarget=packet.position;peer.lastView=Time.unscaledTime;peer.velocity=Vector3.zero;peer.viewTime=(float)network.ServerTime.TimeAsFloat;if(peer.proxy){peer.proxy.transform.position=packet.position;peer.proxy.transform.rotation=packet.rotation;}}
    if(packet.id==network.LocalClientId){health.Restore(health.maxHealth);var effects=player.GetComponent<EnemyControlEffects>();if(effects)effects.ResetEffects();player.Teleport(packet.position);}
   }
  }
  void CreateProxy(ulong id,Peer peer){
   var root=new GameObject("Jugador "+id);root.transform.position=peer.position;root.transform.rotation=peer.rotation;
   peer.visualFemale=peer.female;var source=peer.female?player.female:player.male;
   var visual=Instantiate(source,root.transform);visual.SetActive(true);visual.transform.localPosition=source.transform.localPosition;visual.transform.localRotation=source.transform.localRotation;
   foreach(var c in visual.GetComponentsInChildren<Collider>(true))Destroy(c);
   foreach(var script in visual.GetComponentsInChildren<MonoBehaviour>(true))script.enabled=false;
   var col=root.AddComponent<CapsuleCollider>();col.height=2;col.center=Vector3.up;col.radius=.4f;
   var controls=root.AddComponent<EnemyControlEffects>();var sourceControls=player.GetComponent<EnemyControlEffects>();if(sourceControls)controls.webPrefab=sourceControls.webPrefab;
   root.AddComponent<ArenaTarget>().clientId=id;peer.proxy=root;peer.animator=visual.GetComponentInChildren<Animator>();
   if(peer.animator){peer.animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;peer.animator.applyRootMotion=false;}
  }
  void Launch(Packet packet,bool authority){
   var definition=inventory.catalog.Find(packet.species);if(definition==null)return;
   var go=new GameObject("Babosa de arena");go.layer=2;go.transform.position=packet.position;
   var shot=go.AddComponent<SlugProjectile>();shot.speed=inventory.projectileSpeed;shot.transformDistance=inventory.transformationDistance;
   shot.arenaShot=true;shot.arenaAuthority=authority;shot.arenaOwner=packet.id;shot.Launch(definition,packet.direction);shot.SetGolden(packet.golden);
   if(packet.targeted&&peers.TryGetValue(packet.other,out var target)&&target.proxy)shot.HomingTarget=target.proxy.transform;
   float age=Mathf.Clamp((float)network.ServerTime.TimeAsFloat-packet.eventTime,0,2.5f);
   if(age>.01f)shot.Advance(age);
  }
  public void SubmitShot(SlugDefinition definition,Vector3 origin,Vector3 direction,SlugProjectile localShot){
   localShot.arenaShot=true;localShot.arenaAuthority=false;localShot.arenaOwner=network.LocalClientId;
   predictedHostShot=localShot;
   var target=localShot.HomingTarget?localShot.HomingTarget.GetComponentInParent<ArenaTarget>():null;
   SendToServer(new Packet{kind="shot",species=definition.id,position=origin,direction=direction,eventTime=(float)network.ServerTime.TimeAsFloat,targeted=target,other=target?target.clientId:0,golden=localShot.IsGolden},NetworkDelivery.Reliable);
   predictedHostShot=null;
  }
  public void Hit(ulong id,SlugDefinition definition,bool transformed,Vector3 direction,bool golden=false){
   if(!IsServer||!peers.TryGetValue(id,out var peer)||peer.health<=0||peer.evading)return;
   var combat=definition.Combat.WithDamageMultiplier(golden?1.1f:1f);peer.health=Mathf.Max(0,peer.health-(transformed?combat.impactDamage:(definition.id.StartsWith("Malvada_")?3.15f:3)*(golden?1.1f:1f)));
   if(transformed){peer.burnUntil=Time.unscaledTime+combat.burnDuration;peer.burnRate=combat.burnDamagePerSecond;}
   if(peer.health<=0)peer.deadUntil=Time.unscaledTime+3;
   Broadcast(new Packet{kind="hit",id=id,species=definition.id,transformed=transformed,golden=golden,health=peer.health,direction=direction});
  }
  void Update(){
   if(!registered||!network||!network.IsListening)return;
   var keyboard=UnityEngine.InputSystem.Keyboard.current;
   if(!GameSession.Current&&(MobileTouchControls.PausePressed||keyboard!=null&&keyboard.escapeKey.wasPressedThisFrame))LocalPaused=!LocalPaused;
   SmoothRemotePlayers();
   if(!peers.ContainsKey(network.LocalClientId)&&Time.unscaledTime>=helloAt){helloAt=Time.unscaledTime+1;SendToServer(new Packet{kind="hello",name=PlayerName,collection=inventory.owned.Distinct().ToArray(),goldenCollection=GoldenSpecies()});}
   if(Time.unscaledTime>=sendAt){
    sendAt=Time.unscaledTime+1f/50f;
    var a=(player.female.activeSelf?player.female:player.male).GetComponentInChildren<Animator>();var baseState=a.GetCurrentAnimatorStateInfo(0);var upper=a.layerCount>1?a.GetCurrentAnimatorStateInfo(1):default;
    SendToServer(new Packet{kind="pose",eventTime=(float)network.ServerTime.TimeAsFloat,evading=player.GetComponent<PlayerAbilities>()&&player.GetComponent<PlayerAbilities>().Evading,position=player.transform.position,rotation=player.visuals.rotation,female=player.female.activeSelf,state0=baseState.fullPathHash,time0=baseState.normalizedTime,state1=upper.fullPathHash,time1=upper.normalizedTime,upper=a.layerCount>1?a.GetLayerWeight(1):0},NetworkDelivery.UnreliableSequenced);
   }
   if(IsServer)foreach(var pair in peers.ToArray()){
    var peer=pair.Value;
    if(peer.health>0&&Time.unscaledTime<peer.burnUntil){peer.health=Mathf.Max(0,peer.health-peer.burnRate*Time.unscaledDeltaTime);if(peer.health==0){peer.deadUntil=Time.unscaledTime+3;Broadcast(new Packet{kind="hit",id=pair.Key,health=0});}}
    if(peer.health<=0&&Time.unscaledTime>=peer.deadUntil){peer.health=100;peer.burnUntil=0;peer.position=Spawn(pair.Key);Broadcast(new Packet{kind="respawn",id=pair.Key,position=peer.position,health=100});}
   }
  }
  string[] GoldenSpecies(){var result=new List<string>();for(int i=0;i<Mathf.Min(inventory.owned.Count,inventory.goldenOwned.Count);i++)if(inventory.goldenOwned[i])result.Add(inventory.owned[i]);return result.Distinct().ToArray();}
  static GUIStyle tagStyle;float copiedUntil;
  // Names over the other players' heads, each in its own colour (screen space, behind-camera ones skipped).
  void DrawNameTags()
  {
   if(Event.current.type!=EventType.Repaint)return;var cam=Camera.main;if(!cam)return;
   if(tagStyle==null)tagStyle=new GUIStyle(GUI.skin.label){alignment=TextAnchor.MiddleCenter,fontStyle=FontStyle.Bold,wordWrap=false};
   tagStyle.fontSize=Mathf.Clamp(Mathf.RoundToInt(15*Screen.height/720f),13,18);
   foreach(var pair in peers)
   {
    if(pair.Key==network.LocalClientId||!pair.Value.proxy)continue;
    var head=pair.Value.proxy.transform.position+Vector3.up*2.35f;var p=cam.WorldToScreenPoint(head);if(p.z<=0||p.z>70)continue;
    var color=TagColors[(int)(pair.Key%(ulong)TagColors.Length)];string text=string.IsNullOrEmpty(pair.Value.name)?"Jugador "+pair.Key:pair.Value.name;
    var size=tagStyle.CalcSize(new GUIContent(text));var r=new Rect(p.x-size.x/2-8,Screen.height-p.y-size.y/2,size.x+16,size.y+2);
    NexoHud.Fill(r,new Color(0,0,0,.45f));NexoHud.Fill(new Rect(r.x,r.yMax-2,r.width,2),color);
    var old=tagStyle.normal.textColor;tagStyle.normal.textColor=new Color(0,0,0,.8f);GUI.Label(new Rect(r.x+1,r.y+1,r.width,r.height),text,tagStyle);
    tagStyle.normal.textColor=color;GUI.Label(r,text,tagStyle);tagStyle.normal.textColor=old;
   }
  }
  void OnGUI(){
   // Room address (to share) under the life bar; leaving is in the Esc menu.
   if(GameSession.Current&&GameSession.Current.menu||!network||!MultiplayerRoom.Current)return;
   if(LocalPaused){DrawPauseMenu();return;}
   DrawNameTags();
   var before=GUI.matrix;NexoHud.Scale();
   string code=MultiplayerRoom.Current.Code;
   NexoHud.Shadowed(new Rect(122,92,520,18),"SALA "+code+"  ·  "+peers.Count+"/4 jugadores",12,NexoUi.Teal);
   bool copyNow=UnityEngine.InputSystem.Keyboard.current!=null&&UnityEngine.InputSystem.Keyboard.current.f8Key.wasPressedThisFrame;
   if(Cursor.visible&&NexoUi.Button(new Rect(122,108,150,20),"COPIAR CÓDIGO"))copyNow=true;
   if(copyNow){GUIUtility.systemCopyBuffer=code;copiedUntil=Time.unscaledTime+2;}
   NexoHud.Shadowed(new Rect(Cursor.visible?280:122,109,300,18),Time.unscaledTime<copiedUntil?"✔ Código copiado":"F8 · copiar código",11,Time.unscaledTime<copiedUntil?new Color(.5f,1,.6f):NexoUi.Muted);
   GUI.matrix=before;
  }
  void DrawPauseMenu(){
   var before=GUI.matrix;NexoHud.Fill(new Rect(0,0,Screen.width,Screen.height),new Color(.005f,.02f,.03f,.78f));NexoHud.Scale();
   var panel=new Rect(440,226,400,268);NexoUi.Panel(panel);NexoUi.Title(new Rect(panel.x+30,panel.y+22,panel.width-60,62),"PARTIDA EN PAUSA","La sala sigue activa para los demás jugadores.");
   if(NexoHud.PrimaryButton(new Rect(panel.x+40,panel.y+112,panel.width-80,54),"REANUDAR")){LocalPaused=false;NexoUi.ResetFocus();}
   if(NexoHud.Button(new Rect(panel.x+40,panel.y+180,panel.width-80,50),"SALIR AL MENÚ PRINCIPAL")){LocalPaused=false;if(MultiplayerRoom.Current)MultiplayerRoom.Current.Leave();FrontEndMenu.Load(FrontEndMenu.MenuScene);}
   GUI.matrix=before;
  }
 }
}



