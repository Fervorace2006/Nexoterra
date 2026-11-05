using UnityEngine;
using UnityEngine.AI;
namespace Nexoterra.World
{
 [DefaultExecutionOrder(-300)]
 public sealed class SlugNavigation : MonoBehaviour
 {
  public NavMeshData data;
  NavMeshDataInstance instance;
  void OnEnable(){if(data)instance=NavMesh.AddNavMeshData(data);}
  void OnDisable(){if(instance.valid)instance.Remove();}
 }
 public sealed class SlugReturnRoute
 {
  readonly NavMeshPath path=new NavMeshPath();Vector3[] corners;int index;float refresh;Vector3 destination;
  public bool HasRoute {get;private set;}
  public Vector3 Next(Vector3 position,Vector3 goal)
  {
   if(Time.time>=refresh||Vector3.Distance(goal,destination)>2)
   {
    refresh=Time.time+.85f;destination=goal;
    if(NavMesh.SamplePosition(position-Vector3.up*.22f,out var from,1.5f,NavMesh.AllAreas)&&NavMesh.SamplePosition(goal-Vector3.up*.22f,out var to,1.5f,NavMesh.AllAreas)&&NavMesh.CalculatePath(from.position,to.position,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete)
    {corners=path.corners;index=1;HasRoute=corners.Length>1;}
    else {HasRoute=false;corners=null;}
   }
   if(!HasRoute)return goal;
   while(index<corners.Length-1&&Vector3.Distance(position,corners[index]+Vector3.up*.22f)<.65f)index++;
   return corners[Mathf.Min(index,corners.Length-1)]+Vector3.up*.22f;
  }
 }
}
