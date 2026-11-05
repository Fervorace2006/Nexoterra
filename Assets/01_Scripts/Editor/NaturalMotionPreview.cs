using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Nexoterra.World;
using Object=UnityEngine.Object;
namespace Nexoterra.Editor{
[InitializeOnLoad] public static class NaturalMotionPreview {
 static NaturalMotionPreview(){EditorApplication.update+=Tick;}
 static void Tick(){if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlaying||!File.Exists("Library/NaturalPreview.request"))return;File.Delete("Library/NaturalPreview.request");try{Capture();File.WriteAllText("Library/NaturalPreview.result.txt","PASS");}catch(Exception e){File.WriteAllText("Library/NaturalPreview.result.txt",e.ToString());}}
 public static void Capture(){
 var player=Object.FindAnyObjectByType<NativePlayerAnimation>();var camGo=new GameObject("Motion QA camera");var cam=camGo.AddComponent<Camera>();cam.cullingMask=1<<30;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.055f,.07f,.09f);cam.orthographic=true;cam.orthographicSize=1.2f;cam.nearClipPlane=.01f;cam.farClipPlane=20;cam.GetUniversalAdditionalCameraData().renderPostProcessing=false;cam.GetUniversalAdditionalCameraData().renderShadows=false;
 var lightGo=new GameObject("Motion QA key");var light=lightGo.AddComponent<Light>();light.type=LightType.Directional;light.intensity=2;light.color=new Color(1,.95f,.9f);light.cullingMask=1<<30;light.transform.rotation=Quaternion.Euler(30,150,0);
 var oldFog=RenderSettings.fog;RenderSettings.fog=false;var rt=new RenderTexture(640,640,24);rt.Create();var before=RenderTexture.active;var report=new System.Text.StringBuilder();
 try{foreach(string role in new[]{"Player_Male","Player_Female"}){
 var model=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/NexoterraWorld/Characters/"+role+".prefab"));var a=model.GetComponentInChildren<Animator>();model.transform.position=new Vector3(0,3000,0);foreach(var mb in model.GetComponentsInChildren<MonoBehaviour>())mb.enabled=false;foreach(var t in model.GetComponentsInChildren<Transform>())t.gameObject.layer=30;foreach(var r in model.GetComponentsInChildren<Renderer>())r.shadowCastingMode=ShadowCastingMode.Off;
 var weapon=Object.Instantiate(player.weapon.gameObject);foreach(var t in weapon.GetComponentsInChildren<Transform>())t.gameObject.layer=30;var hand=a.GetBoneTransform(HumanBodyBones.RightHand);weapon.transform.SetParent(hand,false);weapon.transform.localPosition=role=="Player_Male"?player.maleGrip:player.femaleGrip;weapon.transform.localRotation=role=="Player_Male"?player.maleGripRotation:player.femaleGripRotation;var hs=hand.lossyScale;var ws=player.weapon.lossyScale;weapon.transform.localScale=new Vector3(ws.x/hs.x,ws.y/hs.y,ws.z/hs.z);weapon.transform.localPosition+=Vector3.Scale(weapon.transform.localRotation*(Vector3.down*player.gripHeightCorrection+Vector3.left*.01f),new Vector3(1/hs.x,1/hs.y,1/hs.z));
 try{a.Rebind();a.Update(0);foreach(string state in new[]{"Idle","Walk","Run","Aim"}){
 a.Play(state=="Aim"?"Idle":state,0,.25f);a.SetLayerWeight(1,state=="Aim"?1:0);a.Update(.001f);
 foreach(var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>()){skin.updateWhenOffscreen=true;var baked=new Mesh();skin.BakeMesh(baked);var mf=skin.GetComponent<MeshFilter>();if(!mf)mf=skin.gameObject.AddComponent<MeshFilter>();if(mf.sharedMesh)Object.DestroyImmediate(mf.sharedMesh);mf.sharedMesh=baked;var mr=skin.GetComponent<MeshRenderer>();if(!mr)mr=skin.gameObject.AddComponent<MeshRenderer>();mr.sharedMaterials=skin.sharedMaterials;skin.enabled=false;}
 var hips=a.GetBoneTransform(HumanBodyBones.Hips);var head=a.GetBoneTransform(HumanBodyBones.Head);var lf=a.GetBoneTransform(HumanBodyBones.LeftFoot);var rf=a.GetBoneTransform(HumanBodyBones.RightFoot);report.AppendLine(role+" "+state+" hands="+(hand.position-model.transform.position)+" Lfoot="+(lf.position-model.transform.position)+" Rfoot="+(rf.position-model.transform.position));
 var focus=model.transform.position+Vector3.up*.95f;cam.transform.position=focus+new Vector3(2,.3f,4);cam.transform.LookAt(focus);
 RenderPipeline.SubmitRenderRequest(cam,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});RenderPipeline.SubmitRenderRequest(cam,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});RenderTexture.active=rt;var tex=new Texture2D(640,640,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,640,640),0,0);tex.Apply();File.WriteAllBytes("Documentation/Nexoterra/Locomotion/"+role+"_"+state+".png",tex.EncodeToPNG());Object.DestroyImmediate(tex);
 }}finally{Object.DestroyImmediate(model);}
 }}finally{RenderTexture.active=before;rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(lightGo);Object.DestroyImmediate(camGo);RenderSettings.fog=oldFog;}
 File.WriteAllText("Documentation/Nexoterra/Locomotion/PoseChecks.txt",report.ToString());
 }
}}
