using UnityEngine;
namespace Nexoterra.World
{
 public sealed class PlayerHealth : MonoBehaviour,IDamageReceiver
 {
  public float maxHealth=100,respawnDelay=2;
  public float Health {get;private set;}
  public bool IsDead=>Health<=0;
  public bool CanMove=>!IsDead&&(!GetComponent<EnemyControlEffects>()||GetComponent<EnemyControlEffects>().CanMove);
  public void ReceiveSlug(SlugDefinition slug,bool transformed){if(IsDead||(GetComponent<PlayerAbilities>()&&GetComponent<PlayerAbilities>().Evading))return;ReceiveDamage(transformed?slug.Combat.impactDamage:3);if(!IsDead&&transformed&&SlugCatalog.Canonical(slug.id.Replace("Malvada_",""))=="Arana"){var control=GetComponent<EnemyControlEffects>();if(control)control.Apply("Arana",Vector3.zero);}}
  float recovery;
  NexoterraThirdPerson player;
  void Awake(){Health=maxHealth;player=GetComponent<NexoterraThirdPerson>();}
  public void ReceiveDamage(float damage){if(IsDead||(GetComponent<PlayerAbilities>()&&GetComponent<PlayerAbilities>().Evading))return;Health=Mathf.Max(0,Health-Mathf.Max(0,damage));if(IsDead){recovery=Mathf.Max(2,respawnDelay);if(player)player.Notify("Derrotado · regresando al inicio");}}
  void Update(){if(!IsDead)return;recovery-=Time.deltaTime;if(recovery<=0){if(player)player.Teleport(player.spawn);Health=maxHealth;var control=GetComponent<EnemyControlEffects>();if(control)control.ResetEffects();}}
  public void Heal(float value){if(!IsDead)Health=Mathf.Min(maxHealth,Health+Mathf.Max(0,value));}
  public void Restore(float value){Health=Mathf.Clamp(value,1,maxHealth);recovery=0;}
 }
}
