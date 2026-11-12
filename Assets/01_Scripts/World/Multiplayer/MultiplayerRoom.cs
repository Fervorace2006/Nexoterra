using System;
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
  public string Status {get;private set;}="Crea una sala o introduce un código.";
  public bool Busy {get;private set;}
  bool quitting;
  void OnApplicationQuit(){quitting=true;}
  public bool Connected=>Manager&&Manager.IsListening;
  public static MultiplayerRoom Open(){
   if(Current)return Current;return new GameObject("Sala multijugador").AddComponent<MultiplayerRoom>();
  }
  void Awake(){Current=this;DontDestroyOnLoad(gameObject);}
  async Task Prepare(){
   if(UnityServices.State!=ServicesInitializationState.Initialized)await UnityServices.InitializeAsync();
   if(!AuthenticationService.Instance.IsSignedIn)await AuthenticationService.Instance.SignInAnonymouslyAsync();
   Manager=gameObject.AddComponent<NetworkManager>();Manager.NetworkConfig=new NetworkConfig();var transport=gameObject.AddComponent<UnityTransport>();
   Manager.NetworkConfig.NetworkTransport=transport;Manager.NetworkConfig.EnableSceneManagement=false;Manager.NetworkConfig.ForceSamePrefabs=false;
   Manager.NetworkConfig.ConnectionApproval=true;Manager.NetworkConfig.ProtocolVersion=1;
   Manager.ConnectionApprovalCallback=(request,response)=>{response.Approved=Manager.ConnectedClientsIds.Count<4;response.CreatePlayerObject=false;response.Pending=false;response.Reason="Sala completa (4 jugadores).";};
   Manager.OnClientConnectedCallback+=ConnectedClient;Manager.OnClientDisconnectCallback+=DisconnectedClient;
  }
  public async void Host(){
   if(Busy||Connected)return;Busy=true;Status="Conectando con Relay…";
   try{
    await Prepare();var allocation=await RelayService.Instance.CreateAllocationAsync(3);
    Manager.GetComponent<UnityTransport>().SetRelayServerData(AllocationUtils.ToRelayServerData(allocation,"dtls"));
    Code=await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
    if(!Manager.StartHost())throw new Exception("No se pudo iniciar la sala.");
    Status="Sala "+Code;
   }catch(Exception e){Debug.LogException(e);Status="No se pudo crear la sala. "+e.Message;CleanupNetwork();}
   finally{Busy=false;}
  }
  public async void Join(string code){
   if(Busy||Connected)return;Busy=true;Status="Uniéndose a la sala…";
   try{
    await Prepare();var allocation=await RelayService.Instance.JoinAllocationAsync(code.Trim().ToUpperInvariant());
    Manager.GetComponent<UnityTransport>().SetRelayServerData(AllocationUtils.ToRelayServerData(allocation,"dtls"));
    Code=code.Trim().ToUpperInvariant();
    if(!Manager.StartClient())throw new Exception("No se pudo conectar.");
   }catch(Exception e){Debug.LogException(e);Status="No se pudo entrar. "+e.Message;CleanupNetwork();}
   finally{Busy=false;}
  }
  void ConnectedClient(ulong id){
   if(id==Manager.LocalClientId){Status="Sala "+Code;FrontEndMenu.Load("Nexoterra_Arena");}
  }
  void DisconnectedClient(ulong id){
   if(quitting||!Application.isPlaying||!Manager||Manager.IsServer&&id!=Manager.LocalClientId)return;
   Status="La conexión se cerró.";CleanupNetwork();FrontEndMenu.Load(FrontEndMenu.MenuScene);
  }
  void CleanupNetwork(){
   if(!Manager)return;Manager.OnClientConnectedCallback-=ConnectedClient;Manager.OnClientDisconnectCallback-=DisconnectedClient;
   Manager.Shutdown();Destroy(Manager);var transport=GetComponent<UnityTransport>();if(transport)Destroy(transport);Manager=null;
  }
  public void Leave(){CleanupNetwork();Current=null;Destroy(gameObject);Time.timeScale=1;FrontEndMenu.Load(FrontEndMenu.MenuScene);}
  void OnDestroy(){if(Current==this)Current=null;}
 }
}


