using UnityEngine;
namespace Nexoterra.World
{
    public sealed class MushroomGlow : MonoBehaviour
    {
        public Light glow;
        Transform player;float next;
        void Start(){var p=FindFirstObjectByType<NexoterraThirdPerson>();if(p)player=p.transform;}
        void Update(){if(!glow||!player||Time.time<next)return;next=Time.time+1;glow.enabled=(player.position-transform.position).sqrMagnitude<1600;}
    }
}
