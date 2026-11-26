using UnityEngine;
namespace Nexoterra.World
{
 [RequireComponent(typeof(PlayerHealth))]
 public sealed class PlayerAbilities : MonoBehaviour
 {
  public const float QuickRollDuration=.62f;
  public float dodgeDuration=QuickRollDuration,dodgeDistance=4,dodgeCooldown=.9f;
  public float docHealing=25,windMultiplier=1.25f,windDuration=6;
  public bool IsDodging=>dodgeRemaining>0;
  public bool DodgeLeft {get;private set;}
  public int DodgeAxis {get;private set;}
  public Vector3 DodgeDirection {get;private set;}
  public float DodgeSpeed=>dodgeDistance/Mathf.Max(.1f,dodgeDuration);
  public float MovementMultiplier=>Time.time<boostUntil?windMultiplier:1;
  public bool Evading=>IsDodging&&dodgeRemaining<dodgeDuration-.08f&&dodgeRemaining>.1f;
  public float BoostRemaining=>Mathf.Max(0,boostUntil-Time.time);
  public float DodgeCooldown=>Mathf.Max(0,readyAt-Time.time);
  float dodgeRemaining,readyAt,boostUntil;PlayerHealth health;
  void Awake(){health=GetComponent<PlayerHealth>();}
  // Energy elemental self-heal: up to 40 % of max health over 5 s, once every 60 s (healing slugs is unlimited).
  public const float EnergySelfHeal=.4f,EnergyCooldown=60f,EnergyHealSeconds=5f;
  float energyReadyAt,energyHealLeft;
  public float EnergyCooldownLeft=>Mathf.Max(0,energyReadyAt-Time.time);
  void Update(){if(GameSession.Blocked)return;
   if(energyHealLeft>0){float step=Mathf.Min(energyHealLeft,health.maxHealth*EnergySelfHeal/EnergyHealSeconds*Time.deltaTime);health.Heal(step);energyHealLeft-=step;if(health.IsDead)energyHealLeft=0;}if(!health.CanMove)dodgeRemaining=0;else dodgeRemaining=Mathf.Max(0,dodgeRemaining-Time.deltaTime);}
  public bool TryDodge(int side,Vector3 right)
  {return TryDodge(side,right,false);}
  public bool TryDodge(int side,Vector3 axis,bool longitudinal)
  {
   var cc=GetComponent<CharacterController>();if(longitudinal)return false;   // only side rolls
   if(GameSession.Blocked||!health.CanMove||IsDodging||Time.time<readyAt||!cc.isGrounded)return false;
   DodgeLeft=side<0;DodgeAxis=longitudinal?(side<0?-1:1):0;axis.y=0;DodgeDirection=axis.normalized*(DodgeLeft?-1:1);dodgeRemaining=dodgeDuration;readyAt=Time.time+dodgeDuration+dodgeCooldown;if(PerfectDodge())TechShield.Activate(transform);return true;
  }
  // Perfect evasion: an enemy slug was about to hit (close and flying straight at us).
  bool PerfectDodge()
  {
   var body=transform.position+Vector3.up;
   foreach(var shot in FindObjectsByType<SlugProjectile>(FindObjectsSortMode.None))
   {
    if(!shot||!shot.Hostile||shot.IsReturning)continue;var to=body-shot.transform.position;
    if(to.magnitude<5.5f&&Vector3.Dot(shot.Direction,to.normalized)>.8f)return true;
   }
   return false;
  }
  public void OnSlugLaunched(string id)
  {
   if(id=="Gon_Doc"){health.Heal(docHealing);GetComponent<NexoterraThirdPerson>().Notify("Curación recibida");}
   if(id=="Elemental_Energia")
   {
    var who=GetComponent<NexoterraThirdPerson>();
    if(Time.time>=energyReadyAt){energyHealLeft=health.maxHealth*EnergySelfHeal;energyReadyAt=Time.time+EnergyCooldown;who.Notify("Energía · curación gradual (+40 % de salud)");}
    else who.Notify("Curación de Energía disponible en "+Mathf.CeilToInt(energyReadyAt-Time.time)+" s");
   }
   id=SlugCatalog.BaseSpecies(id);
   if(id=="Elemental_Aire"||id=="Tornado"){boostUntil=Time.time+windDuration;GetComponent<NexoterraThirdPerson>().Notify("Viento · movimiento +25% durante 6 s");}
  }
 }
}
