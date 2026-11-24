using UnityEngine;
using UnityEngine.InputSystem;

namespace Nexoterra.World
{
    [DefaultExecutionOrder(50)]
    public sealed class PlayerEmotes:MonoBehaviour
    {
        public bool IsDancing {get;private set;}
        public bool IsOpen {get;private set;}
        Texture2D disk;
        bool restoreCursor;
        SlugInventory inventory;NativePlayerAnimation animationDriver;CharacterController motor;
        void Awake(){inventory=GetComponent<SlugInventory>();animationDriver=GetComponent<NativePlayerAnimation>();motor=GetComponent<CharacterController>();}
        public void Cancel(){IsDancing=false;Close();}
        void Close(){if(!IsOpen)return;IsOpen=false;if(restoreCursor&&!GameSession.Blocked&&!(inventory&&inventory.IsOpen)){Cursor.lockState=CursorLockMode.Locked;Cursor.visible=false;}}
        public bool BeginDance()
        {
            if(GameSession.Blocked||!animationDriver||!animationDriver.HasDance||!motor||!motor.isGrounded||animationDriver.IsReloading)return false;
            var health=GetComponent<PlayerHealth>();if(health&&!health.CanMove)return false;
            var abilities=GetComponent<PlayerAbilities>();if(abilities&&abilities.IsDodging)return false;
            IsDancing=true;Close();return true;
        }
        void Update()
        {
            var k=Keyboard.current;var mouse=Mouse.current;if(k==null)return;
            bool move=k.wKey.isPressed||k.aKey.isPressed||k.sKey.isPressed||k.dKey.isPressed||k.spaceKey.isPressed;
            bool action=k.rKey.wasPressedThisFrame||k.vKey.wasPressedThisFrame||k.mKey.wasPressedThisFrame||k.tKey.wasPressedThisFrame||k.escapeKey.wasPressedThisFrame||(mouse!=null&&(mouse.rightButton.isPressed||(!IsOpen&&mouse.leftButton.wasPressedThisFrame)));
            if(GameSession.Blocked||(inventory&&inventory.IsOpen)||move||action){Cancel();return;}
            if(k.bKey.wasPressedThisFrame){
                if(IsOpen){Close();return;}
                if(IsDancing){IsDancing=false;return;}
                restoreCursor=Cursor.lockState==CursorLockMode.Locked;IsOpen=true;Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
            }
        }
        void OnDisable(){Cancel();}
        void OnDestroy(){if(disk)Destroy(disk);}
        void OnGUI()
        {
            if(!IsOpen)return;
            if(!disk){disk=new Texture2D(128,128,TextureFormat.RGBA32,false);for(int y=0;y<128;y++)for(int x=0;x<128;x++){float d=Vector2.Distance(new Vector2(x+.5f,y+.5f),new Vector2(64,64));disk.SetPixel(x,y,new Color(1,1,1,Mathf.Clamp01(64-d)));}disk.Apply();}
            float scale=Mathf.Min(Screen.width/1280f,Screen.height/720f);var previous=GUI.matrix;GUI.matrix=Matrix4x4.TRS(new Vector3((Screen.width-1280*scale)*.5f,(Screen.height-720*scale)*.5f,0),Quaternion.identity,Vector3.one*scale);
            Color before=GUI.color;GUI.color=new Color(.015f,.04f,.055f,.96f);GUI.DrawTexture(new Rect(450,170,380,380),disk);GUI.color=Color.white;
            var title=new GUIStyle(GUI.skin.label){fontSize=22,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleCenter};title.normal.textColor=Color.white;
            GUI.Label(new Rect(510,310,260,45),"GESTOS",title);
            var label=new GUIStyle(title){fontSize=12,fontStyle=FontStyle.Normal};label.normal.textColor=new Color(.55f,.8f,.85f);
            GUI.Label(new Rect(510,350,260,30),"B / Esc · cerrar",label);
            for(int i=0;i<6;i++){
                float angle=(-90+i*60)*Mathf.Deg2Rad;var r=new Rect(640+Mathf.Cos(angle)*135-54,360+Mathf.Sin(angle)*135-30,108,60);
                if(i==0){if(GUI.Button(r,"BAILE 1")){if(!BeginDance())GetComponent<NexoterraThirdPerson>()?.Notify("Ponte de pie y quieto para bailar");}}
                else {GUI.color=new Color(.45f,.55f,.6f,.5f);GUI.Label(r,"—",title);GUI.color=Color.white;}
            }
            GUI.Label(new Rect(410,565,460,28),"Muévete para cancelar el baile",label);GUI.color=before;GUI.matrix=previous;
        }
    }
}
