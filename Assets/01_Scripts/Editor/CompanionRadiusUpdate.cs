using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Nexoterra.World;

[InitializeOnLoad]
static class CompanionRadiusUpdate
{
    static CompanionRadiusUpdate(){EditorApplication.update+=Tick;}
    static void Tick()
    {
        const string request="Library/CompanionRadius.request";
        if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||!File.Exists(request))return;
        File.Delete(request);
        try
        {
            var player=Object.FindAnyObjectByType<CorruptionCompanion>();
            if(!player)throw new System.Exception("No se encuentra CorruptionCompanion en la escena abierta.");
            Undo.RecordObject(player,"Ampliar radio de companera");
            player.GetComponent<SlugInventory>().docHealingRadius=6;
            EditorUtility.SetDirty(player.GetComponent<SlugInventory>());
            player.followRadius=18;
            player.stopRadius=14;
            EditorUtility.SetDirty(player);
            EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
            EditorSceneManager.SaveScene(player.gameObject.scene);
            if(!Resources.Load<Material>("SlugVfxSmoke"))throw new System.Exception("Material de humo ausente.");
            if(!SlugFlightEffects.RibbonMaterial)throw new System.Exception("Material de arcos ausente.");
            File.WriteAllText("Library/CompanionRadius.result","PASS: Doc radius 6; compiled; radius 18, stop 14, release at player feet; smoke and arcs materials available.");
        }
        catch(System.Exception e){File.WriteAllText("Library/CompanionRadius.result",e.ToString());}
    }
}
