using UnityEngine;
using UnityEditor;
using System.IO;
using Nexoterra.World;
[InitializeOnLoad] static class CorruptedPortraits {
 static CorruptedPortraits(){EditorApplication.update+=Tick;}
 static void Tick(){if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||!File.Exists("Library/CorruptedPortraits.request"))return;try{File.Delete("Library/CorruptedPortraits.request");var cat=AssetDatabase.LoadAssetAtPath<SlugCatalog>("Assets/NexoterraWorld/Slugs/Catalog.asset");int n=0;foreach(var d in cat.species)if(d.id.StartsWith("Malvada_")){d.portrait=Nexoterra.Editor.NexoterraUiPolish.Portrait(d.normal,d.id,false);n++;}EditorUtility.SetDirty(cat);AssetDatabase.SaveAssets();File.WriteAllText("Library/CorruptedPortraits.result","PASS "+n+" portraits");}catch(System.Exception e){File.WriteAllText("Library/CorruptedPortraits.result",e.ToString());}}
}
