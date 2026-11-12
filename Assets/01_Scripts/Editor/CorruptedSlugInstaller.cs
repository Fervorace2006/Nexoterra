using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Nexoterra.World;
using System.IO;
using System.Linq;
using System.Collections.Generic;
[InitializeOnLoad] static class CorruptedSlugInstaller {
 static CorruptedSlugInstaller(){EditorApplication.update+=Poll;}
 static void Poll(){if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||!File.Exists("Library/CorruptedSlugs.request"))return;try{File.Delete("Library/CorruptedSlugs.request");Build();File.WriteAllText("Library/CorruptedSlugs.result","PASS");}catch(System.Exception e){File.WriteAllText("Library/CorruptedSlugs.result",e.ToString());}}
 static GameObject Model(string folder,string id){
 string dir="Assets/Characters/Malvadas/"+folder;string file=Directory.GetFiles(dir,"*.fbx").First();var asset=AssetDatabase.LoadAssetAtPath<GameObject>(file);if(!asset)throw new System.Exception("FBX pendiente: "+file);
 var go=Object.Instantiate(asset);go.name=id;
 var matPath=dir+"/URP.mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(matPath);if(!mat){mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(mat,matPath);}var texPath=Directory.GetFiles(dir,"*",SearchOption.AllDirectories).First(p=>p.ToLower().Contains("basecolor")&&!p.EndsWith(".meta"));mat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(texPath.Replace('\\','/')));mat.SetFloat("_Smoothness",.25f);
 foreach(var r in go.GetComponentsInChildren<Renderer>())r.sharedMaterials=Enumerable.Repeat(mat,r.sharedMaterials.Length).ToArray();
 foreach(var c in go.GetComponentsInChildren<Collider>())Object.DestroyImmediate(c);
 var result=PrefabUtility.SaveAsPrefabAsset(go,"Assets/Resources/CorruptedSlugs/"+id+".prefab");Object.DestroyImmediate(go);return result;
 }
 static void Build(){
 if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().path!="Assets/00_Scenes/Nexoterra_CuatroNodos.unity")throw new System.Exception("Abre Nexoterra_CuatroNodos antes de instalar");
 Directory.CreateDirectory("Assets/Resources/CorruptedSlugs");AssetDatabase.Refresh();var cat=AssetDatabase.LoadAssetAtPath<SlugCatalog>("Assets/NexoterraWorld/Slugs/Catalog.asset");var defs=cat.species.Where(d=>!d.id.StartsWith("Malvada_")).ToList();
 string[] folders={"Arana","Carnero","Electroshock","ELEMENTAL_AGUA","ELEMENTAL_AIRE","ELEMENTAL_FUEGO","ELEMENTAL_TIERRA","Hielo","Infierno","Tornado","Torpedo"};string[] ids={"Arana","Carnero","tazerling","Elemental_Agua","Elemental_Aire","Elemental_Fuego","Elemental_Tierra","ice","Infierno","Tornado","Armashelt"};
 for(int i=0;i<ids.Length;i++){var previous=cat.Find("Malvada_"+ids[i]);var original=cat.Find(ids[i]);if(original==null)throw new System.Exception("Especie ausente: "+ids[i]);if(ids[i]=="Infierno")original.unique=true;var combat=SlugCombatProfile.Default(ids[i]);combat.impactDamage*=1.15f;if(!original.unique){combat.slowDuration=Mathf.Max(combat.slowDuration,2);combat.slowMultiplier=Mathf.Min(combat.slowMultiplier,.8f);}defs.Add(new SlugDefinition{id="Malvada_"+ids[i],displayName=original.displayName+" malvada",normal=Model(folders[i]+"_Malvada","Malvada_"+ids[i]),transformed=Model(folders[i]+"_Malvada_T","Malvada_"+ids[i]+"_T"),portrait=previous!=null?previous.portrait:null,unique=original.unique,accent=new Color(.3f,1,.5f),combat=combat,combatConfigured=true});}
 cat.species=defs.ToArray();EditorUtility.SetDirty(cat);
 var player=Object.FindAnyObjectByType<NexoterraThirdPerson>();if(player&&!player.GetComponent<CorruptionCompanion>())player.gameObject.AddComponent<CorruptionCompanion>();
 foreach(var spawn in Object.FindObjectsByType<SlugSpawnPoint>(FindObjectsSortMode.None)){if(SlugCatalog.BaseSpecies(spawn.species)=="Infierno"){spawn.gameObject.SetActive(false);continue;}if(spawn.biome=="Cienaga carmesi"&&!spawn.unique&&cat.Find("Malvada_"+SlugCatalog.BaseSpecies(spawn.species))!=null)spawn.species="Malvada_"+SlugCatalog.BaseSpecies(spawn.species);}
 EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());EditorSceneManager.SaveOpenScenes();AssetDatabase.SaveAssets();
 }
}
