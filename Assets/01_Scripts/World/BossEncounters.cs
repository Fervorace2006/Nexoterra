using UnityEngine;
namespace Nexoterra.World
{
 public sealed class BossEncounters : MonoBehaviour
 {
  public GameObject bossPrefab,finalBossPrefab;
  bool finalPhase;
  public bool CampaignComplete=>encounterPoints.Length>0&&Completed>=encounterPoints.Length;
  public Transform[] encounterPoints;
  public float triggerDistance=25;
  public string progressKey="Nexoterra.SirBlack.Encounters.v1";
  public int Completed {get;private set;}
  EnemyCombatant active;
  public EnemyCombatant Active=>active;
  public void Restore(int count){if(active)Destroy(active.gameObject);active=null;finalPhase=false;Completed=Mathf.Clamp(count,0,encounterPoints.Length);}
  PlayerHealth player;
  void Start(){Completed=GameSession.Current?0:Mathf.Clamp(PlayerPrefs.GetInt(progressKey,0),0,encounterPoints.Length);player=FindAnyObjectByType<PlayerHealth>();}
  void Update()
  {
   if(GameSession.Blocked||!player||player.IsDead||!bossPrefab||Completed>=encounterPoints.Length)return;
   if(active)
   {
    if(active.IsDead&&Completed==encounterPoints.Length-1&&!finalPhase&&finalBossPrefab){var point=active.transform.position;Destroy(active.gameObject);active=Instantiate(finalBossPrefab,point,Quaternion.identity).GetComponent<EnemyCombatant>();active.Revive();finalPhase=true;player.GetComponent<NexoterraThirdPerson>().Notify("Sir Black · fase final");return;}
    bool retreat=Completed<encounterPoints.Length-1&&active.Health<=active.maxHealth*.4f;
    if(active.IsDead||retreat){Destroy(active.gameObject);Completed++;if(!GameSession.Current){PlayerPrefs.SetInt(progressKey,Completed);PlayerPrefs.Save();}var p=player.GetComponent<NexoterraThirdPerson>();if(p)p.Notify(retreat?"Sir Black se retira. Volveras a encontrarlo.":"HISTORIA COMPLETADA · Sir Black derrotado. Guarda tu partida.");}return;
   }
   if(Vector3.Distance(player.transform.position,encounterPoints[Completed].position)>triggerDistance)return;
   var go=Instantiate(bossPrefab,encounterPoints[Completed].position,encounterPoints[Completed].rotation);active=go.GetComponent<EnemyCombatant>();active.Revive();
  }
 }
}
