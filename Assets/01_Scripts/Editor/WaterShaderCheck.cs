using UnityEditor;
using System.IO;
[InitializeOnLoad]
static class WaterShaderCheck
{
 static WaterShaderCheck(){EditorApplication.update+=Tick;}
 static void Tick(){
  if(EditorApplication.isCompiling||EditorApplication.isUpdating||!File.Exists("Library/WaterCheck.request"))return;
  File.Delete("Library/WaterCheck.request");
  Validate();
 }
 public static void Validate(){
  var result="";
  foreach(var path in new[]{"Assets/NexoterraWorld/Shaders/CavernWater.shader","Assets/NexoterraWorld/Shaders/SanctuaryWater.shader"}){
   var shader=AssetDatabase.LoadAssetAtPath<UnityEngine.Shader>(path);
   result+=path+": "+(shader&&!ShaderUtil.ShaderHasError(shader)?"PASS":"FAIL")+"\n";
   if(shader)foreach(var message in ShaderUtil.GetShaderMessages(shader))result+=message.message+"\n";
  }
  File.WriteAllText("Library/WaterCheck.result",result);
 }
}
