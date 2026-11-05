using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Nexoterra.World;

namespace Nexoterra.Editor
{
    [InitializeOnLoad]
    public static class NexoterraSlugsSetup
    {
        const string Folder="Assets/NexoterraWorld/Slugs";
        static NexoterraSlugsSetup(){EditorApplication.update+=Poll;}
        static void Poll()
        {
            if(EditorApplication.isCompiling||EditorApplication.isUpdating||!File.Exists("Library/NexoterraSlugs.request"))return;
            File.Delete("Library/NexoterraSlugs.request");
            try{Install();}catch(Exception e){File.WriteAllText("Library/NexoterraSlugs.result.txt","FAIL\n"+e);Debug.LogException(e);}
        }
        public static void BatchInstall()
        {
            EditorSceneManager.OpenScene(NexoterraBiomeBuilder.ScenePath);Install();
        }
        [MenuItem("Nexoterra/Babosas/Instalar inventario y transformaciones")]
        public static void Install()
        {
            if(EditorApplication.isPlaying)throw new Exception("Salir de Play antes de instalar.");
            var scene=SceneManager.GetActiveScene();
            if(scene.path!=NexoterraBiomeBuilder.ScenePath)throw new Exception("Abrir la escena CuatroNodos.");
            // Model repair must work even when old spawn previews are missing.
            Directory.CreateDirectory(Folder+"/Models");Directory.CreateDirectory(Folder+"/Portraits");AssetDatabase.Refresh();
            string[][] sources={
                new[]{"Arana","Araña","Assets/NexoterraWorld/Biomes/Araña.prefab","Assets/02_Prefabs/Araña_T.fbx"},
                new[]{"Armashelt","Armashelt","Assets/02_Prefabs/Armashelt.fbx","Assets/02_Prefabs/Armashelt_T.fbx"},
                new[]{"Infierno","Infierno","Assets/02_Prefabs/Infierno.fbx","Assets/02_Prefabs/Infierno_T.prefab"},
                new[]{"Gon_Doc","Gon Doc","Assets/02_Prefabs/Gon_Doc.fbx","Assets/02_Prefabs/Gon_Doc_T.fbx"},
                new[]{"ice","Hielo","Assets/02_Prefabs/ice.fbx","Assets/02_Prefabs/Ice_T.fbx"},
                new[]{"tazerling","Tazerling","Assets/02_Prefabs/tazerling.fbx","Assets/02_Prefabs/tazerling_T.fbx"},
                new[]{"Carnero","Carnero","Assets/02_Prefabs/CARNERO.prefab","Assets/02_Prefabs/Carnero_T.fbx"},
                new[]{"Elemental_Agua","E. agua","Assets/02_Prefabs/ELEMENTAL_AGUA.prefab","Assets/02_Prefabs/ELEMENTAL_AGUA_T.fbx"},
                new[]{"Elemental_Aire","E. aire","Assets/02_Prefabs/ELEMENTAL_AIRE.prefab","Assets/02_Prefabs/ELEMENTAL_AIRE_T.prefab"},
                new[]{"Elemental_Fuego","E. fuego","Assets/02_Prefabs/ELEMENTAL_FUEGO.prefab","Assets/02_Prefabs/ELEMENTAL_FUEGO_T.prefab"},
                new[]{"Elemental_Tierra","E. tierra","Assets/02_Prefabs/ELEMENTAL_TIERRA.prefab","Assets/02_Prefabs/ELEMENTAL_TIERRA_T.prefab"}
            };
            var catalog=AssetDatabase.LoadAssetAtPath<SlugCatalog>(Folder+"/Catalog.asset");
            if(!catalog){catalog=ScriptableObject.CreateInstance<SlugCatalog>();AssetDatabase.CreateAsset(catalog,Folder+"/Catalog.asset");}
            foreach(string suffix in new[]{"","_T"})
            {
                string old=Folder+"/Models/Burpy"+suffix+".prefab",replacement=Folder+"/Models/Infierno"+suffix+".prefab";
                if(File.Exists(old)&&!File.Exists(replacement))AssetDatabase.MoveAsset(old,replacement);
            }
            catalog.species=sources.Select(s=>new SlugDefinition{id=s[0],displayName=s[1],unique=s[0].StartsWith("Elemental_"),normal=Visual(s[0],s[2]),transformed=Visual(s[0]+"_T",s[3])}).ToArray();
            foreach(var definition in catalog.species)
            {
                definition.portrait=Portrait(definition);
                Portrait(new SlugDefinition{id=definition.id+"_T",normal=definition.transformed});
            }
            EditorUtility.SetDirty(catalog);
            var player=UnityEngine.Object.FindAnyObjectByType<NexoterraThirdPerson>();
            if(!player)throw new Exception("Falta jugador");
            var inventory=player.GetComponent<SlugInventory>()??player.gameObject.AddComponent<SlugInventory>();inventory.catalog=catalog;
            // The imported gun points along local +Z. Put the origin at its front edge.
            var mount=player.muzzle.parent;var renderers=mount.GetComponentsInChildren<Renderer>();
            if(renderers.Length>0)
            {
                float front=float.NegativeInfinity;Vector3 center=Vector3.zero;int count=0;
                foreach(var r in renderers)
                {
                    Bounds b=r.localBounds;
                    for(int x=0;x<2;x++)for(int y=0;y<2;y++)for(int z=0;z<2;z++)
                    {
                        var p=mount.InverseTransformPoint(r.transform.TransformPoint(b.center+Vector3.Scale(b.extents,new Vector3(x*2-1,y*2-1,z*2-1))));
                        front=Mathf.Max(front,p.z);center+=p;count++;
                    }
                }
                center/=count;player.muzzle.localPosition=new Vector3(center.x,center.y,front+.025f);
            }
            if(player.tracer)player.tracer.enabled=false;
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            int bones=player.male.GetComponentsInChildren<SkinnedMeshRenderer>(true).Sum(r=>r.bones.Length)+player.female.GetComponentsInChildren<SkinnedMeshRenderer>(true).Sum(r=>r.bones.Length);
            foreach(var definition in catalog.species)
                if(!definition.normal||!definition.transformed||!definition.portrait)throw new Exception("Especie incompleta: "+definition.id);
            File.WriteAllText("Library/NexoterraSlugs.result.txt","PASS\n11 especies con modelo normal, transformacion y retrato.\nInventario de 6 espacios conectado.\nHuesos encontrados en jugadores: "+bones);
        }
        static GameObject Visual(string id,string source)
        {
            var asset=AssetDatabase.LoadAssetAtPath<GameObject>(source);if(!asset)throw new Exception("No existe "+source);
            var root=new GameObject(id);var model=(GameObject)PrefabUtility.InstantiatePrefab(asset);
            PrefabUtility.UnpackPrefabInstance(model,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
            model.transform.SetParent(root.transform,false);model.transform.localPosition=Vector3.zero;model.transform.localRotation=Quaternion.identity;model.transform.localScale=Vector3.one;
            if(id=="Elemental_Agua_T")model.transform.localRotation=Quaternion.Euler(-90,0,0);
            var rs=model.GetComponentsInChildren<Renderer>();if(rs.Length==0)throw new Exception("Modelo sin malla: "+source);
            Bounds b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);
            float extent=id.EndsWith("_T")?Mathf.Max(b.size.x,b.size.y,b.size.z):b.size.y;
            float scale=1/Mathf.Max(.01f,extent);model.transform.localScale=Vector3.one*scale;model.transform.localPosition=-b.center*scale;
            foreach(var t in root.GetComponentsInChildren<Transform>(true))t.gameObject.layer=2;
            var prefab=PrefabUtility.SaveAsPrefabAsset(root,Folder+"/Models/"+id+".prefab");UnityEngine.Object.DestroyImmediate(root);return prefab;
        }
        public static Texture2D Portrait(SlugDefinition definition)
        {
            var model=UnityEngine.Object.Instantiate(definition.normal);model.transform.position=new Vector3(0,-1000,0);
            foreach(var t in model.GetComponentsInChildren<Transform>(true))t.gameObject.layer=30;
            var cameraObject=new GameObject("Retrato temporal");var cam=cameraObject.AddComponent<Camera>();
            cam.cullingMask=1<<30;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=Color.clear;
            cam.orthographic=true;cam.nearClipPlane=.01f;cam.farClipPlane=20;
            cam.GetUniversalAdditionalCameraData().renderPostProcessing=false;
            var renderers=model.GetComponentsInChildren<Renderer>();Bounds b=renderers[0].bounds;foreach(var r in renderers)b.Encapsulate(r.bounds);
            cam.orthographicSize=Mathf.Max(b.size.y,b.size.x)*.65f;
            cam.transform.position=b.center+new Vector3(.65f,.2f,3);cam.transform.LookAt(b.center);
            var lightObject=new GameObject("Luz retrato");var light=lightObject.AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.6f;light.cullingMask=1<<30;lightObject.transform.rotation=Quaternion.Euler(25,-145,0);
            var rt=new RenderTexture(256,256,24,RenderTextureFormat.ARGB32);rt.Create();var before=RenderTexture.active;
            string path=Folder+"/Portraits/"+definition.id+".png";
            try
            {
                RenderPipeline.SubmitRenderRequest(cam,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});RenderTexture.active=rt;
                var texture=new Texture2D(256,256,TextureFormat.RGBA32,false);texture.ReadPixels(new Rect(0,0,256,256),0,0);texture.Apply();File.WriteAllBytes(path,texture.EncodeToPNG());UnityEngine.Object.DestroyImmediate(texture);
            }
            finally{RenderTexture.active=before;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(model);UnityEngine.Object.DestroyImmediate(cameraObject);UnityEngine.Object.DestroyImmediate(lightObject);}
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
