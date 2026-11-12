using UnityEngine;
namespace Nexoterra.World
{
 public sealed class PlayerHealth : MonoBehaviour,IDamageReceiver
 {
  public float maxHealth=100,respawnDelay=2;
  public float Health {get;private set;}
  public bool IsDead=>Health<=0;
  public bool CanMove=>!IsDead&&(!GetComponent<EnemyControlEffects>()||GetComponent<EnemyControlEffects>().CanMove);
  public void ReceiveSlug(SlugDefinition slug,bool transformed,Vector3 direction=default){if(IsDead||(GetComponent<PlayerAbilities>()&&GetComponent<PlayerAbilities>().Evading))return;ReceiveDamage(transformed?slug.Combat.impactDamage:slug.id.StartsWith("Malvada_")?3.15f:3);
   if(!IsDead&&EnemyControlEffects.CausesKnockdown(slug.id)){var effects=GetComponent<EnemyControlEffects>();if(!effects)effects=gameObject.AddComponent<EnemyControlEffects>();effects.BeginKnockdown();}
   if(!IsDead&&transformed){var control=GetComponent<EnemyControlEffects>();if(!control)control=gameObject.AddComponent<EnemyControlEffects>();control.Apply(slug.id,direction);if(slug.Combat.burnDuration>0){corruptedBurn=slug.id.StartsWith("Malvada_");burnRemaining=Mathf.Max(burnRemaining,slug.Combat.burnDuration);burnRate=slug.Combat.burnDamagePerSecond;}}}
  bool corruptedBurn;float burnRemaining,burnRate;GameObject burnVisual;
  float recovery;
  NexoterraThirdPerson player;
  void Awake(){Health=maxHealth;player=GetComponent<NexoterraThirdPerson>();}
  public void ReceiveDamage(float damage){if(IsDead||(GetComponent<PlayerAbilities>()&&GetComponent<PlayerAbilities>().Evading))return;Health=Mathf.Max(0,Health-Mathf.Max(0,damage));if(IsDead){recovery=Mathf.Max(2,respawnDelay);if(player)player.Notify("Derrotado · regresando al inicio");}}
  void Update(){if(!IsDead&&burnRemaining>0){float dt=Mathf.Min(Time.deltaTime,burnRemaining);burnRemaining-=dt;ReceiveDamage(burnRate*dt);if(!burnVisual)burnVisual=ImportedSlugVfx.BurningCharacter(transform);}if(corruptedBurn&&burnVisual)ImportedSlugVfx.Tint(burnVisual,new Color(.15f,1,.55f));if((IsDead||burnRemaining<=0)&&burnVisual)Destroy(burnVisual);if(IsDead)burnRemaining=0;if(!IsDead||ArenaSession.Current)return;recovery-=Time.deltaTime;if(recovery<=0){if(player)player.Teleport(player.spawn);Health=maxHealth;var control=GetComponent<EnemyControlEffects>();if(control)control.ResetEffects();}}
  public void ApplyArenaHealth(float value){bool dying=!IsDead&&value<=0;Health=Mathf.Clamp(value,0,maxHealth);if(dying)recovery=3;}
  public void CorruptionCost(){if(!IsDead)Health=Mathf.Max(1,Health-maxHealth*.01f);}
  public void Heal(float value){if(!IsDead)Health=Mathf.Min(maxHealth,Health+Mathf.Max(0,value));}
  public void Restore(float value){Health=Mathf.Clamp(value,1,maxHealth);recovery=0;}
 }
}


