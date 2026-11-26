using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

namespace Nexoterra.World
{
 public sealed class MultiplayerRoom : MonoBehaviour
 {
  public static MultiplayerRoom Current {get;private set;}
  public NetworkManager Manager {get;private set;}
  public string Code {get;private set;}
  public string Status {get;private set;}="Internet con código o conexión directa por IP.";
  public bool Busy {get;private set;}
  bool quitting,leaving;int operation;float connectDeadline;
  public bool Connected=>Manager&&Manager.IsListening;
  public static MultiplayerRoom Open()=>Current?Current:new GameObject("Sala multijugador").AddComponent<MultiplayerRoom>();
  void Awake(){Current=this;DontDestroyOnLoad(gameObject);}
  void OnApplicationQuit(){quitting=true;}
  void PrepareNetwork(){
   CleanupNetwork();
   var node=new GameObject("Conexión de red");DontDestroyOnLoad(node); // NGO forbids a nested NetworkManager.
   Manager=node.AddComponent<NetworkManager>();Manager.NetworkConfig=new NetworkConfig();
   var transport=node.AddComponent<UnityTransport>();
   Manager.NetworkConfig.NetworkTransport=transport;Manager.NetworkConfig.EnableSceneManagement=false;
   Manager.NetworkConfig.ForceSamePrefabs=false;Manager.NetworkConfig.ConnectionApproval=true;Manager.NetworkConfig.ProtocolVersion=1;Manager.NetworkConfig.TickRate=60;   // messages flush every tick: 60 Hz halves the send delay
   Manager.ConnectionApprovalCallback=(request,response)=>{response.Approved=Manager&&Manager.ConnectedClientsIds.Count<4;response.CreatePlayerObject=false;response.Pending=false;response.Reason=response.Approved?"":"Sala completa (4 jugadores).";};
   Manager.OnClientConnectedCallback+=ConnectedClient;Manager.OnClientDisconnectCallback+=DisconnectedClient;
  }
  async Task Authenticate(){
   if(UnityServices.State!=ServicesInitializationState.Initialized)await UnityServices.InitializeAsync();
   if(!AuthenticationService.Instance.IsSignedIn)await AuthenticationService.Instance.SignInAnonymouslyAsync();
  }
  bool Stale(int id)=>!this||leaving||id!=operation;
  public async void Host(){
   if(Busy||Connected)return;Busy=true;leaving=false;int id=++operation;Status="Creando sala de Internet...";
   try{
    await Authenticate();if(Stale(id))return;
    var allocation=await RelayService.Instance.CreateAllocationAsync(3);if(Stale(id))return;
    var code=await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);if(Stale(id))return;
    PrepareNetwork();Code=code;GUIUtility.systemCopyBuffer=code;   // ready to paste for the friend

    Manager.GetComponent<UnityTransport>().SetRelayServerData(AllocationUtils.ToRelayServerData(allocation,"dtls"));
    if(!Manager.StartHost())throw new Exception("No se pudo iniciar la sala.");
    Status="Código: "+Code;Busy=false;
   }catch(Exception e){if(!Stale(id)){Status="No se pudo crear la sala: "+e.Message;CleanupNetwork();Busy=false;}}
  }
  public async void Join(string code){
   if(Busy||Connected)return;
   if(string.IsNullOrWhiteSpace(code)){Status="Introduce el código de la sala.";return;}
   Busy=true;leaving=false;int id=++operation;Status="Conectando...";
   try{
    await Authenticate();if(Stale(id))return;
    var allocation=await RelayService.Instance.JoinAllocationAsync(code.Trim().ToUpperInvariant());if(Stale(id))return;
    PrepareNetwork();Code=code.Trim().ToUpperInvariant();
    Manager.GetComponent<UnityTransport>().SetRelayServerData(AllocationUtils.ToRelayServerData(allocation,"dtls"));
    if(!Manager.StartClient())throw new Exception("No se pudo conectar.");
    connectDeadline=Time.unscaledTime+20;
   }catch(Exception e){if(!Stale(id)){Status="No se pudo entrar: "+e.Message;CleanupNetwork();Busy=false;}}
  }
  int mappedPort;
  // Direct IP. Host: listens on all interfaces and asks the router (UPnP) to forward the UDP port,
  // then shows the public address to share. Client: accepts "ip", "ip:port" or a host name.
  public void Direct(bool host,string address,string portText){
   if(Busy||Connected)return;
   address=(address??"").Trim();
   int colon=address.LastIndexOf(':');if(!host&&colon>0&&address.IndexOf(':')==colon){portText=address.Substring(colon+1);address=address.Substring(0,colon);}
   if(!ushort.TryParse(portText,out var port)||port==0){Status="Puerto inválido (1 a 65535).";return;}
   string target=address;
   if(!host)
   {
    if(!IPAddress.TryParse(address,out var ip)||ip.AddressFamily!=System.Net.Sockets.AddressFamily.InterNetwork)
    {
     try{ip=Dns.GetHostAddresses(address).FirstOrDefault(a=>a.AddressFamily==System.Net.Sockets.AddressFamily.InterNetwork);}catch{ip=null;}
     if(ip==null){Status="No se encontró la dirección. Usa la IP pública del anfitrión (ej. 181.115.20.4).";return;}
    }
    target=ip.ToString();
   }
   try{
    leaving=false;int id=++operation;PrepareNetwork();Code=host?"Puerto "+port:target+":"+port;
    Manager.GetComponent<UnityTransport>().SetConnectionData(host?"127.0.0.1":target,port,host?"0.0.0.0":null);
    Busy=true;Status=host?"Abriendo servidor...":"Conectando a "+Code;
    if(!(host?Manager.StartHost():Manager.StartClient()))throw new Exception("No se pudo abrir la conexión. ¿El puerto "+port+" ya está en uso?");
    if(host){Busy=false;Share(port,id);}else connectDeadline=Time.unscaledTime+20;
   }catch(Exception e){Status=e.Message;CleanupNetwork();Busy=false;}
  }
  async void Share(int port,int id){
   string local=PortMapper.LocalIPv4();Code=local+":"+port;Status="Misma red: "+Code+" · abriendo el puerto en el router…";
   var (ok,publicIp,error)=await PortMapper.MapUdp(port);
   if(Stale(id))return;
   if(ok){mappedPort=port;Code=(publicIp??local)+":"+port;GUIUtility.systemCopyBuffer=Code;Status="Comparte esta dirección: "+Code+"  ·  misma red: "+local+":"+port;}
   else Status="Misma red: "+local+":"+port+". Para Internet abre el puerto UDP "+port+" en el router o usa INTERNET · CÓDIGO. ("+error+")";
  }
  void Update(){if(Busy&&Manager&&connectDeadline>0&&Time.unscaledTime>connectDeadline){Cancel();Status="Tiempo de conexión agotado. Revisa IP, puerto y disponibilidad del anfitrión.";}}
  void ConnectedClient(ulong id){
   if(leaving||!Manager||id!=Manager.LocalClientId)return;
   Busy=false;connectDeadline=0;Status="Sala "+Code;FrontEndMenu.Load("Nexoterra_Arena");
  }
  void DisconnectedClient(ulong id){
   if(quitting||leaving||!Application.isPlaying||!Manager||Manager.IsServer&&id!=Manager.LocalClientId)return;
   bool inArena=ArenaSession.Current;string reason=Manager.DisconnectReason;
   Status=string.IsNullOrEmpty(reason)?"La conexión se cerró.":reason;Busy=false;CleanupNetwork();
   if(inArena)FrontEndMenu.Load(FrontEndMenu.MenuScene);
  }
  void CleanupNetwork(){
   connectDeadline=0;if(mappedPort>0){_=PortMapper.Unmap(mappedPort);mappedPort=0;}if(!Manager)return;
   var old=Manager;Manager=null;
   old.OnClientConnectedCallback-=ConnectedClient;old.OnClientDisconnectCallback-=DisconnectedClient;
   old.Shutdown();Destroy(old.gameObject);
  }
  public void Cancel(){operation++;Busy=false;CleanupNetwork();Status="Conexión cancelada.";}
  public void Leave(){
   if(leaving)return;leaving=true;Cancel();Current=null;Time.timeScale=1;
   Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
   FrontEndMenu.Load(FrontEndMenu.MenuScene);Destroy(gameObject);
  }
  void OnDestroy(){operation++;if(!quitting)CleanupNetwork();if(Current==this)Current=null;}
 }
}
