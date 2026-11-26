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
   public bool female,transformed,evading;public float health,time0,time1,upper,eventTime;public int state0,state1; public string[] collection;
  }
  sealed class Peer {
   public Vector3 velocity,lastPosePosition;public float lastPoseTime=-1,viewTime;   // for latency-compensated prediction
   public string[] collection;public GameObject proxy;public Vector3 position;public Quaternion rotation;
   public float health=100,lastShot=-10,deadUntil,burnUntil,burnRate,lastMove;
   public bool female,visualFemale,evading;public Animator animator;
   // Remote view smoothing: last received pose, estimated velocity and animation states.
   public Vector3 viewTarget;public Quaternion viewRotation=Quaternion.identity;public float lastView=-1;public int shown0,shown1;
   public readonly Dictionary<string,float> busyUntil=new Dictionary<string,float>();
  }
  public static ArenaSession Current {get;private set;}
  readonly Dictionary<ulong,Peer> peers=new Dictionary<ulong,Peer>();
  NetworkManager network;
  NexoterraThirdPerson player;SlugInventory inventory;PlayerHealth health;
  float sendAt,helloAt;bool registered;
  SlugProjectile predictedHostShot;
  public bool IsServer=>network&&network.IsServer;
  void Awake(){Current=this;}
  System.Collections.IEnumerator Start(){
   player=FindAnyObjectByType<NexoterraThirdPerson>();inventory=player.GetComponent<SlugInventory>();health=player.GetComponent<PlayerHealth>();
   yield return null;
   var room=MultiplayerRoom.Current;
   if(!room||!room.Connected){FrontEndMenu.Load(FrontEndMenu.MenuScene);yield break;}
   network=room.Manager;
   network.CustomMessagingManager.RegisterNamedMessageHandler("arena",Receive);registered=true;
   network.OnClientDisconnectCallback+=RemovePeer;
   var own=player.gameObject.AddComponent<ArenaTarget>();own.clientId=network.LocalClientId;
   GameSession.Data data=null;try{data=GameSession.ReadCollection();}catch(Exception e){Debug.LogWarning("Colección no disponible: "+e.Message);}
   if(data!=null){
    inventory.owned=data.owned.Where(id=>inventory.catalog.Find(id)!=null).ToList();
    inventory.equipped=new[]{-1,-1,-1,-1,-1,-1};for(int i=0;i<Mathf.Min(6,inventory.owned.Count);i++)inventory.equipped[i]=i;
   }else{inventory.owned.Clear();inventory.equipped=new[]{-1,-1,-1,-1,-1,-1};inventory.Add("Infierno");}
   inventory.persist=false;
   SendToServer(new Packet{kind="hello",collection=inventory.owned.Distinct().ToArray()});
   player.Teleport(Spawn(network.LocalClientId));health.Restore(health.maxHealth);
  }
  static Vector3 Spawn(ulong id){Vector3[] points={new Vector3(-85,.3f,-90),new Vector3(85,.3f,85),new Vector3(-85,.3f,85),new Vector3(85,.3f,-90)};return points[(int)(id%4)];}
  void OnDestroy(){
   if(registered&&network){network.CustomMessagingManager.UnregisterNamedMessageHandler("arena");network.OnClientDisconnectCallback-=RemovePeer;}
   foreach(var peer in peers.Values)if(peer.proxy)Destroy(peer.proxy);
   if(Current==this)Current=null;
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
    if(peers.ContainsKey(sender)){Send(sender,new Packet{kind="join",id=sender,position=peers[sender].position,health=peers[sender].health});return;}
    var owned=(packet.collection??Array.Empty<string>()).Where(id=>inventory.catalog.Find(id)!=null).Distinct().Take(64).ToArray();
    peers[sender]=new Peer{collection=owned,position=Spawn(sender),rotation=Quaternion.identity,lastMove=Time.unscaledTime};
    foreach(var pair in peers)Send(sender,new Packet{kind="join",id=pair.Key,position=pair.Value.position,rotation=pair.Value.rotation,health=pair.Value.health});
    Broadcast(new Packet{kind="join",id=sender,position=Spawn(sender),rotation=Quaternion.identity,health=100});return;
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
    if(peers.TryGetValue(packet.id,out var peer)){peer.health=packet.health;var effect=inventory.catalog.Find(packet.species);if(peer.proxy&&effect!=null&&packet.transformed){peer.proxy.GetComponent<EnemyControlEffects>().Apply(effect.id,packet.direction);if(effect.Combat.burnDuration>0){var fire=ImportedSlugVfx.BurningCharacter(peer.proxy.transform);if(fire)Destroy(fire,effect.Combat.burnDuration);}}}
    if(packet.id==network.LocalClientId){
     var def=inventory.catalog.Find(packet.species);if(def!=null)health.ReceiveSlug(def,packet.transformed,packet.direction);
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
   shot.arenaShot=true;shot.arenaAuthority=authority;shot.arenaOwner=packet.id;shot.Launch(definition,packet.direction);
   float age=Mathf.Clamp((float)network.ServerTime.TimeAsFloat-packet.eventTime,0,2.5f);
   if(age>.01f)shot.Advance(age);
  }
  public void SubmitShot(SlugDefinition definition,Vector3 origin,Vector3 direction,SlugProjectile localShot){
   localShot.arenaShot=true;localShot.arenaAuthority=false;localShot.arenaOwner=network.LocalClientId;
   predictedHostShot=localShot;
   SendToServer(new Packet{kind="shot",species=definition.id,position=origin,direction=direction,eventTime=(float)network.ServerTime.TimeAsFloat},NetworkDelivery.Reliable);
   predictedHostShot=null;
  }
  public void Hit(ulong id,SlugDefinition definition,bool transformed,Vector3 direction){
   if(!IsServer||!peers.TryGetValue(id,out var peer)||peer.health<=0||peer.evading)return;
   var combat=definition.Combat;peer.health=Mathf.Max(0,peer.health-(transformed?combat.impactDamage:definition.id.StartsWith("Malvada_")?3.15f:3));
   if(transformed){peer.burnUntil=Time.unscaledTime+combat.burnDuration;peer.burnRate=combat.burnDamagePerSecond;}
   if(peer.health<=0)peer.deadUntil=Time.unscaledTime+3;
   Broadcast(new Packet{kind="hit",id=id,species=definition.id,transformed=transformed,health=peer.health,direction=direction});
  }
  void Update(){
   if(!registered||!network||!network.IsListening)return;
   SmoothRemotePlayers();
   if(!peers.ContainsKey(network.LocalClientId)&&Time.unscaledTime>=helloAt){helloAt=Time.unscaledTime+1;SendToServer(new Packet{kind="hello",collection=inventory.owned.Distinct().ToArray()});}
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
  void OnGUI(){
   // Room address (to share) under the life bar; leaving is in the Esc menu.
   if(GameSession.Blocked||!network||!MultiplayerRoom.Current)return;var before=GUI.matrix;NexoHud.Scale();
   NexoHud.Shadowed(new Rect(122,92,520,18),"SALA "+MultiplayerRoom.Current.Code+"  ·  "+peers.Count+"/4 jugadores",12,NexoUi.Teal);
   GUI.matrix=before;
  }
 }
}



