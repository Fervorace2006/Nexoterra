using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;
using Nexoterra.World;
[InitializeOnLoad] public static class CavernColorsUpdate {
 static CavernColorsUpdate(){EditorApplication.update+=Tick;}
 static void Tick(){if(EditorApplication.isCompiling||EditorApplication.isUpdating||!File.Exists("Library/CavernColors.request"))return;if(EditorApplication.isPlayingOrWillChangePlaymode){EditorApplication.isPlaying=false;return;}File.Delete("Library/CavernColors.request");
 var roof=Make("Techo_cafe_claro",new Color(.78f,.59f,.40f));var rock=Make("Picos_gris_roca",new Color(.48f,.49f,.51f));
 foreach(var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None)){var prop=r.GetComponentInParent<WorldDestructible>();if(prop&&prop.kind==WorldDestructible.Kind.CeilingRock)r.sharedMaterial=rock;else if(r.name.StartsWith("Estalactitas_")||r.name.StartsWith("Techo_")||r.name=="Union_bovedas")r.sharedMaterial=roof;}
 EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());EditorSceneManager.SaveOpenScenes();AssetDatabase.SaveAssets();File.WriteAllText("Library/CavernColors.result","PASS");}
 static Material Make(string name,Color color){var path="Assets/NexoterraWorld/MapRepair/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(!m){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",.12f);m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",color*(name=="Techo_cafe_claro"?.22f:.08f));EditorUtility.SetDirty(m);return m;}
}
