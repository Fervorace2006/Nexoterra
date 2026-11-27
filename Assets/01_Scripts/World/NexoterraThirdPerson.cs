using UnityEngine;
using UnityEngine.InputSystem;

namespace Nexoterra.World
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class NexoterraThirdPerson : MonoBehaviour
    {
        public Camera view;
        public Transform visuals;
        public GameObject male, female;
        public Transform muzzle;
        public LineRenderer tracer;
        public Vector3 spawn;
        public float walkSpeed=4.2f, sprintSpeed=7.2f;
        public int food, adopted;
        public Transform[] travelPoints;
        CharacterController motor;
        SlugInventory inventory;
        PlayerHealth health;
        PlayerAnimationDriver animationDriver;
        PlayerAbilities abilities;
        Transform mobileAimTarget;
        readonly RaycastHit[] mobileAimHits = new RaycastHit[32];
        float yaw=-35,pitch=12,vertical,tracerUntil;
        bool help=false,map;
        string notice="Haz clic para explorar";
        float noticeUntil=6;
        int travelIndex;
        GUIStyle label,title;

        void Awake(){if(!view)view=Camera.main;abilities=GetComponent<PlayerAbilities>();health=GetComponent<PlayerHealth>();animationDriver=GetComponent<PlayerAnimationDriver>();inventory=GetComponent<SlugInventory>();motor=GetComponent<CharacterController>();motor.minMoveDistance=0;spawn=transform.position;}
        void Update()
        {
            if(GameSession.Blocked)return;
            var k=Keyboard.current;var m=Mouse.current;
            if(k!=null&&k.escapeKey.wasPressedThisFrame&&!GameSession.Current)Release();
            if(NexoInput.AnyActivity&&Cursor.lockState!=CursorLockMode.Locked&&!map&&!(inventory&&inventory.IsOpen)&&!(GetComponent<PlayerEmotes>()&&GetComponent<PlayerEmotes>().IsOpen)){Cursor.lockState=CursorLockMode.Locked;Cursor.visible=false;}
            bool inventoryOpen=inventory&&inventory.IsOpen;
            bool emoteMenu=GetComponent<PlayerEmotes>()&&GetComponent<PlayerEmotes>().IsOpen;
            bool capturing=m!=null&&m.leftButton.wasPressedThisFrame&&Cursor.lockState!=CursorLockMode.Locked&&!map&&!inventoryOpen&&!emoteMenu&&!(inventory&&inventory.PointerOverBar);
            if(capturing)
            {Cursor.lockState=CursorLockMode.Locked;Cursor.visible=false;}
            if(k!=null&&k.hKey.wasPressedThisFrame)help=!help;
            if(k!=null&&k.mKey.wasPressedThisFrame||NexoInput.Map)map=!map;
            if(inventoryOpen)map=false;
            if(k!=null&&k.vKey.wasPressedThisFrame){bool chooseFemale=!female.activeSelf;female.SetActive(chooseFemale);male.SetActive(!chooseFemale);Notify(chooseFemale?"Personaje: Comando":"Personaje: Jugador");}
            if(k!=null&&k.homeKey.wasPressedThisFrame||transform.position.y < -6)Teleport(spawn);
            if(!CampaignLevel.Current&&k!=null&&k.tKey.wasPressedThisFrame&&travelPoints.Length>0){travelIndex=(travelIndex+1)%travelPoints.Length;Teleport(travelPoints[travelIndex].position);Notify("Visita de prueba: "+travelPoints[travelIndex].name);}
            Vector2 touchLook=MobileTouchControls.IsEnabled?MobileTouchControls.LookDelta:Vector2.zero;
            bool touchLookMoved=touchLook.sqrMagnitude>.1f;
            bool otherCameraInput=NexoInput.Look.sqrMagnitude>.01f||m!=null&&m.delta.ReadValue().sqrMagnitude>1f;
            if(!MobileTouchControls.IsEnabled||map||inventoryOpen||emoteMenu||otherCameraInput)mobileAimTarget=null;
            else if(touchLookMoved)mobileAimTarget=null;
            Vector2 input=Vector2.zero;
            if((Cursor.lockState==CursorLockMode.Locked||MobileTouchControls.IsEnabled)&&!map&&!inventoryOpen&&!emoteMenu)
            {
                Vector2 mouse=m!=null?m.delta.ReadValue()*.09f:Vector2.zero;
                if(MobileTouchControls.IsEnabled)mouse+=touchLook*.12f;
                Vector2 stick=NexoInput.Look*Time.deltaTime*(NexoInput.Aim?110:200);mouse+=new Vector2(stick.x,stick.y*.7f);yaw+=mouse.x;pitch=Mathf.Clamp(pitch-mouse.y,-30,65);
                if(MobileTouchControls.IsEnabled)
                {
                    if(touchLookMoved)mobileAimTarget=AcquireMobileAimTarget();
                    else if(mobileAimTarget)TrackMobileAimTarget();
                }
                if(k!=null)input=new Vector2((k.dKey.isPressed?1:0)-(k.aKey.isPressed?1:0),(k.wKey.isPressed?1:0)-(k.sKey.isPressed?1:0));
                Vector2 padMove=NexoInput.Move;if(padMove!=Vector2.zero)input=padMove;
                if(abilities&&NexoInput.Dodge){int side=Mathf.Abs(input.x)>.3f?(input.x>0?1:-1):1;abilities.TryDodge(side,Quaternion.Euler(0,yaw,0)*Vector3.right);}
                if(abilities&&k!=null&&(k.leftCtrlKey.isPressed||k.rightCtrlKey.isPressed)){
                    bool ctrl=k.leftCtrlKey.wasPressedThisFrame||k.rightCtrlKey.wasPressedThisFrame;
                    if(k.aKey.wasPressedThisFrame||(ctrl&&k.aKey.isPressed))abilities.TryDodge(-1,Quaternion.Euler(0,yaw,0)*Vector3.right);
                    else if(k.dKey.wasPressedThisFrame||(ctrl&&k.dKey.isPressed))abilities.TryDodge(1,Quaternion.Euler(0,yaw,0)*Vector3.right);
                }
                if(k!=null&&k.eKey.wasPressedThisFrame)Collect();
                if(k!=null&&k.fKey.wasPressedThisFrame){if(!CampaignLevel.Current||!CampaignLevel.Current.TryReturnElemental())Feed();}
                if(NexoInput.Interact){bool returned=CampaignLevel.Current&&CampaignLevel.Current.TryReturnElemental();if(!returned){Collect();Feed();}}
                if((k!=null&&k.rKey.wasPressedThisFrame||NexoInput.Reload)&&animationDriver)animationDriver.Reload();
                if((m!=null&&m.leftButton.wasPressedThisFrame&&!capturing)||NexoInput.Shoot)Shoot();
            }
            if(health&&!health.CanMove)input=Vector2.zero;
            Vector3 movement=Quaternion.Euler(0,yaw,0)*Vector3.ClampMagnitude(new Vector3(input.x,0,input.y),1);
            if(motor.isGrounded&&vertical<0)vertical=-2;
            // Jumping is disabled in Nexoterra (Space does nothing).
            // Snappy arc: lighter going up, heavier coming down (no floaty hang time).
            vertical+=Physics.gravity.y*(vertical>0?2.2f:2.9f)*Time.deltaTime;
            float moveSpeed=(k!=null&&k.leftShiftKey.isPressed||NexoInput.Sprint?sprintSpeed:walkSpeed)*(abilities?abilities.MovementMultiplier:1);
            if(abilities&&abilities.IsDodging){movement=abilities.DodgeDirection;moveSpeed=abilities.DodgeSpeed;}
            if(health&&!health.CanMove)movement=Vector3.zero;
            motor.Move((movement*moveSpeed+Vector3.up*vertical)*Time.deltaTime);
            if(movement.sqrMagnitude>.01f||(((m!=null&&m.rightButton.isPressed)||NexoInput.Aim||mobileAimTarget)&&!inventoryOpen))
            {
                float facing=((m!=null&&m.rightButton.isPressed)||NexoInput.Aim||mobileAimTarget||(abilities&&abilities.IsDodging))?yaw:Mathf.Atan2(movement.x,movement.z)*Mathf.Rad2Deg;
                visuals.rotation=Quaternion.Slerp(visuals.rotation,Quaternion.Euler(0,facing,0),1-Mathf.Exp(-15*Time.deltaTime));
            }
            if(tracer)tracer.enabled=Time.time<tracerUntil;
        }
        void LateUpdate()
        {
            if(!view)return;
            bool aim=(Mouse.current!=null&&Mouse.current.rightButton.isPressed||NexoInput.Aim)&&(Cursor.lockState==CursorLockMode.Locked||MobileTouchControls.IsEnabled);
            Quaternion rotation=Quaternion.Euler(pitch,yaw,0);
            Vector3 pivot=transform.position+Vector3.up*1.5f;
            Vector3 offset=rotation*new Vector3(aim?.55f:.75f,.25f,aim?-2.2f:-4.1f);
            Vector3 target=pivot+offset;
            if(Physics.SphereCast(pivot,.18f,offset.normalized,out RaycastHit hit,offset.magnitude,~(1<<2),QueryTriggerInteraction.Ignore))target=pivot+offset.normalized*Mathf.Max(.3f,hit.distance-.12f);
            view.transform.position=target;view.transform.rotation=rotation;
            view.fieldOfView=Mathf.Lerp(view.fieldOfView,aim?48:68,1-Mathf.Exp(-12*Time.deltaTime));
        }
        Transform AcquireMobileAimTarget()
        {
            if(!view)return null;
            Quaternion rotation=Quaternion.Euler(pitch,yaw,0);
            Vector3 direction=rotation*Vector3.forward;
            int count=Physics.SphereCastNonAlloc(view.transform.position,.65f,direction,mobileAimHits,55f,~(1<<2),QueryTriggerInteraction.Ignore);
            Collider nearest=null;float nearestDistance=float.PositiveInfinity;
            for(int i=0;i<count;i++)
            {
                var hit=mobileAimHits[i];var collider=hit.collider;
                if(!collider||collider.transform.IsChildOf(transform)||hit.distance>=nearestDistance)continue;
                nearest=collider;nearestDistance=hit.distance;
            }
            if(!nearest)return null;
            var enemy=nearest.GetComponentInParent<EnemyCombatant>();
            if(enemy&&!enemy.IsDead)
            {
                var animation=enemy.GetComponent<NativeEnemyAnimation>();
                return animation&&animation.IsDodging?null:enemy.transform;
            }
            var rival=nearest.GetComponentInParent<ArenaTarget>();
            if(rival&&ArenaSession.Current&&rival.clientId!=ArenaSession.Current.LocalClientId&&!ArenaSession.Current.IsRivalEvading(rival.clientId))return rival.transform;
            return null;
        }
        void TrackMobileAimTarget()
        {
            if(!view||!IsValidMobileAimTarget(mobileAimTarget)){mobileAimTarget=null;return;}
            Vector3 direction=(MobileAimPoint(mobileAimTarget)-view.transform.position).normalized;
            if(direction.sqrMagnitude<.01f)return;
            float targetYaw=Mathf.Atan2(direction.x,direction.z)*Mathf.Rad2Deg;
            float targetPitch=Mathf.Clamp(-Mathf.Asin(Mathf.Clamp(direction.y,-1f,1f))*Mathf.Rad2Deg,-30f,65f);
            float blend=1-Mathf.Exp(-10f*Time.deltaTime);
            yaw=Mathf.LerpAngle(yaw,targetYaw,blend);pitch=Mathf.Lerp(pitch,targetPitch,blend);
        }
        static bool IsValidMobileAimTarget(Transform target)
        {
            if(!target)return false;
            var enemy=target.GetComponentInParent<EnemyCombatant>();
            if(enemy){var animation=enemy.GetComponent<NativeEnemyAnimation>();return !enemy.IsDead&&(!animation||!animation.IsDodging);}
            var rival=target.GetComponentInParent<ArenaTarget>();
            return rival&&ArenaSession.Current&&rival.clientId!=ArenaSession.Current.LocalClientId&&!ArenaSession.Current.IsRivalEvading(rival.clientId);
        }
        static Vector3 MobileAimPoint(Transform target)
        {
            var enemy=target.GetComponentInParent<EnemyCombatant>();
            var body=enemy?enemy.GetComponent<CharacterController>():target.GetComponentInParent<CharacterController>();
            return body?body.bounds.center+Vector3.up*.2f:target.position+Vector3.up*1.1f;
        }
        public void Teleport(Vector3 p){motor.enabled=false;transform.position=p;motor.enabled=true;vertical=0;}
        void Shoot()
        {
            if((abilities&&abilities.IsDodging)||!inventory||!muzzle||!view||(health&&health.IsDead)||(animationDriver&&animationDriver.IsReloading))return;
            var controls=GetComponent<EnemyControlEffects>();if(controls&&!controls.CanShoot)return;
            var nativeAnimation=GetComponent<NativePlayerAnimation>();
            if(!nativeAnimation||!nativeAnimation.IsLoaded){Notify(MobileTouchControls.IsEnabled?"Espera a que se recargue la babosa":"Pulsa R para recargar antes de disparar");return;}
            // Keep the body upright while the projectile follows the crosshair in 3D.
            Vector3 facing=Quaternion.Euler(0,yaw,0)*Vector3.forward;
            visuals.rotation=Quaternion.LookRotation(facing);
            LateUpdate();
            Ray aim=view.ViewportPointToRay(new Vector3(.5f,.5f,0));
            Vector3 target=IsValidMobileAimTarget(mobileAimTarget)?MobileAimPoint(mobileAimTarget):Physics.Raycast(aim,out var hit,100,SlugHop.WorldMask,QueryTriggerInteraction.Collide)?hit.point:aim.GetPoint(100);
            Vector3 direction=(target-muzzle.position).normalized;
            if(direction.sqrMagnitude<.001f)direction=aim.direction;
            if(!inventory.Fire(muzzle,direction))Notify(inventory.Status);else if(animationDriver)animationDriver.Shot();
        }

        void Collect()
        {
            MushroomFood closest=null;float best=3;
            foreach(var mushroom in MushroomFood.Available)
            {if(!mushroom||!mushroom.isActiveAndEnabled)continue;float d=Vector3.Distance(transform.position,mushroom.transform.position);if(d<best){best=d;closest=mushroom;}}
            if(closest){food++;closest.Collect();Notify("Hongo recogido · comida +1");}else Notify("Acércate a un hongo pequeño para recogerlo");
        }
        void Feed()
        {
            WildSlug closest=null;float best=3;
            foreach(var slug in WildSlug.Active)
            {if(!slug||slug.adopted)continue;float d=Vector3.Distance(transform.position,slug.transform.position);if(d<best){best=d;closest=slug;}}
            if(!closest){Notify("Acércate a una babosa para alimentarla");return;}
            if(CampaignLevel.Current&&CampaignLevel.Current.IsReward(closest)&&closest.species.StartsWith("Malvada_")){Notify("Primero cura la elemental con Doc o Energía.");return;}
            if(food==0){Notify("Necesitas un hongo · recógelo con E");return;}
            if(!inventory||!inventory.Add(closest.species,closest.golden)){Notify("No se pudo registrar esta especie.");return;}
            food--;adopted=inventory.Count;var loot=closest.GetComponent<EnemySlugLoot>();if(loot&&CampaignLevel.Current)CampaignLevel.Current.CollectLoot(loot.key);closest.Adopt();if(CampaignLevel.Current)GameSession.RememberSpecies(closest.species,closest.golden);if(GameSession.Current)GameSession.Current.SaveInventory();Notify(inventory.Status);
        }
        public void Notify(string text){notice=text;noticeUntil=Time.time+4;}
        void OnDisable(){Release();}
        void OnApplicationFocus(bool focus){if(!focus)Release();}
        static void Release(){Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}
        void OnGUI()
        {
            if(GameSession.Blocked||inventory&&inventory.IsOpen)return;
            if(label==null){label=new GUIStyle(GUI.skin.label){fontSize=15};label.normal.textColor=Color.white;title=new GUIStyle(label){fontSize=19,fontStyle=FontStyle.Bold};}
            GUI.color=Color.white;
            float cx=Screen.width*.5f,cy=Screen.height*.5f;
            GUI.DrawTexture(new Rect(cx-8,cy-1,5,2),Texture2D.whiteTexture);GUI.DrawTexture(new Rect(cx+3,cy-1,5,2),Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx-1,cy-8,2,5),Texture2D.whiteTexture);GUI.DrawTexture(new Rect(cx-1,cy+3,2,5),Texture2D.whiteTexture);
            if(help)
            {
                GUI.Box(new Rect(18,18,560,112),GUIContent.none);
                GUI.Label(new Rect(32,25,540,28),"NEXOTERRA / "+BiomeAt(transform.position),title);
                GUI.Label(new Rect(32,54,540,24),"WASD · mover   Mayús · correr   Ratón · cámara",label);
                GUI.Label(new Rect(32,78,540,24),"Clic der. · apuntar   Clic izq. · lanzar babosa   V · personaje",label);
                GUI.Label(new Rect(32,101,540,24),"E · recoger   F · alimentar   Alt · mochila   B · gestos   T · visitar zona",label);
            }

            if(Time.time<noticeUntil)GUI.Label(new Rect(cx-260,Screen.height-195,590,30),notice,label);
            if(map)DrawMap();
        }
        void DrawMap()
        {
            if(ArenaSession.Current){DrawArenaMap();return;}
            float x=Screen.width*.5f-240,y=Screen.height*.5f-240;
            GUI.Box(new Rect(x-16,y-45,512,545),GUIContent.none);GUI.Label(new Rect(x,y-35,480,30),"CUATRO NODOS / seis biomas · siete episodios · M para cerrar",title);
            Color[] colors={new Color(.4f,.29f,.17f),new Color(.5f,.13f,.07f),new Color(.35f,.63f,.75f),new Color(.05f,.4f,.48f),new Color(.24f,.06f,.09f),new Color(.2f,.45f,.2f)};
            string[] names={"TIERRA","VOLCÁN","HIELO","ACUÁTICA","FINAL CARMESÍ","INICIO VERDE"};
            for(int i=0;i<6;i++){int row=i/2;float top=row==0?0:row==1?180:300;float height=row==1?120:180;var rect=new Rect(x+(i%2)*240,y+top,238,height-2);GUI.color=colors[i];GUI.DrawTexture(rect,Texture2D.whiteTexture);GUI.color=Color.white;GUI.Label(new Rect(rect.x+12,rect.y+12,220,30),names[i],label);}
            float px=x+Mathf.InverseLerp(-320,320,transform.position.x)*480,py=y+(1-Mathf.InverseLerp(-320,320,transform.position.z))*480;
            GUI.color=Color.yellow;GUI.DrawTexture(new Rect(px-4,py-4,8,8),Texture2D.whiteTexture);GUI.color=Color.white;
        }
        void DrawArenaMap()
        {
            float x=Screen.width*.5f-170,y=Screen.height*.5f-190,size=340;
            GUI.Box(new Rect(x-14,y-40,size+28,size+54),GUIContent.none);GUI.Label(new Rect(x,y-32,size,24),"ARENA · RIVALES",title);
            GUI.color=new Color(.08f,.13f,.16f,.96f);GUI.DrawTexture(new Rect(x,y,size,size),Texture2D.whiteTexture);GUI.color=Color.white;
            float px=x+Mathf.InverseLerp(-125,125,transform.position.x)*size,py=y+(1-Mathf.InverseLerp(-125,125,transform.position.z))*size;
            GUI.color=Color.cyan;GUI.DrawTexture(new Rect(px-5,py-5,10,10),Texture2D.whiteTexture);
            var session=ArenaSession.Current;
            if(session)foreach(var rival in session.RivalPositions()){var pos=rival.Value;float rx=x+Mathf.InverseLerp(-125,125,pos.x)*size,ry=y+(1-Mathf.InverseLerp(-125,125,pos.z))*size;GUI.color=new Color(1,.2f,.18f);GUI.DrawTexture(new Rect(rx-4,ry-4,8,8),Texture2D.whiteTexture);}
            GUI.color=Color.white;
        }
        public static string BiomeAt(Vector3 p){if(CampaignLevel.Current)return CampaignLevel.Titles[CampaignLevel.Current.index];if(ArenaSession.Current)return "Arena multijugador";if(p.x>=0)return p.z>80?"Caldera volcánica":p.z>-80?"Galería acuática":"Jardines del Nexo";return p.z>80?"Tierras antiguas":p.z>-80?"Cavernas de hielo":"Ciénaga carmesí";}
    }
}



