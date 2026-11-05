using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Nexoterra.Editor
{
    [InitializeOnLoad]
    public static class NexoterraAssetAudit
    {
        static NexoterraAssetAudit(){EditorApplication.update+=Poll;}
        static void Poll()
        {
            const string request="Library/NexoterraAssets.request";
            if(EditorApplication.isCompiling||EditorApplication.isUpdating||!File.Exists(request))return;
            File.Delete(request);Audit();
        }
        public static void Audit()
        {
            var s=new StringBuilder();
            foreach(string path in AssetDatabase.GetAllAssetPaths())
            {
                if(!(path.StartsWith("Assets/02_Prefabs/")||path.StartsWith("Assets/03_Models/"))||!(path.EndsWith(".fbx",StringComparison.OrdinalIgnoreCase)||path.EndsWith(".prefab")))continue;
                var model=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(!model)continue;
                s.AppendLine(path);
                var instance=UnityEngine.Object.Instantiate(model);instance.transform.position=Vector3.zero;instance.transform.rotation=Quaternion.identity;instance.transform.localScale=Vector3.one;
                Bounds bounds=new Bounds();bool first=true;
                foreach(var r in instance.GetComponentsInChildren<Renderer>())
                {if(first){bounds=r.bounds;first=false;}else bounds.Encapsulate(r.bounds);foreach(var m in r.sharedMaterials)if(m)s.AppendLine(" MATERIAL "+m.name+" | "+m.shader.name+" | "+AssetDatabase.GetAssetPath(m));}
                s.AppendLine(" BOUNDS "+bounds.center+" / "+bounds.size);
                foreach(var asset in AssetDatabase.LoadAllAssetsAtPath(path))if(asset is AnimationClip c && !c.name.StartsWith("__preview"))s.AppendLine(" CLIP "+c.name+" | "+c.length);
                UnityEngine.Object.DestroyImmediate(instance);
            }
            File.WriteAllText("Library/NexoterraAssets.txt",s.ToString());Debug.Log("NEXOTERRA_ASSET_AUDIT_OK");
        }
    }
}
