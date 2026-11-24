using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Nexoterra.World;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class UltimasSlugInstaller
{
    const string Folder="Assets/02_Prefabs/Gameplay/Ultimas";
    static UltimasSlugInstaller(){EditorApplication.update+=Poll;}
    static void Poll()
    {
        if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||!File.Exists("Library/Ultimas.request"))return;
        File.Delete("Library/Ultimas.request");
        try{Install();File.WriteAllText("Library/Ultimas.result","PASS");}catch(Exception e){File.WriteAllText("Library/Ultimas.result",e.ToString());Debug.LogException(e);}
    }
    static GameObject Model(string folder,string id)
    {
        string dir="Assets/Characters/Ultimas/"+folder;
        string file=Directory.GetFiles(dir,"*.fbx",SearchOption.AllDirectories).First().Replace((char)92,'/');
        var model=AssetDatabase.LoadAssetAtPath<GameObject>(file);if(!model)throw new Exception("Modelo sin importar: "+file);
        var root=new GameObject(id);var visual=(GameObject)PrefabUtility.InstantiatePrefab(model);visual.transform.SetParent(root.transform,false);
        try{
            string tex=Directory.GetFiles(dir,"*",SearchOption.AllDirectories).First(p=>p.ToLower().Contains("basecolor")&&!p.EndsWith(".meta")).Replace((char)92,'/');
            string matPath=Folder+"/"+id+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if(!mat){mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(mat,matPath);}
            mat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(tex));mat.SetFloat("_Smoothness",.25f);EditorUtility.SetDirty(mat);
            foreach(var r in visual.GetComponentsInChildren<Renderer>())r.sharedMaterials=Enumerable.Repeat(mat,r.sharedMaterials.Length).ToArray();
            foreach(var c in visual.GetComponentsInChildren<Collider>())Object.DestroyImmediate(c);
            foreach(var a in visual.GetComponentsInChildren<Animator>())a.applyRootMotion=false;
            var rs=visual.GetComponentsInChildren<Renderer>();var b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);
            float scale=1/Mathf.Max(.01f,b.size.y);visual.transform.localScale*=scale;visual.transform.localPosition=-b.center*scale+Vector3.up*.5f;
            return PrefabUtility.SaveAsPrefabAsset(root,Folder+"/"+id+".prefab");
        }finally{Object.DestroyImmediate(root);}
    }
    static void Install()
    {
        Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
        var cat=AssetDatabase.LoadAssetAtPath<SlugCatalog>("Assets/NexoterraWorld/Slugs/Catalog.asset");
        var list=cat.species.ToList();
        string[] ids={"Demoledora","Malvada_Demoledora","Malvada_Goon"};
        string[] folders={"Demoledora","Demoledora_Malvada","Goon_Malvada"};
        for(int i=0;i<ids.Length;i++){
            var d=cat.Find(ids[i])??new SlugDefinition{id=ids[i]};
            d.displayName=i==0?"Demoledora":i==1?"Demoledora malvada":"Goon malvada";
            d.normal=Model(folders[i],ids[i]);d.transformed=Model(folders[i]+"_T",ids[i]+"_T");
            d.combat=SlugCombatProfile.Default(ids[i]);d.combatConfigured=true;d.usePrefabCombat=false;d.accent=i==0?new Color(1,.6f,.2f):new Color(1,.1f,.15f);
            d.portrait=Nexoterra.Editor.NexoterraUiPolish.Portrait(d.normal,d.id,false);
            if(!list.Contains(d))list.Add(d);
        }
        cat.species=list.ToArray();EditorUtility.SetDirty(cat);
        var spawnRoot=new GameObject("Demoledora silvestre");
        var visual=Object.Instantiate(cat.Find("Demoledora").normal,spawnRoot.transform);visual.transform.localScale=Vector3.one*.5f;
        var col=spawnRoot.AddComponent<SphereCollider>();col.radius=.3f;col.center=Vector3.up*.2f;col.isTrigger=true;
        spawnRoot.AddComponent<WildSlug>().species="Demoledora";
        var prefab=PrefabUtility.SaveAsPrefabAsset(spawnRoot,Folder+"/Demoledora_Wild.prefab");Object.DestroyImmediate(spawnRoot);
        AssetDatabase.SaveAssets();
        // Edit only selected spawns/loadout fields; never regenerate campaign geometry.
        for(int index=0;index<CampaignLevel.Scenes.Length;index++){
            if(index!=0&&index!=1&&index!=4&&index!=5)continue;
            string path="Assets/00_Scenes/Campaign/"+CampaignLevel.Scenes[index]+".unity";
            var scene=SceneManager.GetSceneByPath(path);bool opened=!scene.IsValid()||!scene.isLoaded;
            if(!opened&&scene.isDirty)throw new Exception("Escena con cambios sin guardar: "+path);
            if(opened)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
            try{
                var roots=scene.GetRootGameObjects();var level=roots.SelectMany(r=>r.GetComponentsInChildren<CampaignLevel>(true)).Single();
                if(index<2){
                    var spawn=roots.SelectMany(r=>r.GetComponentsInChildren<SlugSpawnPoint>(true)).FirstOrDefault(s=>s.name=="Demoledora junto a la entrada");
                    if(!spawn){var go=new GameObject("Demoledora junto a la entrada");SceneManager.MoveGameObjectToScene(go,scene);spawn=go.AddComponent<SlugSpawnPoint>();}
                    spawn.species="Demoledora";spawn.biome=CampaignLevel.Titles[index];spawn.creaturePrefab=prefab;spawn.population=1;
                    spawn.transform.position=level.entrance.position+new Vector3(7,0,6);
                }else{
                    foreach(var enemy in level.enemies.Where(e=>e)){
                        bool eligible=index==4?(enemy==level.trapGuard||enemy==level.ambushBoss):enemy==level.guardian;
                        if(!eligible)continue;
                        var ai=enemy.GetComponent<EnemyShooter>();if(!ai)continue;
                        var slots=ai.slugSlots.ToList();if(!slots.Contains("Malvada_Goon")){if(slots.Count<3)slots.Add("Malvada_Goon");else slots[2]="Malvada_Goon";}
                        ai.slugSlots=slots.ToArray();EditorUtility.SetDirty(ai);PrefabUtility.RecordPrefabInstancePropertyModifications(ai);
                    }
                }
                EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            }finally{if(opened)EditorSceneManager.CloseScene(scene,true);}
        }
        AssetDatabase.SaveAssets();
    }
}
