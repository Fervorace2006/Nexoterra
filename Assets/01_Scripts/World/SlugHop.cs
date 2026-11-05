using UnityEngine;

namespace Nexoterra.World
{
    // Ground-aware jump planning shared by wild slugs and returning companions.
    public static class SlugHop
    {
        public const int WorldMask=~(1<<2);
        public static Vector3 Sample(Vector3 start,Vector3 end,float t,float height)
            =>Vector3.Lerp(start,end,t)+Vector3.up*(4*height*t*(1-t));

        public static bool Plan(Vector3 start,Vector3 goal,float stride,float height,float footOffset,out Vector3 end)
        {
            end=start;Vector3 forward=goal-start;forward.y=0;
            if(forward.sqrMagnitude<.001f)return false;
            float distance=Mathf.Min(stride,forward.magnitude);forward.Normalize();float best=float.PositiveInfinity;
            foreach(float angle in new[]{0,30,-30,60,-60,90,-90,120,-120,150,-150})
            {
                Vector3 candidate=start+Quaternion.Euler(0,angle,0)*forward*distance;
                Vector3 origin=new Vector3(candidate.x,start.y+1.3f,candidate.z);
                if(!Physics.Raycast(origin,Vector3.down,out var ground,8,WorldMask,QueryTriggerInteraction.Ignore)||ground.normal.y<.6f)continue;
                candidate.y=ground.point.y+footOffset;
                if(Mathf.Abs(candidate.y-start.y)>1.0f)continue;
                bool clear=true;Vector3 previous=start+Vector3.up*.12f;
                for(int i=1;i<=10;i++)
                {
                    Vector3 next=Sample(start,candidate,i/10f,height)+Vector3.up*.12f;
                    Vector3 delta=next-previous;
                    if(Physics.SphereCast(previous,.075f,delta.normalized,out _,delta.magnitude,WorldMask,QueryTriggerInteraction.Ignore)){clear=false;break;}
                    previous=next;
                }
                if(!clear)continue;
                float score=Vector3.Distance(candidate,goal)+Mathf.Abs(angle)*.002f;
                if(score<best){best=score;end=candidate;}
            }
            return !float.IsPositiveInfinity(best);
        }
    }
}
