using UnityEngine;
namespace Nexoterra.World
{
 [RequireComponent(typeof(EnemyCombatant))]
 public sealed class EnemyPracticeTarget : MonoBehaviour
 {
  public float speed=1.4f,range=4,respawnDelay=8;
  EnemyCombatant enemy;
  Vector3 origin;
  int direction=1;
  float deadTime;
  void Start(){enemy=GetComponent<EnemyCombatant>();origin=transform.position;}
  void Update()
  {
   if(enemy.IsDead){deadTime+=Time.deltaTime;if(deadTime>=respawnDelay){transform.position=origin;enemy.Revive();deadTime=0;}return;}
   if(!enemy.CanAct)return;
   if(Mathf.Abs(transform.position.x-origin.x)>range)direction=transform.position.x>origin.x?-1:1;
   transform.position+=Vector3.right*(direction*speed*enemy.SpeedMultiplier*Time.deltaTime);
  }
 }
}
