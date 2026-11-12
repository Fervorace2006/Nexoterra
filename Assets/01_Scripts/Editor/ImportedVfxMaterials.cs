using UnityEngine;
using UnityEditor;
[InitializeOnLoad] static class ImportedVfxMaterials {
 static ImportedVfxMaterials(){EditorApplication.delayCall+=Convert;}
 static void Convert(){var shader=Shader.Find("Universal Render Pipeline/Particles/Unlit");if(!shader)return;
 foreach(var guid in AssetDatabase.FindAssets("t:Material",new[]{"Assets/Matthew Guz/Status and Auras FREE"})){
 var m=AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));if(!m||m.shader==shader)continue;var tex=m.GetTexture("_MainTex");var color=m.GetColor("_Color");float dst=m.HasProperty("_DstBlend")?m.GetFloat("_DstBlend"):10;m.shader=shader;m.SetTexture("_BaseMap",tex);m.SetColor("_BaseColor",color);m.SetFloat("_Surface",1);m.SetFloat("_Blend",dst==1?2:0);m.SetFloat("_SrcBlend",5);m.SetFloat("_DstBlend",dst);m.SetFloat("_ZWrite",0);m.SetFloat("_Cull",0);m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");m.renderQueue=3000;EditorUtility.SetDirty(m);}
 AssetDatabase.SaveAssets();System.IO.File.WriteAllText("Library/ImportedVfxMaterials.result","PASS");}
}
