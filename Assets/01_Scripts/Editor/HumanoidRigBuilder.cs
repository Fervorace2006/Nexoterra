using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Nexoterra.World;
namespace Nexoterra.Editor
{
 public static class HumanoidRigBuilder
 {
  const string Folder="Assets/NexoterraWorld/Combat/Rigs";
  public static HumanoidPose Build(GameObject model,string key)
  {
   if(model.GetComponentInChildren<RiggedCharacterModel>(true))return null;
   var existing=model.GetComponentInChildren<HumanoidPose>(true);if(existing)return existing;
   var filters=model.GetComponentsInChildren<MeshFilter>(true).Where(f=>f.sharedMesh&&f.GetComponent<MeshRenderer>()).ToArray();if(filters.Length==0)return null;
   Bounds bounds=new Bounds();bool first=true;
   foreach(var f in filters)foreach(var v in f.sharedMesh.vertices){Vector3 p=model.transform.InverseTransformPoint(f.transform.TransformPoint(v));if(first){bounds=new Bounds(p,Vector3.zero);first=false;}else bounds.Encapsulate(p);}
   float h=bounds.size.y;var rig=new GameObject("Rig provisional · "+key).transform;rig.SetParent(model.transform,false);rig.localPosition=new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
   Vector3[] points={new Vector3(0,.5f,0),new Vector3(0,.62f,0),new Vector3(0,.76f,0),new Vector3(0,.85f,0),new Vector3(0,.92f,0),new Vector3(-.17f,.79f,0),new Vector3(-.33f,.79f,0),new Vector3(-.46f,.79f,0),new Vector3(.17f,.79f,0),new Vector3(.33f,.79f,0),new Vector3(.46f,.79f,0),new Vector3(-.085f,.5f,0),new Vector3(-.085f,.28f,0),new Vector3(-.085f,.06f,0),new Vector3(.085f,.5f,0),new Vector3(.085f,.28f,0),new Vector3(.085f,.06f,0)};
   int[] parents={-1,0,1,2,3,2,5,6,2,8,9,0,11,12,0,14,15};
   string[] names={"Hips","Spine","Chest","Neck","Head","LeftUpperArm","LeftForearm","LeftHand","RightUpperArm","RightForearm","RightHand","LeftThigh","LeftShin","LeftFoot","RightThigh","RightShin","RightFoot"};
   var bones=new Transform[points.Length];for(int i=0;i<bones.Length;i++){bones[i]=new GameObject(names[i]).transform;bones[i].SetParent(parents[i]<0?rig:bones[parents[i]],false);bones[i].position=rig.TransformPoint(points[i]*h);}
   int[] ends={1,2,3,4,4,6,7,7,9,10,10,12,13,13,15,16,16};
   System.IO.Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
   for(int n=0;n<filters.Length;n++)
   {
    var f=filters[n];var source=f.sharedMesh;var mesh=UnityEngine.Object.Instantiate(source);mesh.name=key+"_skin_"+n;var matrix=rig.worldToLocalMatrix*f.transform.localToWorldMatrix;
    var vertices=mesh.vertices;for(int i=0;i<vertices.Length;i++)vertices[i]=matrix.MultiplyPoint3x4(vertices[i]);mesh.vertices=vertices;
    var normalMatrix=matrix.inverse.transpose;var normals=mesh.normals;for(int i=0;i<normals.Length;i++)normals[i]=normalMatrix.MultiplyVector(normals[i]).normalized;mesh.normals=normals;
    var tangents=mesh.tangents;for(int i=0;i<tangents.Length;i++){Vector3 t=matrix.MultiplyVector(tangents[i]).normalized;tangents[i]=new Vector4(t.x,t.y,t.z,tangents[i].w);}mesh.tangents=tangents;
    var weights=new BoneWeight[vertices.Length];
    for(int i=0;i<vertices.Length;i++)
    {
     Vector3 v=vertices[i]/h;int[] candidates;
     if(v.y>.84f&&Mathf.Abs(v.x)<.18f)candidates=new[]{3,4};
     else if(Mathf.Abs(v.x)>.17f&&v.y>.52f)candidates=v.x<0?new[]{5,6,7}:new[]{8,9,10};
     else if(v.y<.51f)candidates=v.x<0?new[]{0,11,12,13}:new[]{0,14,15,16};
     else candidates=new[]{0,1,2,3};
     float best=float.MaxValue,next=float.MaxValue;int bi=0,ni=0;
     foreach(int bone in candidates){Vector3 a=points[bone],b=points[ends[bone]];if(a==b)b+=bone==4?Vector3.up*.08f:bone==13||bone==16?Vector3.forward*.12f:Vector3.right*Mathf.Sign(a.x)*.06f;Vector3 d=b-a;float t=Mathf.Clamp01(Vector3.Dot(v-a,d)/d.sqrMagnitude);float distance=(v-a-d*t).sqrMagnitude;if(distance<best){next=best;ni=bi;best=distance;bi=bone;}else if(distance<next){next=distance;ni=bone;}}
     float w=1/(best+.0005f),other=1/(next+.0005f);w=w*w;other=other*other;weights[i]=new BoneWeight{boneIndex0=bi,boneIndex1=ni,weight0=w/(w+other),weight1=other/(w+other)};
    }
    mesh.boneWeights=weights;mesh.bindposes=bones.Select(b=>b.worldToLocalMatrix*rig.localToWorldMatrix).ToArray();mesh.RecalculateBounds();
    string path=Folder+"/"+mesh.name+".asset";var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(saved){EditorUtility.CopySerialized(mesh,saved);UnityEngine.Object.DestroyImmediate(mesh);mesh=saved;}else AssetDatabase.CreateAsset(mesh,path);
    var go=new GameObject("Piel · "+f.name);go.layer=model.layer;go.transform.SetParent(rig,false);var skin=go.AddComponent<SkinnedMeshRenderer>();skin.sharedMesh=mesh;skin.bones=bones;skin.rootBone=bones[0];skin.sharedMaterials=f.GetComponent<MeshRenderer>().sharedMaterials;skin.localBounds=new Bounds(new Vector3(0,h*.5f,0),Vector3.one*h*2);skin.updateWhenOffscreen=true;f.GetComponent<MeshRenderer>().enabled=false;
   }
   var pose=rig.gameObject.AddComponent<HumanoidPose>();pose.height=h;pose.hips=bones[0];pose.spine=bones[1];pose.chest=bones[2];pose.head=bones[4];pose.leftArm=bones[5];pose.leftForearm=bones[6];pose.leftHand=bones[7];pose.rightArm=bones[8];pose.rightForearm=bones[9];pose.rightHand=bones[10];pose.leftThigh=bones[11];pose.leftShin=bones[12];pose.rightThigh=bones[14];pose.rightShin=bones[15];AssetDatabase.SaveAssets();return pose;
  }
 }
}
