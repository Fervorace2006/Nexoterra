using UnityEngine;
namespace Nexoterra.World
{
 // Sir Black's base gate: the boss waits inside, frozen, until the player comes close or hits him.
 // Then the two doors slide apart, the red beacons flash and the boss walks out to fight.
 public sealed class FortressGate : MonoBehaviour
 {
  public Transform leftDoor,rightDoor;
  public EnemyCombatant boss;
  public float triggerDistance=34,openSeconds=2.2f,slide=4.2f;
  public Light[] beacons;
  Vector3 leftClosed,rightClosed;float openT;bool opening;Transform player;EnemyShooter ai;float bossHealth;
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
    bool near=player&&Vector3.Distance(player.position,transform.position)<triggerDistance;
    bool hit=boss&&boss.Health<bossHealth-.01f;
    if(near||hit||!boss){opening=true;NexoAudio.PlayAt("slug_launch",transform.position,.8f);}
    else return;
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
