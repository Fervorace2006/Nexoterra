using UnityEngine;
using UnityEditor;
using Nexoterra.World;
using System.Linq;
using System.IO;
[InitializeOnLoad] static class CorruptedSlugChecks {
 static CorruptedSlugChecks(){EditorApplication.update+=Run;}
 static void Require(bool condition,string message){if(!condition)throw new System.Exception(message);}
 static void Run(){if(EditorApplication.isCompiling||EditorApplication.isUpdating||!File.Exists("Library/CorruptedChecks.request"))return;File.Delete("Library/CorruptedChecks.request");GameObject go=null;try{
 var cat=AssetDatabase.LoadAssetAtPath<SlugCatalog>("Assets/NexoterraWorld/Slugs/Catalog.asset");var evil=cat.species.Where(d=>d.id.StartsWith("Malvada_")).ToArray();Require(evil.Length==11,"Expected 11 corrupted species");foreach(var d in evil){Require(d.normal&&d.transformed,"Missing models: "+d.id);Require(d.normal.GetComponentsInChildren<Renderer>().All(r=>r.sharedMaterials.All(m=>m&&m.mainTexture)),"Missing textures: "+d.id);Require(cat.Find(SlugCatalog.BaseSpecies(d.id))!=null,"Missing normal version");}
 go=new GameObject("Temporary inventory checks");go.SetActive(false);var inv=go.AddComponent<SlugInventory>();inv.persist=false;inv.catalog=cat;Require(inv.Add("Infierno"),"Initial Infierno failed");Require(!inv.Add("Malvada_Infierno"),"Duplicate unique allowed");inv.owned[0]="Malvada_Infierno";Require(inv.HasSpecies("Infierno"),"Corrupted ownership not recognized");Require(!inv.Add("Infierno"),"Normal duplicate allowed");Require(cat.Find("Malvada_Gon_Doc")==null,"Doc must remain normal");Require(cat.Find("Malvada_Armashelt")!=null,"Torpedo mapping missing");Require(!Object.FindObjectsByType<SlugSpawnPoint>(FindObjectsSortMode.None).Any(s=>s.isActiveAndEnabled&&SlugCatalog.BaseSpecies(s.species)=="Infierno"),"Infierno still spawns");Require(Object.FindObjectsByType<SlugSpawnPoint>(FindObjectsSortMode.None).Where(s=>s.species.StartsWith("Malvada_")).All(s=>s.biome=="Cienaga carmesi"&&!s.unique),"Invalid corrupted habitat");File.WriteAllText("Library/CorruptedChecks.result","PASS: 11 model pairs with textures; unique ownership across variants; Torpedo mapping; Doc normal; Infierno spawn disabled; corrupted habitats restricted.");
 }catch(System.Exception e){File.WriteAllText("Library/CorruptedChecks.result",e.ToString());}finally{if(go)Object.DestroyImmediate(go);}}
}
