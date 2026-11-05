using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Nexoterra.World;
namespace Nexoterra.Editor
{
 [InitializeOnLoad] public static class NexoterraUiPreview
 {
  static NexoterraUiPreview(){EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=state=>{if(state==PlayModeStateChange.EnteredEditMode&&SessionState.GetBool("UI.PreviewActive",false)){EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>(SessionState.GetString("UI.StartScene","Assets/00_Scenes/Nexoterra_Menu.unity"));SessionState.SetBool("UI.PreviewActive",false);}};}
  static void Tick()
  {
   if(EditorApplication.isCompiling||EditorApplication.isUpdating)return;
   if(File.Exists("Library/UiPreview.request")&&!EditorApplication.isPlayingOrWillChangePlaymode){File.Delete("Library/UiPreview.request");SessionState.SetString("UI.StartScene",AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));SessionState.SetBool("UI.PreviewActive",true);SessionState.SetBool("UI.Seeded",false);EditorSceneManager.playModeStartScene=null;EditorSceneManager.OpenScene(NexoterraBiomeBuilder.ScenePath);EditorApplication.isPlaying=true;}
   if(!SessionState.GetBool("UI.PreviewActive",false)||SessionState.GetBool("UI.Seeded",false)||!EditorApplication.isPlaying||!GameSession.Current||!GameSession.Current.Ready||Time.time<2)return;
   var inv=GameSession.Current.GetComponent<SlugInventory>();foreach(var d in inv.catalog.species)inv.Add(d.id);inv.selected=0;inv.SetOpen(true);SessionState.SetBool("UI.Seeded",true);
  }
 }
}
