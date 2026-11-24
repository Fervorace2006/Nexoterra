using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

[InitializeOnLoad]
public static class PlayerActionsInstaller
{
    static PlayerActionsInstaller(){EditorApplication.update+=Poll;}
    static void Poll(){if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||!File.Exists("Library/PlayerActions.request"))return;File.Delete("Library/PlayerActions.request");try{Install();}catch(Exception e){File.WriteAllText("Library/PlayerActions.result",e.ToString());Debug.LogException(e);}}
    static void Install(){
        var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(AssetDatabase.GUIDToAssetPath("beb69273470f5b94386b4ffaa21c52a4"));
        if(!controller)throw new Exception("Missing player controller");
        string report="";
        foreach(string name in new[]{"Baile1","VolteretaAdelante"}){
            string path=Directory.GetFiles("Assets/Characters/PlayerActions/"+name,"*.fbx",SearchOption.AllDirectories).Single().Replace((char)92,'/');
            var importer=(ModelImporter)AssetImporter.GetAtPath(path);
            importer.animationType=ModelImporterAnimationType.Human;importer.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;importer.importAnimation=true;importer.importCameras=false;importer.importLights=false;importer.materialImportMode=ModelImporterMaterialImportMode.None;
            var settings=importer.clipAnimations.Length>0?importer.clipAnimations:importer.defaultClipAnimations;
            foreach(var clip in settings){clip.loopTime=name=="Baile1";clip.loopPose=clip.loopTime;clip.lockRootRotation=true;clip.keepOriginalOrientation=false;clip.lockRootPositionXZ=true;clip.keepOriginalPositionXZ=false;clip.lockRootHeightY=true;clip.heightFromFeet=true;clip.keepOriginalPositionY=false;}
            importer.clipAnimations=settings;importer.SaveAndReimport();
            var avatar=AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();
            if(!avatar||!avatar.isValid||!avatar.isHuman)throw new Exception("Avatar humanoide inválido: "+name);
            var animation=AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__preview__")).OrderByDescending(c=>c.length).FirstOrDefault();
            if(!animation||animation.length<=.1f||!animation.humanMotion)throw new Exception("El FBX no contiene una animación humanoide utilizable: "+name);
            string stateName=name=="Baile1"?"Dance1":"RollForward";
            var machine=controller.layers[0].stateMachine;var state=machine.states.Select(c=>c.state).FirstOrDefault(s=>s.name==stateName)??machine.AddState(stateName);
            state.motion=animation;state.speed=name=="Baile1"?1:animation.length;state.writeDefaultValues=true;
            EditorUtility.SetDirty(state);report+="PASS "+name+" humanoid, "+animation.length+"s, root motion baked\n";
        }
        EditorUtility.SetDirty(controller);AssetDatabase.SaveAssets();File.WriteAllText("Library/PlayerActions.result",report);
    }
}
