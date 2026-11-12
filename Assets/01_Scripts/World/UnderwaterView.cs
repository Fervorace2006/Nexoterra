using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Nexoterra.World
{
 // Camera-only immersion; the original lighting/fog is restored after rendering.
 public sealed class UnderwaterView : MonoBehaviour
 {
  struct Surface { public Transform transform;public Mesh mesh;public Vector3[] vertices;public int[] triangles;public Color color; }
  readonly List<Surface> surfaces=new List<Surface>();
  bool applied,oldFog;Color oldColor;FogMode oldMode;float oldDensity,oldStart,oldEnd;
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
  static void Install(){var go=new GameObject("Vista submarina");DontDestroyOnLoad(go);go.AddComponent<UnderwaterView>();}
  void OnEnable(){SceneManager.sceneLoaded+=Loaded;RenderPipelineManager.beginCameraRendering+=Begin;RenderPipelineManager.endCameraRendering+=End;Scan();}
  void Loaded(Scene scene,LoadSceneMode mode){Scan();}
  void Scan(){
   surfaces.Clear();
   foreach(var filter in FindObjectsByType<MeshFilter>(FindObjectsSortMode.None)){
    var renderer=filter.GetComponent<Renderer>();if(!renderer||!renderer.sharedMaterial||!filter.sharedMesh||!filter.sharedMesh.isReadable)continue;
    string shader=renderer.sharedMaterial.shader.name;if(shader!="Nexoterra/Cavern Water"&&shader!="Nexoterra/Sanctuary Water")continue;
    surfaces.Add(new Surface{transform=filter.transform,mesh=filter.sharedMesh,vertices=filter.sharedMesh.vertices,triangles=filter.sharedMesh.triangles,color=renderer.sharedMaterial.GetColor("_DeepColor")});
   }
  }
  bool Contains(Surface s,Vector3 position){
   if(!s.transform)return false;
   var p=s.transform.InverseTransformPoint(position);var bounds=s.mesh.bounds;
   if(p.x<bounds.min.x||p.x>bounds.max.x||p.z<bounds.min.z||p.z>bounds.max.z||p.y>bounds.max.y-.04f||p.y<bounds.min.y-8)return false;
   for(int i=0;i<s.triangles.Length;i+=3){
    Vector3 a=s.vertices[s.triangles[i]],b=s.vertices[s.triangles[i+1]],c=s.vertices[s.triangles[i+2]];
    float den=(b.z-c.z)*(a.x-c.x)+(c.x-b.x)*(a.z-c.z);if(Mathf.Abs(den)<.000001f)continue;
    float u=((b.z-c.z)*(p.x-c.x)+(c.x-b.x)*(p.z-c.z))/den;
    float v=((c.z-a.z)*(p.x-c.x)+(a.x-c.x)*(p.z-c.z))/den;
    if(u>=0&&v>=0&&u+v<=1&&p.y<u*a.y+v*b.y+(1-u-v)*c.y-.04f)return true;
   }return false;
  }
  void Begin(ScriptableRenderContext context,Camera camera){
   if(camera.cameraType!=CameraType.Game)return;
   Restore();
   foreach(var s in surfaces)if(Contains(s,camera.transform.position)){
    oldFog=RenderSettings.fog;oldColor=RenderSettings.fogColor;oldMode=RenderSettings.fogMode;oldDensity=RenderSettings.fogDensity;oldStart=RenderSettings.fogStartDistance;oldEnd=RenderSettings.fogEndDistance;
    applied=true;RenderSettings.fog=true;RenderSettings.fogMode=FogMode.ExponentialSquared;RenderSettings.fogColor=Color.Lerp(s.color,new Color(.04f,.23f,.32f),.25f);RenderSettings.fogDensity=.075f;break;
   }
  }
  void End(ScriptableRenderContext context,Camera camera){Restore();}
  void Restore(){if(!applied)return;RenderSettings.fog=oldFog;RenderSettings.fogColor=oldColor;RenderSettings.fogMode=oldMode;RenderSettings.fogDensity=oldDensity;RenderSettings.fogStartDistance=oldStart;RenderSettings.fogEndDistance=oldEnd;applied=false;}
  void OnDisable(){Restore();SceneManager.sceneLoaded-=Loaded;RenderPipelineManager.beginCameraRendering-=Begin;RenderPipelineManager.endCameraRendering-=End;}
 }
}
