using UnityEngine;
using UnityEngine.InputSystem;

namespace Nexoterra.World
{
    /// <summary>Standalone exploration rig. Uses the project's existing Input System.</summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class CavernExplorer : MonoBehaviour
    {
        public Camera view;
        public float walkSpeed = 6f;
        public float sprintSpeed = 11f;
        public float lookSensitivity = .09f;
        public Vector3 spawn;
        CharacterController controller;
        float verticalSpeed, pitch;
        bool showHelp = true;

        void Awake()
        {
            controller=GetComponent<CharacterController>();
            spawn=transform.position;
            if (!view) view=GetComponentInChildren<Camera>();
            pitch=view.transform.localEulerAngles.x;
            if(pitch>180) pitch-=360;
        }
        void Update()
        {
            var keyboard=Keyboard.current; var mouse=Mouse.current;
            if(keyboard==null || mouse==null || view==null) return;
            if(mouse.leftButton.wasPressedThisFrame && Cursor.lockState!=CursorLockMode.Locked)
            { Cursor.lockState=CursorLockMode.Locked; Cursor.visible=false; }
            if(keyboard.escapeKey.wasPressedThisFrame) ReleaseCursor();
            if(keyboard.hKey.wasPressedThisFrame) showHelp=!showHelp;
            if(keyboard.rKey.wasPressedThisFrame || transform.position.y < -8) Respawn();
            if(Cursor.lockState!=CursorLockMode.Locked) return;
            Vector2 look=mouse.delta.ReadValue()*lookSensitivity;
            transform.Rotate(0,look.x,0);
            pitch=Mathf.Clamp(pitch-look.y,-80,80);
            view.transform.localRotation=Quaternion.Euler(pitch,0,0);
            Vector2 input=new Vector2((keyboard.dKey.isPressed?1:0)-(keyboard.aKey.isPressed?1:0),
                (keyboard.wKey.isPressed?1:0)-(keyboard.sKey.isPressed?1:0));
            input=Vector2.ClampMagnitude(input,1);
            if(controller.isGrounded && verticalSpeed<0) verticalSpeed=-2;
            if(controller.isGrounded && keyboard.spaceKey.wasPressedThisFrame) verticalSpeed=5.5f;
            verticalSpeed+=Physics.gravity.y*Time.deltaTime*1.8f;
            Vector3 motion=(transform.right*input.x+transform.forward*input.y)*(keyboard.leftShiftKey.isPressed?sprintSpeed:walkSpeed);
            controller.Move((motion+Vector3.up*verticalSpeed)*Time.deltaTime);
        }
        public void Respawn()
        { controller.enabled=false; transform.position=spawn; controller.enabled=true; verticalSpeed=0; }
        void OnDisable(){ReleaseCursor();}
        void OnApplicationFocus(bool focus){if(!focus)ReleaseCursor();}
        static void ReleaseCursor(){Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}
        void OnGUI()
        {
            if(!showHelp)return;
            GUI.color=new Color(1,1,1,.9f);
            GUI.Box(new Rect(18,18,435,76),GUIContent.none);
            GUI.Label(new Rect(32,25,410,24),"NEXOTERRA  /  Cuenca del Nexo");
            GUI.Label(new Rect(32,47,410,22),"Clic: explorar   WASD: moverse   Mayús: correr   Espacio: saltar");
            GUI.Label(new Rect(32,67,410,22),"Esc: liberar ratón   R: volver al inicio   H: ocultar ayuda");
        }
    }
}
