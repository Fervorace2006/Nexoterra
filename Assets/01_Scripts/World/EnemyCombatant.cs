using UnityEngine;
using UnityEngine.Events;
namespace Nexoterra.World
{
 public sealed class EnemyCombatant : MonoBehaviour, IDamageReceiver
 {
  [Min(1)] public float maxHealth=160;
  public string displayName="Enemigo";
  public Transform visualRoot;
  [Min(0)] public float corpseLifetime=8;
  public Texture2D portrait;
  public float LastDamageTime {get;private set;}=float.NegativeInfinity;
  float diedAt;
  Renderer[] hiddenRenderers;
  Collider[] hiddenColliders;
  public UnityEvent onDeath=new UnityEvent();
  public float Health {get;private set;}
  public bool IsDead=>Health<=0;
  public bool IsBurning=>burnRemaining>0&&!IsDead;
  public bool IsKnockedDown=>!IsDead&&(downRemaining>0||(Controls&&Controls.KnockdownPose!=null));
  public bool IsStunned=>stunRemaining>0&&!IsDead;
  public float SpeedMultiplier=>slowRemaining>0?slowFactor:1;
  public bool CanAct=>!IsDead&&!IsKnockedDown&&!IsStunned;
  public EnemyControlEffects Controls {get;private set;}
  public bool CanMove=>CanAct&&(!Controls||Controls.CanMove);
  public bool CanShoot=>CanAct&&(!Controls||Controls.CanShoot);
  float burnRemaining,burnRate,slowRemaining,slowFactor=1,downRemaining,stunRemaining;
  Vector3 knockVelocity;
  Quaternion upright;
  ParticleSystem status;GameObject burnVisual;bool corruptedBurn;
  int lastStatus;
  static GUIStyle label;
  Transform healthHead;
  Collider healthBody;

  void Start()
  {
   var animator=GetComponentInChildren<Animator>();
   if(animator && animator.isHuman) healthHead=animator.GetBoneTransform(HumanBodyBones.Head);
   healthBody=GetComponent<Collider>();
  }
  void Awake(){Controls=GetComponent<EnemyControlEffects>();if(!Controls)Controls=gameObject.AddComponent<EnemyControlEffects>();Health=maxHealth;upright=visualRoot?visualRoot.localRotation:Quaternion.identity;}
  public void ReceiveSlug(SlugDefinition slug,Vector3 direction,bool transformed)
  {
   if(IsDead||slug==null)return;
   if(EnemyControlEffects.CausesKnockdown(slug.id))Controls.BeginKnockdown();
   if(!transformed){Damage(slug.id.StartsWith("Malvada_")?3.15f:3);return;}
   Controls.Apply(slug.id,direction);var p=slug.Combat;Damage(Mathf.Max(0,p.impactDamage));if(IsDead)return;
   if(p.burnDuration>0){corruptedBurn=slug.id.StartsWith("Malvada_");burnRemaining=Mathf.Max(burnRemaining,p.burnDuration);burnRate=Mathf.Max(burnRate,p.burnDamagePerSecond);}
   if(p.slowDuration>0){slowFactor=slowRemaining>0?Mathf.Min(slowFactor,p.slowMultiplier):p.slowMultiplier;slowRemaining=Mathf.Max(slowRemaining,p.slowDuration);}
   downRemaining=Mathf.Max(downRemaining,p.knockdownDuration);stunRemaining=Mathf.Max(stunRemaining,p.stunDuration);
   direction.y=0;if(direction.sqrMagnitude<.01f)direction=transform.forward;
   if(p.knockbackSpeed>0)knockVelocity=direction.normalized*p.knockbackSpeed;
   RefreshVisuals();
  }
  public void ReceiveDamage(float amount){Damage(Mathf.Max(0,amount));}
  void Damage(float amount)
  {
   if(IsDead)return;if(amount>0)LastDamageTime=Time.time;Health=Mathf.Max(0,Health-amount);
   if(IsDead){diedAt=Time.time;burnRemaining=slowRemaining=downRemaining=stunRemaining=0;knockVelocity=Vector3.zero;RefreshVisuals();onDeath.Invoke();}
  }
  public void Advance(float dt)
  {
   dt=Mathf.Max(0,dt);if(IsDead)return;
   if(burnRemaining>0){float active=Mathf.Min(dt,burnRemaining);burnRemaining=Mathf.Max(0,burnRemaining-dt);Damage(active*burnRate);if(burnRemaining<=0)burnRate=0;}
   slowRemaining=Mathf.Max(0,slowRemaining-dt);downRemaining=Mathf.Max(0,downRemaining-dt);stunRemaining=Mathf.Max(0,stunRemaining-dt);
   if(knockVelocity.sqrMagnitude>.01f)
   {
    Vector3 delta=knockVelocity*((1-Mathf.Exp(-7*dt))/7);knockVelocity*=Mathf.Exp(-7*dt);
    float distance=delta.magnitude;
    foreach(var hit in Physics.SphereCastAll(transform.position+Vector3.up*.8f,.32f,delta.normalized,distance,1,QueryTriggerInteraction.Ignore))
     if(!hit.collider.transform.IsChildOf(transform))distance=Mathf.Min(distance,Mathf.Max(0,hit.distance-.02f));
    var cc=GetComponent<CharacterController>();if(cc&&cc.enabled)cc.Move(delta.normalized*distance);else transform.position+=delta.normalized*distance;
   }
   RefreshVisuals();
  }
  void Update(){Advance(Time.deltaTime);if(IsBurning&&!burnVisual)burnVisual=ImportedSlugVfx.BurningCharacter(transform);if(corruptedBurn&&burnVisual)ImportedSlugVfx.Tint(burnVisual,new Color(.15f,1,.55f));if(!IsBurning&&burnVisual)Destroy(burnVisual);}
  void LateUpdate()
  {
   if(visualRoot)visualRoot.localRotation=Quaternion.Slerp(visualRoot.localRotation,upright*Quaternion.Euler(0,0,(IsDead||IsKnockedDown)&&!GetComponent<NativeEnemyAnimation>()?82:0),1-Mathf.Exp(-12*Time.deltaTime));
  }
  void RefreshVisuals()
  {
   int current=IsBurning?1:SpeedMultiplier<1&&!IsDead?2:IsKnockedDown||IsStunned?3:0;if(current==lastStatus)return;lastStatus=current;
   if(status){status.Stop(true,ParticleSystemStopBehavior.StopEmitting);Destroy(status.gameObject,.7f);status=null;}
   if(current!=0)status=SlugFlightEffects.Status(transform,current==1?new Color(1,.3f,.04f):current==2?new Color(.25f,.75f,1):new Color(1,.85f,.25f));
  }
  public void Revive(){if(Controls)Controls.ResetEffects();Health=maxHealth;burnRemaining=slowRemaining=downRemaining=stunRemaining=burnRate=0;slowFactor=1;knockVelocity=Vector3.zero;RefreshVisuals();}
  void HideCorpse()
  {
   if(hiddenRenderers!=null)return;
   hiddenRenderers=System.Array.FindAll(GetComponentsInChildren<Renderer>(),r=>r.enabled);
   hiddenColliders=System.Array.FindAll(GetComponentsInChildren<Collider>(),c=>c.enabled);
   foreach(var r in hiddenRenderers)r.enabled=false;
   foreach(var c in hiddenColliders)c.enabled=false;
  }
  void FixedUpdate(){if(IsDead&&Time.time-diedAt>=corpseLifetime)HideCorpse();else if(!IsDead&&hiddenRenderers!=null){foreach(var r in hiddenRenderers)if(r)r.enabled=true;foreach(var c in hiddenColliders)if(c)c.enabled=true;hiddenRenderers=null;hiddenColliders=null;}}
  void DrawLegacyWorldBar()
  {
   if(GameSession.Blocked)return;var shooter=GetComponent<EnemyShooter>();if(shooter&&shooter.boss)return;
   var cam=Camera.main;if(!cam||Vector3.Distance(cam.transform.position,transform.position)>45)return;
   Vector3 anchor=transform.position+Vector3.up*1.9f;
   if(healthHead) anchor=healthHead.position+Vector3.up*.28f;
   else if(healthBody) anchor=new Vector3(healthBody.bounds.center.x,healthBody.bounds.max.y+.1f,healthBody.bounds.center.z);
   var p=cam.WorldToScreenPoint(anchor);if(p.z<=0)return;
   if(label==null)label=new GUIStyle(GUI.skin.label){alignment=TextAnchor.MiddleCenter,fontSize=12};
   float width=Mathf.Clamp(520f/p.z,28f,66f);
   float x=p.x-width*.5f,y=Screen.height-p.y-5;var previous=GUI.color;
   GUI.color=Color.black;GUI.DrawTexture(new Rect(x,y,width,5),Texture2D.whiteTexture);
   GUI.color=IsBurning?new Color(1,.4f,.1f):new Color(.3f,.9f,.5f);GUI.DrawTexture(new Rect(x+1,y+1,(width-2)*Health/maxHealth,3),Texture2D.whiteTexture);
   GUI.color=Color.white;string state=IsDead?"Derrotado":IsKnockedDown?"Derribado":IsStunned?"Aturdido":IsBurning?"Quemadura":SpeedMultiplier<1?"Ralentizado":"";
   GUI.Label(new Rect(p.x-80,y-19,160,18),displayName+" · "+Mathf.CeilToInt(Health),label);GUI.Label(new Rect(p.x-80,y-36,160,18),state,label);GUI.color=previous;
  }
  void OnDisable(){if(status)status.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);}
 }
}

