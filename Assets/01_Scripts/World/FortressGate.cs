using UnityEngine;
using System.Linq;
namespace Nexoterra.World
{
 // Sir Black's base gate: the boss waits inside, frozen, until the patrol outside is defeated.
 // Then the two doors slide apart, the red beacons flash and the boss walks out to fight.
 public sealed class FortressGate : MonoBehaviour
 {
  public Transform leftDoor,rightDoor;
  public EnemyCombatant boss;
  public float triggerDistance=34,openSeconds=2.2f,slide=4.2f;
  public Light[] beacons;
  Vector3 leftClosed,rightClosed;float openT;bool opening;Transform player;EnemyShooter ai;float bossHealth;
  int OutsideRemaining()
  {
   var level=CampaignLevel.Current;
   if(level&&level.enemies!=null&&level.enemies.Length>0)return level.enemies.Count(e=>e&&e!=boss&&(!e.gameObject.activeInHierarchy||!e.IsDead));
   return FindObjectsByType<EnemyCombatant>().Count(e=>e!=boss&&!e.IsDead);
  }
  void Start()
  {
   if(leftDoor)leftClosed=leftDoor.localPosition;if(rightDoor)rightClosed=rightDoor.localPosition;
   var p=FindAnyObjectByType<NexoterraThirdPerson>();if(p)player=p.transform;
   if(boss){ai=boss.GetComponent<EnemyShooter>();bossHealth=boss.Health;if(ai)ai.enabled=false;}
  }
  void Update()
  {
   if(!opening)
   {
    // Locked until every enemy outside the base is down; then the boss comes out.
    if(OutsideRemaining()>0||Time.timeSinceLevelLoad<2){if(beacons!=null)foreach(var b in beacons)if(b)b.color=new Color(1,.15f,.1f);return;}
    opening=true;NexoAudio.PlayAt("slug_launch",transform.position,.8f);
   }
   openT=Mathf.MoveTowards(openT,1,Time.deltaTime/openSeconds);float k=Mathf.SmoothStep(0,1,openT);
   if(leftDoor)leftDoor.localPosition=leftClosed+Vector3.left*slide*k;
   if(rightDoor)rightDoor.localPosition=rightClosed+Vector3.right*slide*k;
   if(beacons!=null)foreach(var b in beacons)if(b)b.intensity=openT<1?(Mathf.Repeat(Time.time*3,1)<.5f?6:1):2;
   if(openT>.55f&&ai&&!ai.enabled)ai.enabled=true;
   if(openT>=1&&(!ai||ai.enabled))enabled=false;
  }
 }
}
