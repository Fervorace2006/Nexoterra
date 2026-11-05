using System.IO;
using UnityEditor;
using UnityEngine;
namespace Nexoterra.Editor
{
 [InitializeOnLoad]
 public static class NexoterraVfxSetup
 {
  static NexoterraVfxSetup(){EditorApplication.delayCall+=Ensure;}
  public static void Ensure()
  {
   var shader=Shader.Find("Nexoterra/Slug Glow");if(!shader)return;
   const string folder="Assets/NexoterraWorld/Slugs/Resources";
   if(!AssetDatabase.IsValidFolder(folder)){AssetDatabase.CreateFolder("Assets/NexoterraWorld/Slugs","Resources");}
   foreach(bool ribbon in new[]{false,true})
   {
    string path=folder+(ribbon?"/SlugVfxTrail.mat":"/SlugVfxParticle.mat");
    if(AssetDatabase.LoadAssetAtPath<Material>(path))continue;
    var material=new Material(shader);material.SetFloat("_Ribbon",ribbon?1:0);AssetDatabase.CreateAsset(material,path);
   }
   if(!AssetDatabase.LoadAssetAtPath<Material>(folder+"/SlugVfxSmoke.mat")){var smoke=new Material(shader);smoke.SetFloat("_DstBlend",10);AssetDatabase.CreateAsset(smoke,folder+"/SlugVfxSmoke.mat");}
   var catalog=AssetDatabase.LoadAssetAtPath<Nexoterra.World.SlugCatalog>("Assets/NexoterraWorld/Slugs/Catalog.asset");
   if(catalog){bool changed=false;foreach(var slug in catalog.species)if(!slug.combatConfigured){slug.combat=Nexoterra.World.SlugCombatProfile.Default(slug.id);slug.combatConfigured=true;changed=true;}if(changed)EditorUtility.SetDirty(catalog);}
   AssetDatabase.SaveAssets();
  }
 }
}
