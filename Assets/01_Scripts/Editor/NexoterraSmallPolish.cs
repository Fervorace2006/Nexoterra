using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace Nexoterra.Editor
{
 [InitializeOnLoad] public static class NexoterraSmallPolish
 {
  static NexoterraSmallPolish(){EditorApplication.update+=Poll;}
  static void Poll(){if(EditorApplication.isCompiling||EditorApplication.isUpdating||!File.Exists("Library/SmallPolish.request"))return;if(EditorApplication.isPlayingOrWillChangePlaymode){EditorApplication.isPlaying=false;return;}File.Delete("Library/SmallPolish.request");try{Build();}catch(Exception e){File.WriteAllText("Library/SmallPolish.result.txt",e.ToString());Debug.LogException(e);}}
  public static void Build()
  {
   AssetDatabase.Refresh();var source=AssetDatabase.LoadAllAssetsAtPath("Assets/Characters/Animations/Pistol_Run.fbx").OfType<AnimationClip>().First(c=>!c.name.StartsWith("__preview"));
   var bindings=AnimationUtility.GetCurveBindings(source);string[] components={"x","y","z","w"};var rotation=components.Select(c=>AnimationUtility.GetEditorCurve(source,bindings.First(b=>b.propertyName=="RootQ."+c))).ToArray();
   foreach(string name in new[]{"Walk_Pistol","Idle_Pistol"})
   {
    string path="Assets/NexoterraWorld/Expansion/"+name+".anim";var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);bool idle=name.StartsWith("Idle");
    Directory.CreateDirectory("Library/SmallPolish");string backup="Library/SmallPolish/"+name+".anim";if(!File.Exists(backup))File.Copy(path,backup);
    var upright=new[]{new AnimationCurve(),new AnimationCurve(),new AnimationCurve(),new AnimationCurve()};
    int samples=idle?1:Mathf.CeilToInt(source.length*60);
    for(int i=0;i<=samples;i++){float time=idle?i:i*source.length/samples,at=idle?0:time;var q=new Quaternion(rotation[0].Evaluate(at),rotation[1].Evaluate(at),rotation[2].Evaluate(at),rotation[3].Evaluate(at));var yaw=Quaternion.Euler(0,q.eulerAngles.y,0);for(int k=0;k<4;k++)upright[k].AddKey(time,yaw[k]);}
    for(int k=0;k<4;k++)AnimationUtility.SetEditorCurve(clip,bindings.First(b=>b.propertyName=="RootQ."+components[k]),upright[k]);
    foreach(var b in bindings.Where(b=>(b.propertyName.StartsWith("Spine ")||b.propertyName.StartsWith("Chest ")||b.propertyName.StartsWith("UpperChest "))&&(b.propertyName.EndsWith("Front-Back")||b.propertyName.EndsWith("Left-Right"))))
    {
     var curve=AnimationUtility.GetEditorCurve(source,b);var keys=curve.keys;for(int i=0;i<keys.Length;i++){keys[i].value*=.25f;keys[i].inTangent*=.25f;keys[i].outTangent*=.25f;}
     AnimationUtility.SetEditorCurve(clip,b,idle?AnimationCurve.Constant(0,1,keys[0].value):new AnimationCurve(keys));
    }
    EditorUtility.SetDirty(clip);
   }
   AssetDatabase.SaveAssets();File.WriteAllText("Library/SmallPolish.result.txt","PASS: marcha erguida; clips guardados sin regenerar escenas ni prefabs.");
  }
 }
}
