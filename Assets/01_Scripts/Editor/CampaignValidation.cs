using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Nexoterra.World;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Collections;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;

[InitializeOnLoad]
public static class CampaignValidation
{
 static NetworkManager probe;static bool joined,hitReceived,hitSent,shotSent;static double probeAt;
 static int stage;static double deadline;static WildSlug reward;
 static string report="Library/CampaignValidation.result";
 static CampaignValidation(){EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredEditMode&&SessionState.GetBool("CampaignValidation",false)){SessionState.SetBool("CampaignValidation",false);EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>(SessionState.GetString("CampaignValidation.Start",""));}};}
 static void Check(bool value,string message){if(!value)throw new Exception(message);File.AppendAllText(report,"PASS "+message+"\n");}
 static void Tick(){try{
  if(!EditorApplication.isPlayingOrWillChangePlaymode&&!EditorApplication.isCompiling&&!EditorApplication.isUpdating&&File.Exists("Library/CampaignValidation.request")){
   File.Delete("Library/CampaignValidation.request");File.WriteAllText(report,"");stage=0;
   SessionState.SetString("CampaignValidation.Start",AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));SessionState.SetBool("CampaignValidation",true);
   EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/00_Scenes/Campaign/Nexoterra_Jardines.unity");EditorApplication.isPlaying=true;deadline=EditorApplication.timeSinceStartup+180;return;
  }
  if(!SessionState.GetBool("CampaignValidation",false)||!EditorApplication.isPlaying)return;
  if(deadline==0)deadline=EditorApplication.timeSinceStartup+180;
  if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Tiempo agotado, etapa "+stage+"; "+(MultiplayerRoom.Current?MultiplayerRoom.Current.Status:""));
  var c=CampaignLevel.Current;
  if(stage==0&&c&&GameSession.Current&&GameSession.Current.Ready&&Time.timeSinceLevelLoad>2){
   Directory.CreateDirectory("Documentation/Nexoterra/Campaign");ScreenCapture.CaptureScreenshot("Documentation/Nexoterra/Campaign/Level"+c.index+".png");
   var p=GameSession.Current.GetComponent<NexoterraThirdPerson>();p.enabled=false;
   var ground=UnityEngine.Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None).First(m=>m.name=="Suelo del bioma");Check(Mathf.Abs(ground.sharedMesh.bounds.size.x-250)<.01f&&Mathf.Abs(ground.sharedMesh.bounds.size.z-250)<.01f,"dimensiones 250x250 "+c.index);
   Check(c.enemies.Length==(c.index>=4?9:8),"enemigos nivel "+c.index);Check(Vector3.Distance(c.entrance.position,c.rewardPoint.position)>200,"guardián al otro extremo "+c.index);
   Check(!c.Cleared,"salida bloqueada "+c.index);
   var health=p.GetComponent<PlayerHealth>();health.Restore(100);health.CorruptionCost();Check(Mathf.Approximately(health.Health,99),"coste malvada 1% "+c.index);health.Restore(1);health.CorruptionCost();Check(Mathf.Approximately(health.Health,1),"coste no letal "+c.index);health.Restore(100);
   var evil=c.catalog.Find("Malvada_Infierno");var profile=evil.combatConfigured?evil.combat:SlugCombatProfile.Default(evil.id);Check(Mathf.Approximately(evil.Combat.impactDamage,profile.impactDamage*1.05f),"bono malvada 5% "+c.index);
   foreach(var e in c.enemies)e.ReceiveDamage(10000);
   Check(c.Remaining==0,"enemigos derrotados "+c.index);
   string id=CampaignLevel.Rewards[c.index];
   if(id!=""){
    reward=WildSlug.Active.FirstOrDefault(w=>w&&c.IsReward(w));Check(reward&&reward.species=="Malvada_"+id,"recompensa malvada "+c.index);Check(!c.Cleared,"curación obligatoria "+c.index);
    typeof(SlugProjectile).GetMethod("PurifyWild",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{reward,p.GetComponent<SlugInventory>()});stage=1;
   }else stage=2;
  }
  if(stage==1&&reward&&!reward.GetComponent<SlugPurification>()){
   Check(reward.species==CampaignLevel.Rewards[c.index],"purificación "+c.index);GameSession.Current.GetComponent<SlugInventory>().Add(reward.species);reward.Adopt();stage=2;
  }
  if(stage==2){
   Check(c.Cleared,"túnel desbloqueado "+c.index);var snapshot=GameSession.Current.Capture();Check(snapshot.defeated.Length==c.enemies.Length,"estado guardable "+c.index);
   if(c.index<CampaignLevel.Scenes.Length-1){GameSession.Current.Travel(CampaignLevel.Scenes[c.index+1]);stage=3;}else{MultiplayerRoom.Open().Host();stage=4;}deadline=EditorApplication.timeSinceStartup+120;
  }
  if(stage==3&&Time.timeSinceLevelLoad<1)stage=0;
  if(stage==4&&ArenaSession.Current&&MultiplayerRoom.Current.Connected){Check(!string.IsNullOrEmpty(MultiplayerRoom.Current.Code),"Relay: sala de Internet creada con código");stage=5;JoinProbe();}
  if(stage==5&&probe&&probe.IsConnectedClient&&EditorApplication.timeSinceStartup>probeAt){
   probeAt=EditorApplication.timeSinceStartup+.5;
   if(!joined)SendProbe(new ArenaSession.Packet{kind="hello",collection=new[]{"Infierno"}});
      else if(!shotSent){shotSent=true;Check(true,"segundo cliente unido mediante Relay");SendProbe(new ArenaSession.Packet{kind="shot",species="Infierno",position=new Vector3(85,1.3f,85),direction=Vector3.forward});}
   else if(!hitSent){
    Check(UnityEngine.Object.FindObjectsByType<SlugProjectile>(FindObjectsSortMode.None).Any(p=>p.arenaShot&&p.arenaOwner==probe.LocalClientId),"disparo del segundo cliente creado en anfitrión");
    hitSent=true;var target=UnityEngine.Object.FindObjectsByType<ArenaTarget>(FindObjectsSortMode.None).First(t=>t.clientId==probe.LocalClientId);
    TestProjectile(target.transform.position+Vector3.up+Vector3.back*3,0);
    TestProjectile(GameSession.Current.transform.position+Vector3.up+Vector3.back*3,probe.LocalClientId);
   }
   if(hitReceived&&GameSession.Current.GetComponent<PlayerHealth>().Health<100){Check(true,"proyectil impacta al anfitrión y al cliente");Check(true,"daño autorizado por anfitrión recibido por segundo cliente");probe.Shutdown();UnityEngine.Object.Destroy(probe.gameObject);File.AppendAllText(report,"COMPLETE\n");EditorApplication.isPlaying=false;}
  }
  if(stage==4&&MultiplayerRoom.Current&&!MultiplayerRoom.Current.Busy&&!MultiplayerRoom.Current.Connected)throw new Exception(MultiplayerRoom.Current.Status);
 }catch(Exception e){File.AppendAllText(report,"FAIL "+e+"\n");EditorApplication.isPlaying=false;}}
 static async void JoinProbe(){try{
  await System.Threading.Tasks.Task.Delay(1800);
  var allocation=await RelayService.Instance.JoinAllocationAsync(MultiplayerRoom.Current.Code);
  var go=new GameObject("Segundo cliente de prueba");UnityEngine.Object.DontDestroyOnLoad(go);
  probe=go.AddComponent<NetworkManager>();probe.NetworkConfig=new NetworkConfig{EnableSceneManagement=false,ForceSamePrefabs=false,ConnectionApproval=true,ProtocolVersion=1};
  var transport=go.AddComponent<UnityTransport>();probe.NetworkConfig.NetworkTransport=transport;transport.SetRelayServerData(AllocationUtils.ToRelayServerData(allocation,"dtls"));
  if(!probe.StartClient())throw new Exception("No arrancó segundo cliente");
  probe.CustomMessagingManager.RegisterNamedMessageHandler("arena",(sender,reader)=>{reader.ReadValueSafe(out string json);var packet=JsonUtility.FromJson<ArenaSession.Packet>(json);if(packet.id==probe.LocalClientId&&packet.kind=="join")joined=true;if(packet.id==probe.LocalClientId&&packet.kind=="hit"&&packet.health<100)hitReceived=true;});
 }catch(Exception e){File.AppendAllText(report,"FAIL Relay probe "+e+"\n");EditorApplication.isPlaying=false;}}
 static void TestProjectile(Vector3 position,ulong owner){var go=new GameObject("Proyectil de validación");go.transform.position=position;var shot=go.AddComponent<SlugProjectile>();shot.arenaShot=shot.arenaAuthority=true;shot.arenaOwner=owner;shot.Launch(GameSession.Current.GetComponent<SlugInventory>().catalog.Find("Infierno"),Vector3.forward);}
 static void SendProbe(ArenaSession.Packet packet){using(var writer=new FastBufferWriter(4096,Allocator.Temp)){writer.WriteValueSafe(JsonUtility.ToJson(packet));probe.CustomMessagingManager.SendNamedMessage("arena",NetworkManager.ServerClientId,writer);}}
}






