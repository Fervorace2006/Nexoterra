using UnityEngine;

namespace Nexoterra.World
{
    public sealed partial class SlugProjectile : MonoBehaviour
    {
        public float docHealingRadius=6;
        bool healedArea;
        bool CanPurify => definition!=null && (SlugCatalog.BaseSpecies(definition.id)=="Gon_Doc" || SlugCatalog.BaseSpecies(definition.id)=="Elemental_Energia");
        public float normalHitRadius=.16f,transformedHitRadius=.42f;
        Transform shooter;
        readonly RaycastHit[] flightHits=new RaycastHit[64];
        readonly Collider[] flightOverlaps=new Collider[64];
        public float speed=24, transformDistance=8, maxDistance=75;
        public float returnStride=2.8f,returnHopDuration=.23f,returnHopHeight=.65f;
        public bool IsTransformed { get; private set; }
        public bool IsReturning { get; private set; }
        public bool arenaShot,arenaAuthority;
        public ulong arenaOwner;
        public bool HasArrived { get; private set; }
        public int EmergencyRecalls {get;private set;}
        public float Travelled { get; private set; }
        public Vector3 Direction { get; private set; }
        public float ReturnProgress=>HasArrived?1:!IsReturning||!ReturnTarget?0:Mathf.Clamp01(1-Vector3.Distance(transform.position,ReturnTarget.position)/Mathf.Max(1,returnDistance));
        SlugReturnRoute route;
        void Awake(){route=new SlugReturnRoute();}
        Vector3 lastProgress;
        SlugDefinition definition;
        SlugInventory owner;
        EnemyShooter enemyOwner;int enemySlot=-1;
        Transform ReturnTarget=>owner?owner.transform:enemyOwner&&!enemyOwner.IsDead?enemyOwner.transform:null;
        GameObject model;
        SlugFlightEffects effects;
        bool hostile;public bool Hostile=>hostile;
        Vector3 modelBase,modelScale,hopStart,hopEnd;
        float flightTime,fallSpeed,hopTime,pause,returnDistance;
        bool grounded,hopping;
        float returnElapsed,stalled,bestDistance=float.PositiveInfinity,hopDuration,activeHopHeight;
        public void Launch(SlugDefinition slug,Vector3 direction,SlugInventory inventory=null)
        {
            owner=inventory;if(inventory)shooter=inventory.transform;definition=slug;Direction=direction.normalized;
            if(Direction.sqrMagnitude<.5f)Direction=Vector3.forward;
            transform.rotation=Quaternion.LookRotation(Direction);Show(slug.normal,.40f);
        }
        public void LaunchEnemy(SlugDefinition slug,Vector3 direction,EnemyShooter enemy,int slot){enemyOwner=enemy;enemySlot=slot;LaunchHostile(slug,direction,enemy.transform);}
        public void LaunchHostile(SlugDefinition slug,Vector3 direction,Transform source=null){hostile=true;shooter=source;Launch(slug,direction);}
        void Show(GameObject prefab,float height)
        {
            if(model){model.SetActive(false);Destroy(model);}
            model=Instantiate(prefab,transform);model.transform.localPosition=Vector3.zero;
            model.transform.localRotation=Quaternion.identity;model.transform.localScale=Vector3.one;
            foreach(var component in model.GetComponentsInChildren<WildSlug>(true))component.enabled=false;
            foreach(var collider in model.GetComponentsInChildren<Collider>(true))collider.enabled=false;
            foreach(var body in model.GetComponentsInChildren<Rigidbody>(true)){body.isKinematic=true;body.detectCollisions=false;}
            foreach(var t in model.GetComponentsInChildren<Transform>(true))t.gameObject.layer=2;
            Quaternion rotation=transform.rotation;transform.rotation=Quaternion.identity;
            var renderers=model.GetComponentsInChildren<Renderer>();
            if(renderers.Length>0)
            {
                Bounds bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
                float extent=IsTransformed?Mathf.Max(bounds.size.x,bounds.size.y,bounds.size.z):bounds.size.y;
                float scale=height*.7f/Mathf.Max(.01f,extent);
                Vector3 center=bounds.center-transform.position;
                model.transform.localScale=Vector3.one*scale;model.transform.localPosition=-center*scale;
            }
            if(IsTransformed)ElementalAura.Create(model.transform,definition.id,.72f/Mathf.Max(.0001f,model.transform.localScale.x),-model.transform.localPosition/Mathf.Max(.0001f,model.transform.localScale.x));
            transform.rotation=rotation;modelBase=model.transform.localPosition;modelScale=model.transform.localScale;
        }
        void Update(){Advance(Time.deltaTime);}
        public void Advance(float seconds)
        {
            if(definition==null||HasArrived)return;
            // Substeps keep collision checks and jump arcs stable on slow frames.
            float remaining=Mathf.Max(0,seconds);
            while(remaining>0){float dt=Mathf.Min(.025f,remaining);remaining-=dt;if(IsReturning)Return(dt);else Fly(dt);if(HasArrived)break;}
        }
        // Homing: when launched at a targeted enemy the slug bends toward it for 1.5 s, then keeps flying straight.
        // A roll (dodge) breaks the lock, so evading is a real escape.
        public Transform HomingTarget;float homingLeft=1.5f;
        void Steer(float dt)
        {
            if(!HomingTarget||homingLeft<=0||hostile)return;
            homingLeft-=dt;
            var enemy=HomingTarget.GetComponentInParent<EnemyCombatant>();var anim=HomingTarget.GetComponentInParent<NativeEnemyAnimation>();
            if((enemy&&enemy.IsDead)||(anim&&anim.IsDodging)){HomingTarget=null;return;}
            var cc=HomingTarget.GetComponentInParent<CharacterController>();
            Vector3 aim=cc?cc.bounds.center+Vector3.up*.2f:HomingTarget.position+Vector3.up*1.1f;
            Vector3 want=(aim-transform.position).normalized;
            if(Vector3.Dot(want,Direction)<-.1f){HomingTarget=null;return;}          // already passed it
            Direction=Vector3.RotateTowards(Direction,want,Mathf.Deg2Rad*260f*dt,0).normalized;
            transform.rotation=Quaternion.LookRotation(Direction);
        }
        void Fly(float dt)
        {
            Steer(dt);
            flightTime+=dt;float remaining=Mathf.Min(speed*dt,Mathf.Max(0,maxDistance-Travelled));
            while(remaining>.00001f)
            {
                float step=remaining;if(!IsTransformed)step=Mathf.Min(step,Mathf.Max(.00001f,transformDistance-Travelled));
                bool collided=Sweep(step,out var hit);
                if(IsReturning)return;
                CheckProjectileContacts(collided?Mathf.Min(step,hit.distance):step);
                if(IsReturning)return;
                if(collided)
                {transform.position=hit.point+hit.normal*.23f;if(IceWall.TryHit(hit.collider)){if(effects){effects.End(true,hit.point);effects=null;}BeginReturn();return;}IceGround(hit);ArenaHit(hit.collider);DemoledoraImpact(hit.point);HealArea(hit.point);Purify(hit.collider);var enemy=hit.collider.GetComponentInParent<EnemyCombatant>();if(enemy&&!hostile)enemy.ReceiveSlug(definition,Direction,IsTransformed);var player=hit.collider.GetComponentInParent<PlayerHealth>();if(player&&hostile)player.ReceiveSlug(definition,IsTransformed,Direction);var prop=hit.collider.GetComponentInParent<WorldDestructible>();if(prop)prop.Hit(definition.id,IsTransformed);if(effects){effects.End(true,hit.point);effects=null;}BeginReturn();return;}
                if(IsReturning)return;transform.position+=Direction*step;Travelled+=step;remaining-=step;
                if(!IsTransformed&&Travelled>=transformDistance-.0001f){IsTransformed=true;TransformSound();Show(definition.transformed,1.8f);effects=SlugFlightEffects.Create(transform,definition.id);effects.transform.localScale=Vector3.one*.7f;effects.transform.localPosition*=.7f;}
            }
            if(Travelled>=maxDistance-.001f||flightTime>=8)BeginReturn();
        }
        // The player's ice slug hitting the ground raises a protective ice wall.
        void IceGround(RaycastHit hit)
        {
            if(hostile||definition==null||SlugCatalog.BaseSpecies(definition.id)!="ice"||hit.normal.y<.6f)return;
            if(hit.collider.GetComponentInParent<EnemyCombatant>()||hit.collider.GetComponentInParent<PlayerHealth>())return;
            IceWall.Raise(hit.point,Direction,definition.id.StartsWith("Malvada_"));
        }
        // The launch sound plays only once the slug has actually changed to its T form.
        void TransformSound()
        {
            string clip=definition!=null&&definition.id.StartsWith("Malvada_")?"slug_launch_malvada":"slug_launch";
            if(hostile)NexoAudio.PlayAt(clip,transform.position,.85f);else NexoAudio.Play(clip,.9f,.04f);
        }
        void HealArea(Vector3 point){if(CanCorrupt){CorruptArea(point);return;}if(healedArea||!CanPurify)return;healedArea=true;var inventory=owner?owner:FindAnyObjectByType<SlugInventory>();HealingRadiusVisual.Burst(point,docHealingRadius);foreach(var wild in FindObjectsByType<WildSlug>(FindObjectsSortMode.None))if(!wild.adopted&&Vector3.Distance(wild.transform.position,point)<=docHealingRadius)PurifyWild(wild,inventory);foreach(var loose in FindObjectsByType<LooseSlug>(FindObjectsSortMode.None))if(Vector3.Distance(loose.transform.position,point)<=docHealingRadius)loose.Purify();}
        void Purify(Collider collider){if(CanCorrupt){CorruptTarget(collider);return;}if(!CanPurify)return;var loose=collider.GetComponentInParent<LooseSlug>();if(loose)loose.Purify();PurifyWild(collider.GetComponentInParent<WildSlug>(),owner?owner:FindAnyObjectByType<SlugInventory>());}
        static void PurifyWild(WildSlug wild,SlugInventory inventory){if(!wild||!inventory||!wild.species.StartsWith("Malvada_"))return;var clean=inventory.catalog.Find(SlugCatalog.BaseSpecies(wild.species));if(clean==null||wild.GetComponent<SlugPurification>()||wild.GetComponent<WildSlugCorruption>())return;
SlugPurification.Begin(wild.gameObject,()=>ApplyPurifiedWild(wild,clean));}
        static void ApplyPurifiedWild(WildSlug wild,SlugDefinition clean){if(!wild)return;wild.species=clean.id;foreach(var r in wild.GetComponentsInChildren<Renderer>())if(!r.GetComponentInParent<ParticleSystem>())r.enabled=false;var visual=Instantiate(clean.normal,wild.transform);foreach(var c in visual.GetComponentsInChildren<Collider>())c.enabled=false;var rs=visual.GetComponentsInChildren<Renderer>();if(rs.Length==0)return;var b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);float scale=.35f/Mathf.Max(.01f,b.size.y);Vector3 center=b.center-wild.transform.position;visual.transform.localScale*=scale;visual.transform.localPosition=-center*scale+Vector3.up*.175f;}
        void ArenaHit(Collider c){if(!arenaShot||!arenaAuthority||!ArenaSession.Current)return;var target=c.GetComponentInParent<ArenaTarget>();if(target)ArenaSession.Current.Hit(target.clientId,definition,IsTransformed,Direction);}
        bool Eligible(Collider c)
        {
            var arena=c?c.GetComponentInParent<ArenaTarget>():null;
            if(arenaShot&&arena)return arena.clientId!=arenaOwner;
            if(!c||c.transform.IsChildOf(transform)||(shooter&&c.transform.IsChildOf(shooter)))return false;
            if((CanPurify||CanCorrupt)&&(c.GetComponentInParent<LooseSlug>()||c.GetComponentInParent<WildSlug>()))return true;
            var enemy=c.GetComponentInParent<EnemyCombatant>();var player=c.GetComponentInParent<PlayerHealth>();
            if(enemy)return !hostile&&!enemy.IsDead;
            if(player)return hostile&&!player.IsDead;
            return c.gameObject.layer!=2&&!c.isTrigger;
        }
        bool Sweep(float distance,out RaycastHit nearest)
        {
            float radius=IsTransformed?transformedHitRadius:normalHitRadius;nearest=default;float best=float.PositiveInfinity;
            int count=Physics.SphereCastNonAlloc(transform.position,radius,Direction,flightHits,distance,(hostile||CanPurify||CanCorrupt||arenaShot)?~0:SlugHop.WorldMask,QueryTriggerInteraction.Collide);
            // Dense geometry must not silently discard a nearer wall or target.
            var hits=count==flightHits.Length?Physics.SphereCastAll(transform.position,radius,Direction,distance,(hostile||CanPurify||CanCorrupt||arenaShot)?~0:SlugHop.WorldMask,QueryTriggerInteraction.Collide):flightHits;
            if(hits!=flightHits)count=hits.Length;
            for(int i=0;i<count;i++)if(Eligible(hits[i].collider)&&hits[i].distance<best){nearest=hits[i];best=nearest.distance;}
            // Sphere casts do not report a collider already overlapping the launch point.
            int overlapCount=Physics.OverlapSphereNonAlloc(transform.position,radius,flightOverlaps,(hostile||CanPurify||CanCorrupt||arenaShot)?~0:SlugHop.WorldMask,QueryTriggerInteraction.Collide);
            var overlaps=overlapCount==flightOverlaps.Length?Physics.OverlapSphere(transform.position,radius,(hostile||CanPurify||CanCorrupt||arenaShot)?~0:SlugHop.WorldMask,QueryTriggerInteraction.Collide):flightOverlaps;
            if(overlaps!=flightOverlaps)overlapCount=overlaps.Length;
            for(int i=0;i<overlapCount;i++)if(Eligible(overlaps[i]))
            {
                var c=overlaps[i];if(IceWall.TryHit(c)){if(effects){effects.End(true,transform.position);effects=null;}BeginReturn();return false;}ArenaHit(c);DemoledoraImpact(transform.position);HealArea(transform.position);Purify(c);var enemy=c.GetComponentInParent<EnemyCombatant>();var player=c.GetComponentInParent<PlayerHealth>();
                if(enemy&&!hostile)enemy.ReceiveSlug(definition,Direction,IsTransformed);
                if(player&&hostile)player.ReceiveSlug(definition,IsTransformed,Direction);
                var prop=c.GetComponentInParent<WorldDestructible>();if(prop)prop.Hit(definition.id,IsTransformed);
                if(effects){effects.End(true,c.ClosestPoint(transform.position));effects=null;}
                BeginReturn();return false;
            }
            return best<float.PositiveInfinity;
        }
        public void BeginReturn()
        {
            if(IsReturning||HasArrived)return;
            IsReturning=true;IsTransformed=false;
            if(effects){effects.End(false);effects=null;}
            transform.rotation=Quaternion.Euler(0,transform.eulerAngles.y,0);Show(definition.normal,.40f);
            returnDistance=ReturnTarget?Vector3.Distance(transform.position,ReturnTarget.position):1;
            fallSpeed=0;grounded=false;lastProgress=transform.position;
        }
        void Return(float dt)
        {
            if(!ReturnTarget){Destroy(gameObject);return;}
            var targetMotor=ReturnTarget.GetComponent<CharacterController>();
            float targetSpeed=targetMotor?Vector3.ProjectOnPlane(targetMotor.velocity,Vector3.up).magnitude:0;
            Vector3 goal=ReturnTarget.position+Vector3.up*.22f;
            if(enemyOwner){Vector3 approach=Vector3.ProjectOnPlane(transform.position-ReturnTarget.position,Vector3.up);var body=enemyOwner.GetComponent<CharacterController>();goal+=approach.normalized*((body?body.radius:.4f)+.25f);}
            float distance=Vector3.Distance(transform.position,goal);returnElapsed+=dt;
            Vector3 waypoint=route.Next(transform.position,goal);
            if(Vector3.Distance(transform.position,lastProgress)>.25f){lastProgress=transform.position;stalled=0;}else stalled+=dt;
            // A blocked route, a teleport, or a fall under terrain cannot strand a collected creature.
            if((stalled>8)||transform.position.y< -25||distance>500)
            {
                if(TryRecall(goal)){grounded=true;hopping=false;fallSpeed=0;pause=0;stalled=0;returnElapsed=0;bestDistance=float.PositiveInfinity;}
            }
            if(stunRemaining<=0&&Vector3.Distance(transform.position,goal)<Mathf.Clamp(.85f+targetSpeed*.06f,.85f,1.5f))
            {
                // Never recover through a wall just because the player is nearby.
                if(!SlugHop.Blocked(transform.position,goal-transform.position,.035f,ReturnTarget))
                {HasArrived=true;if(owner)owner.Recover(definition.id,this);if(enemyOwner)enemyOwner.RecoverSlug(enemySlot,this);Destroy(gameObject);return;}
            }
            if(!grounded)
            {
                fallSpeed+=20*dt;float step=fallSpeed*dt;
                if(Physics.SphereCast(transform.position,.18f,Vector3.down,out var ground,step+.06f,SlugHop.WorldMask,QueryTriggerInteraction.Ignore)&&ground.normal.y>.5f)
                {transform.position=ground.point+Vector3.up*.22f;grounded=true;fallSpeed=0;}
                else transform.position+=Vector3.down*step;
                return;
            }
            if(stunRemaining>0){stunRemaining=Mathf.Max(0,stunRemaining-dt);return;}
            if(!hopping)
            {
                pause-=dt;if(pause>0)return;
                float stride=Mathf.Min(returnStride,Vector3.Distance(transform.position,waypoint));
                if(!SlugHop.Plan(transform.position,waypoint,stride,returnHopHeight,.22f,out hopEnd,ReturnTarget)
                    &&!SlugHop.Plan(transform.position,waypoint,Mathf.Min(.75f,stride),returnHopHeight,.22f,out hopEnd,ReturnTarget)
                    &&!GroundStep(waypoint,stride,out hopEnd)){pause=.08f;return;}
                hopStart=transform.position;hopTime=0;hopping=true;
                float strideLength=Vector3.Distance(hopStart,hopEnd);
                float chaseSpeed=Mathf.Max(returnStride/Mathf.Max(.05f,returnHopDuration),targetSpeed+6);
                hopDuration=Mathf.Max(.025f,strideLength/(chaseSpeed*Mathf.Lerp(1,1.55f,Mathf.InverseLerp(18,65,distance))));
                activeHopHeight=returnHopHeight*Mathf.Clamp01(strideLength/1.5f);
                Vector3 forward=hopEnd-hopStart;forward.y=0;if(forward.sqrMagnitude>.001f)transform.rotation=Quaternion.LookRotation(forward);
            }
            hopTime+=dt;float t=Mathf.Clamp01(hopTime/Mathf.Max(.025f,hopDuration));
            Vector3 next=SlugHop.Sample(hopStart,hopEnd,t,activeHopHeight),delta=next-transform.position;
            if(SlugHop.Blocked(transform.position,delta,.075f,ReturnTarget))
            {
                // Rolling terrain: lift the arc a little instead of dropping out of the hop.
                if(!SlugHop.Blocked(transform.position+Vector3.up*.18f,delta,.075f,ReturnTarget)){next+=Vector3.up*.18f;hopStart+=Vector3.up*.18f;hopEnd+=Vector3.up*.18f;}
                else{hopping=false;grounded=false;fallSpeed=0;return;}
            }
            transform.position=next;
            if(model){float squash=1-.18f*Mathf.Abs(2*t-1);model.transform.localScale=Vector3.Scale(modelScale,new Vector3(1/squash,squash,1/squash));}
            if(t>=1){hopping=false;pause=0;if(model){model.transform.localScale=modelScale;model.transform.localPosition=modelBase;}}
        }
        // Fallback hop that simply follows the ground toward the waypoint (gentle hills, no clean arc found).
        bool GroundStep(Vector3 waypoint,float stride,out Vector3 end)
        {
            end=transform.position;Vector3 flat=Vector3.ProjectOnPlane(waypoint-transform.position,Vector3.up);if(flat.sqrMagnitude<.01f)return false;
            Vector3 p=transform.position+flat.normalized*Mathf.Min(stride,flat.magnitude);
            if(!Physics.Raycast(p+Vector3.up*2.5f,Vector3.down,out var hit,6,SlugHop.WorldMask,QueryTriggerInteraction.Ignore)||hit.normal.y<.45f)return false;
            if(Mathf.Abs(hit.point.y+.22f-transform.position.y)>1.6f)return false;
            end=hit.point+Vector3.up*.22f;return true;
        }
        bool TryRecall(Vector3 goal)
        {
            for(int i=0;i<12;i++)
            {
                float angle=i*Mathf.PI/6;Vector3 p=goal+new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*1.3f;
                if(!Physics.Raycast(p+Vector3.up*1.8f,Vector3.down,out var hit,5,SlugHop.WorldMask,QueryTriggerInteraction.Ignore)||hit.normal.y<.6f)continue;
                p=hit.point+Vector3.up*.22f;
                if(Physics.CheckSphere(p,.16f,SlugHop.WorldMask,QueryTriggerInteraction.Ignore)||Physics.Linecast(p,goal,SlugHop.WorldMask,QueryTriggerInteraction.Ignore))continue;
                transform.position=p;EmergencyRecalls++;return true;
            }
            return false;
        }
        void OnDestroy(){Flying.Remove(this);if(enemyOwner)enemyOwner.RecoverSlug(enemySlot,this);if(effects)effects.End(false);if(owner&&definition!=null)owner.ReleaseLost(definition.id,this);}
    }
}



