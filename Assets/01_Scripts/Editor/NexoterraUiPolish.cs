using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Nexoterra.World;
namespace Nexoterra.Editor
{
 [InitializeOnLoad] public static class NexoterraUiPolish
 {
  const string Folder="Assets/NexoterraWorld/Interface/PortraitsHD";
  static NexoterraUiPolish(){EditorApplication.update+=Poll;}
  static void Poll(){if(EditorApplication.isCompiling||EditorApplication.isUpdating||!File.Exists("Library/UiPolish.request"))return;if(EditorApplication.isPlayingOrWillChangePlaymode){EditorApplication.isPlaying=false;return;}File.Delete("Library/UiPolish.request");try{Build();}catch(Exception e){File.WriteAllText("Library/UiPolish.result.txt","FAIL\n"+e);Debug.LogException(e);}}
  public static void Build()
  {
   AssetDatabase.Refresh();var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();if(scene.path==NexoterraBiomeBuilder.ScenePath&&scene.isDirty)EditorSceneManager.SaveScene(scene);EditorSceneManager.OpenScene(NexoterraBiomeBuilder.ScenePath);
   File.Copy(NexoterraBiomeBuilder.ScenePath,"Library/BeforeUiPolish_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".unity",true);Directory.CreateDirectory(Folder);
   var inventory=UnityEngine.Object.FindAnyObjectByType<SlugInventory>();foreach(var d in inventory.catalog.species)d.portrait=Portrait(d.normal,d.id,false);EditorUtility.SetDirty(inventory.catalog);
   var hud=UnityEngine.Object.FindAnyObjectByType<NexoHud>();hud.malePortrait=Portrait(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/02_Prefabs/Gameplay/Models/Player_Male.prefab"),"Player_Male",true);hud.femalePortrait=Portrait(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/02_Prefabs/Gameplay/Models/Player_Female.prefab"),"Player_Female",true);
   EditorUtility.SetDirty(hud);EditorSceneManager.MarkSceneDirty(hud.gameObject.scene);EditorSceneManager.SaveScene(hud.gameObject.scene);AssetDatabase.SaveAssets();File.WriteAllText("Library/UiPolish.result.txt","PASS: "+inventory.catalog.species.Length+" retratos de babosas y 2 retratos de jugador en 512 px.");
  }
  public static Texture2D Portrait(GameObject source,string id,bool character)
  {
   if(!source)throw new Exception("Falta modelo "+id);
   var stage=new GameObject("Estudio temporal de retratos");var model=UnityEngine.Object.Instantiate(source,stage.transform);model.SetActive(true);model.transform.position=new Vector3(0,3000,0);model.transform.rotation=Quaternion.identity;
   foreach(var t in model.GetComponentsInChildren<Transform>(true))t.gameObject.layer=30;
   foreach(var mb in model.GetComponentsInChildren<MonoBehaviour>(true))mb.enabled=false;
   var animator=model.GetComponentInChildren<Animator>();if(animator)animator.enabled=false;
   var renderers=model.GetComponentsInChildren<Renderer>().Where(r=>r is MeshRenderer||r is SkinnedMeshRenderer).ToArray();var b=renderers[0].bounds;foreach(var r in renderers){b.Encapsulate(r.bounds);r.shadowCastingMode=ShadowCastingMode.Off;}
   Vector3 focus=b.center;float size=Mathf.Max(b.size.y*.53f,b.size.x*.58f);
   if(character){var head=animator&&animator.isHuman?animator.GetBoneTransform(HumanBodyBones.Head):null;focus=head?head.position-Vector3.up*.025f:b.center+Vector3.up*b.size.y*.35f;size=.27f;}
   var cam=new GameObject("Cámara").AddComponent<Camera>();cam.transform.SetParent(stage.transform);cam.cullingMask=1<<30;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=Color.clear;cam.orthographic=true;cam.orthographicSize=size;cam.nearClipPlane=.01f;cam.farClipPlane=20;cam.transform.position=focus+new Vector3(character?.12f:.3f,character?.015f:.12f,3);cam.transform.LookAt(focus);cam.GetUniversalAdditionalCameraData().renderPostProcessing=false;
   void Light(string name,Vector3 rotation,float strength,Color color){var l=new GameObject(name).AddComponent<Light>();l.transform.SetParent(stage.transform);l.type=LightType.Directional;l.intensity=strength;l.color=color;l.cullingMask=1<<30;l.transform.rotation=Quaternion.Euler(rotation);}
   Light("Principal",new Vector3(25,155,0),2.1f,new Color(1,.94f,.87f));Light("Relleno",new Vector3(0,225,0),.75f,new Color(.7f,.88f,1));Light("Contorno",new Vector3(15,10,0),1.1f,new Color(.3f,.8f,1));
   bool fog=RenderSettings.fog;var ambient=RenderSettings.ambientLight;var mode=RenderSettings.ambientMode;RenderSettings.fog=false;RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.32f,.35f,.4f);
   var rt=new RenderTexture(512,512,24,RenderTextureFormat.ARGB32){antiAliasing=4};rt.Create();var before=RenderTexture.active;Texture2D tex=null;string path=Folder+"/"+id+".png";
   try{RenderPipeline.SubmitRenderRequest(cam,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});RenderTexture.active=rt;tex=new Texture2D(512,512,TextureFormat.RGBA32,false);tex.ReadPixels(new Rect(0,0,512,512),0,0);tex.Apply();File.WriteAllBytes(path,tex.EncodeToPNG());}
   finally{RenderTexture.active=before;rt.Release();UnityEngine.Object.DestroyImmediate(rt);if(tex)UnityEngine.Object.DestroyImmediate(tex);UnityEngine.Object.DestroyImmediate(stage);RenderSettings.fog=fog;RenderSettings.ambientMode=mode;RenderSettings.ambientLight=ambient;}
   AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);var imp=(TextureImporter)AssetImporter.GetAtPath(path);imp.alphaIsTransparency=true;imp.mipmapEnabled=false;imp.textureCompression=TextureImporterCompression.Uncompressed;imp.maxTextureSize=512;imp.filterMode=FilterMode.Bilinear;imp.SaveAndReimport();return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
  }
 }
}
