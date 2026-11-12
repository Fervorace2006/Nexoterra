using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Nexoterra.World;

[InitializeOnLoad]
public static class UpgradeRuntimeReview
{
 static double statusAt;
 static UpgradeRuntimeReview(){EditorApplication.update+=Tick;}
 static void Tick(){
  if(File.Exists("Library/UpgradeReview.status.request")&&EditorApplication.timeSinceStartup>statusAt){statusAt=EditorApplication.timeSinceStartup+1;File.WriteAllText("Library/UpgradeReview.status","play="+EditorApplication.isPlaying+" paused="+EditorApplication.isPaused+" pending="+SessionState.GetBool("UpgradeReview",false)+" session="+(bool)GameSession.Current+" ready="+(GameSession.Current&&GameSession.Current.Ready)+" runner="+(bool)UnityEngine.Object.FindAnyObjectByType<UpgradeReviewRunner>());}
  if(!EditorApplication.isPlayingOrWillChangePlaymode&&!EditorApplication.isCompiling&&!EditorApplication.isUpdating&&File.Exists("Library/UpgradeReview.request")){
   File.Delete("Library/UpgradeReview.request");SessionState.SetBool("UpgradeReview",true);SessionState.SetString("UpgradeReview.Start",AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/00_Scenes/Campaign/Nexoterra_Jardines.unity");EditorApplication.isPaused=false;EditorApplication.isPlaying=true;
  }
  if(EditorApplication.isPlaying&&SessionState.GetBool("UpgradeReview",false)){Application.runInBackground=true;EditorApplication.isPaused=false;}
  if(EditorApplication.isPlaying&&SessionState.GetBool("UpgradeReview",false)&&GameSession.Current&&GameSession.Current.Ready){SessionState.SetBool("UpgradeReview",false);new GameObject("Revisión temporal").AddComponent<UpgradeReviewRunner>();}
 }
}
