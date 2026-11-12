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
  float dodgeRemaining,readyAt,boostUntil;PlayerHealth health;
  void Awake(){health=GetComponent<PlayerHealth>();}
  void Update(){if(GameSession.Blocked)return;if(!health.CanMove)dodgeRemaining=0;else dodgeRemaining=Mathf.Max(0,dodgeRemaining-Time.deltaTime);}
  public bool TryDodge(int side,Vector3 right)
  {return TryDodge(side,right,false);}
  public bool TryDodge(int side,Vector3 axis,bool longitudinal)
  {
   var cc=GetComponent<CharacterController>();if(GameSession.Blocked||!health.CanMove||IsDodging||Time.time<readyAt||!cc.isGrounded)return false;
   DodgeLeft=side<0;DodgeAxis=longitudinal?(side<0?-1:1):0;axis.y=0;DodgeDirection=axis.normalized*(DodgeLeft?-1:1);dodgeRemaining=dodgeDuration;readyAt=Time.time+dodgeDuration+dodgeCooldown;return true;
  }
  public void OnSlugLaunched(string id)
  {
   if(id=="Gon_Doc"||id=="Elemental_Energia"){health.Heal(id=="Gon_Doc"?docHealing:32);GetComponent<NexoterraThirdPerson>().Notify("Curación recibida");}
   if(id=="Elemental_Aire"||id=="Tornado"){boostUntil=Time.time+windDuration;GetComponent<NexoterraThirdPerson>().Notify("Viento · movimiento +25% durante 6 s");}
  }
 }
}
