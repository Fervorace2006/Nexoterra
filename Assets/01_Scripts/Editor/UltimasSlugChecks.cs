using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Nexoterra.World;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class UltimasSlugChecks
{
    const string ScenePath="Assets/__UltimasValidation.unity";
    static UltimasSlugChecks(){EditorApplication.update+=Poll;EditorApplication.playModeStateChanged+=State;}
    static void Poll(){
        if(EditorApplication.isPlaying&&SessionState.GetBool("UltimasChecks.Active",false)&&!EditorApplication.isCompiling){
            EditorApplication.isPaused=false;
            if(!Object.FindAnyObjectByType<UltimasSlugTestRunner>()){File.AppendAllText("Library/UltimasChecks.result","CREATE RUNNER\n");new GameObject("Focused slug checks").AddComponent<UltimasSlugTestRunner>();}
            return;
        }
        if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||!File.Exists("Library/UltimasChecks.request"))return;
        File.Delete("Library/UltimasChecks.request");
        if(Enumerable.Range(0,UnityEngine.SceneManagement.SceneManager.sceneCount).Any(i=>UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)){File.WriteAllText("Library/UltimasChecks.result","FAIL: unsaved scene");return;}
        SessionState.SetString("UltimasChecks.Previous",UnityEngine.SceneManagement.SceneManager.GetActiveScene().path);
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);new GameObject("Focused slug checks").AddComponent<UltimasSlugTestRunner>();EditorSceneManager.SaveScene(scene,ScenePath);
        SessionState.SetString("UltimasChecks.Start",AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);EditorApplication.isPaused=false;
        SessionState.SetBool("UltimasChecks.Active",true);File.WriteAllText("Library/UltimasChecks.result","RUNNING\n");EditorApplication.isPlaying=true;
    }
    static void State(PlayModeStateChange state){
        if(!SessionState.GetBool("UltimasChecks.Active",false))return;
        if(state==PlayModeStateChange.EnteredPlayMode){EditorApplication.isPaused=false;Application.runInBackground=true;}
        if(state==PlayModeStateChange.EnteredEditMode){SessionState.SetBool("UltimasChecks.Active",false);EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>(SessionState.GetString("UltimasChecks.Start","Assets/00_Scenes/Nexoterra_Menu.unity"));string previous=SessionState.GetString("UltimasChecks.Previous","");if(!string.IsNullOrEmpty(previous))EditorSceneManager.OpenScene(previous);AssetDatabase.DeleteAsset(ScenePath);}
    }
}
