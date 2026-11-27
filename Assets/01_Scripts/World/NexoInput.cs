using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Controls;
namespace Nexoterra.World
{
 // PS5 (DualSense) / any gamepad layer, combined with keyboard and mouse by the callers.
 // Fixed: left stick move, right stick camera, L1/R1 cycle slugs, D-pad slot 1–4, L1+down emotes, Options pause.
 // Remappable (saved in PlayerPrefs): see Act. Defaults: R2 shoot, L2 aim, Square reload, Cross collect/feed,
 // Circle (+ stick sideways) roll, Triangle backpack, R3 companion, L3 sprint.
 public static class NexoInput
 {
  public enum Act{Shoot,Aim,Reload,Interact,Dodge,Backpack,Map,Companion,Sprint}
  public static readonly string[] ActNames={"Lanzar","Apuntar","Recargar","Recoger / alimentar","Voltereta","Mochila","Mapa","Compañera","Correr"};
  static readonly GamepadButton[] Defaults={GamepadButton.RightTrigger,GamepadButton.LeftTrigger,GamepadButton.West,GamepadButton.South,GamepadButton.East,GamepadButton.North,GamepadButton.Select,GamepadButton.RightStick,GamepadButton.LeftStick};
  // Buttons that can be assigned (sticks' directions, D-pad and Options stay fixed).
  public static readonly GamepadButton[] Assignable={GamepadButton.South,GamepadButton.East,GamepadButton.West,GamepadButton.North,GamepadButton.LeftShoulder,GamepadButton.RightShoulder,GamepadButton.LeftTrigger,GamepadButton.RightTrigger,GamepadButton.LeftStick,GamepadButton.RightStick,GamepadButton.Select};
  static GamepadButton[] binds;
  static bool gamepadScheme,schemeInitialized;
  static NexoInput(){InputSystem.onDeviceChange+=OnDeviceChange;InputSystem.onAfterUpdate+=RefreshScheme;}
  static string Key(Act a)=>"Nexoterra.Pad."+a;
  public static GamepadButton Binding(Act a){Load();return binds[(int)a];}
  public static void SetBinding(Act a,GamepadButton b)
  {
   Load();int i=(int)a;var old=binds[i];
   // Swap with whichever action already used that button, so nothing ends up unbound.
   for(int j=0;j<binds.Length;j++)if(j!=i&&binds[j]==b){binds[j]=old;PlayerPrefs.SetInt(Key((Act)j),(int)old);}
   binds[i]=b;PlayerPrefs.SetInt(Key(a),(int)b);PlayerPrefs.Save();
  }
  public static void ResetBindings(){binds=(GamepadButton[])Defaults.Clone();for(int i=0;i<binds.Length;i++)PlayerPrefs.DeleteKey(Key((Act)i));PlayerPrefs.Save();}
  static void Load(){if(binds!=null)return;binds=new GamepadButton[Defaults.Length];for(int i=0;i<binds.Length;i++)binds[i]=(GamepadButton)PlayerPrefs.GetInt(Key((Act)i),(int)Defaults[i]);}
  public static string ButtonName(GamepadButton b)=>b switch{
   GamepadButton.South=>"X (Cruz)",GamepadButton.East=>"Círculo",GamepadButton.West=>"Cuadrado",GamepadButton.North=>"Triángulo",
   GamepadButton.LeftShoulder=>"L1",GamepadButton.RightShoulder=>"R1",GamepadButton.LeftTrigger=>"L2",GamepadButton.RightTrigger=>"R2",
   GamepadButton.LeftStick=>"L3",GamepadButton.RightStick=>"R3",GamepadButton.Select=>"Create",GamepadButton.Start=>"Options",_=>b.ToString()};
  static ButtonControl Ctl(Act a){var p=Pad;return p==null?null:p[Binding(a)];}
  public static bool Pressed(Act a){var c=Ctl(a);return c!=null&&c.wasPressedThisFrame;}
  public static bool HeldAct(Act a){var c=Ctl(a);return c!=null&&c.isPressed;}
  // Returns the assignable button pressed this frame, or null.
  public static GamepadButton? AnyAssignablePressed(){var p=Pad;if(p==null)return null;foreach(var b in Assignable)if(p[b].wasPressedThisFrame)return b;return null;}

  public static Gamepad Pad
  {
   get
   {
    var current=Gamepad.current;if(current!=null&&current.added)return current;
    foreach(var candidate in Gamepad.all)if(candidate!=null&&candidate.added)return candidate;
    return null;
   }
  }
  public static bool UsingGamepad=>Pad!=null&&gamepadScheme;
  public static bool UsingTouch=>MobileTouchControls.IsEnabled&&!UsingGamepad;
  public static string ActiveDeviceLabel
  {
   get
   {
    if(UsingGamepad){string name=Pad.displayName??"";return name.IndexOf("DualSense",System.StringComparison.OrdinalIgnoreCase)>=0||name.IndexOf("DualShock",System.StringComparison.OrdinalIgnoreCase)>=0?"DUALSENSE":"MANDO";}
    return UsingTouch?"TÁCTIL":"TECLADO + RATÓN";
   }
  }
  static void OnDeviceChange(InputDevice device,InputDeviceChange change)
  {
   if(!(device is Gamepad))return;
   if(change==InputDeviceChange.Added){gamepadScheme=true;schemeInitialized=true;}
   else if(change==InputDeviceChange.Removed&&Pad==null)gamepadScheme=false;
  }
  static bool HasGamepadActivity(Gamepad p)
  {
   return p.leftStick.ReadValue().sqrMagnitude>.04f||p.rightStick.ReadValue().sqrMagnitude>.04f||p.dpad.ReadValue().sqrMagnitude>.04f||
    p.buttonSouth.wasPressedThisFrame||p.buttonEast.wasPressedThisFrame||p.buttonWest.wasPressedThisFrame||p.buttonNorth.wasPressedThisFrame||
    p.leftShoulder.wasPressedThisFrame||p.rightShoulder.wasPressedThisFrame||p.leftStickButton.wasPressedThisFrame||p.rightStickButton.wasPressedThisFrame||
    p.startButton.wasPressedThisFrame||p.selectButton.wasPressedThisFrame||p.leftTrigger.isPressed||p.rightTrigger.isPressed;
  }
  static void RefreshScheme()
  {
   var pad=Pad;
   if(!schemeInitialized){gamepadScheme=pad!=null;schemeInitialized=true;}
   if(pad==null){gamepadScheme=false;return;}
   if(HasGamepadActivity(pad))gamepadScheme=true;
   var keyboard=Keyboard.current;if(keyboard!=null&&keyboard.anyKey.wasPressedThisFrame)gamepadScheme=false;
   var mouse=Mouse.current;
   if(mouse!=null&&(mouse.leftButton.wasPressedThisFrame||mouse.rightButton.wasPressedThisFrame||mouse.middleButton.wasPressedThisFrame||mouse.delta.ReadValue().sqrMagnitude>36f))gamepadScheme=false;
   if(MobileTouchControls.HasTouchActivity)gamepadScheme=false;
  }
  static bool Down(ButtonControl b)=>b!=null&&b.wasPressedThisFrame;
  static bool Held(ButtonControl b)=>b!=null&&b.isPressed;
  public static Vector2 Move{get{var touch=MobileTouchControls.Move;if(touch.sqrMagnitude>.01f)return touch;var p=Pad;if(p==null)return Vector2.zero;var v=p.leftStick.ReadValue();return v.magnitude<.15f?Vector2.zero:v;}}
  public static Vector2 Look{get{var p=Pad;if(p==null)return Vector2.zero;var v=p.rightStick.ReadValue();return v.magnitude<.12f?Vector2.zero:v*v.magnitude;}}
  public static bool Sprint=>MobileTouchControls.Sprint||Pad!=null&&(HeldAct(Act.Sprint)||Pad.leftStick.ReadValue().magnitude>.92f);
  public static bool Aim=>MobileTouchControls.Aim||HeldAct(Act.Aim);
  public static bool Shoot=>MobileTouchControls.ShootPressed||Pressed(Act.Shoot);
  public static bool Reload=>MobileTouchControls.ReloadPressed||Pressed(Act.Reload);
  public static bool Interact=>MobileTouchControls.InteractPressed||Pressed(Act.Interact);
  public static bool Dodge=>MobileTouchControls.DodgePressed||Pressed(Act.Dodge);
  public static bool Backpack=>Pressed(Act.Backpack);
  public static bool Map=>Pressed(Act.Map);
  public static bool Companion=>MobileTouchControls.CompanionPressed||Pressed(Act.Companion);
  public static bool Pause=>MobileTouchControls.PausePressed||Pad!=null&&Down(Pad.startButton);
  public static bool Emotes=>Pad!=null&&Held(Pad.leftShoulder)&&Down(Pad.dpad.down);
  public static int Cycle=>Pad==null||Held(Pad.leftShoulder)&&Pad.dpad.ReadValue().sqrMagnitude>0?0:Down(Pad.rightShoulder)?1:Down(Pad.leftShoulder)?-1:0;
  // D-pad picks slots: up=1, right=2, down=3, left=4 (only when L1 is not held).
  public static int Slot{get{var p=Pad;if(p==null||Held(p.leftShoulder))return -1;if(Down(p.dpad.up))return 0;if(Down(p.dpad.right))return 1;if(Down(p.dpad.down))return 2;if(Down(p.dpad.left))return 3;return -1;}}
  // Menu-style direction this frame (D-pad or left-stick flick).
  public static Vector2Int NavStep{get{var p=Pad;if(p==null)return Vector2Int.zero;int x=(Down(p.dpad.right)||Down(p.leftStick.right)?1:0)-(Down(p.dpad.left)||Down(p.leftStick.left)?1:0);int y=(Down(p.dpad.down)||Down(p.leftStick.down)?1:0)-(Down(p.dpad.up)||Down(p.leftStick.up)?1:0);return new Vector2Int(x,y);}}
  public static bool Confirm=>Pad!=null&&Down(Pad.buttonSouth);
  public static bool Cancel=>Pad!=null&&Down(Pad.buttonEast);
  public static bool AnyActivity{get{var p=Pad;return MobileTouchControls.HasGameplayActivity||Move!=Vector2.zero||Look!=Vector2.zero||p!=null&&HasGamepadActivity(p);}}

  public static void DrawGameplayControls(float left,float right,float y,float step)
  {
   if(UsingGamepad)
   {
    NexoHud.ControlRow(left,y,"caminar","L-Stick");NexoHud.ControlRow(left,y+step,"correr",ButtonName(Binding(Act.Sprint)));
    NexoHud.ControlRow(left,y+step*2,"voltereta",ButtonName(Binding(Act.Dodge)));NexoHud.ControlRow(left,y+step*3,"recoger",ButtonName(Binding(Act.Interact)));
    NexoHud.ControlRow(left,y+step*4,"alimentar",ButtonName(Binding(Act.Interact)));NexoHud.ControlRow(left,y+step*5,"gestos","L1","+","↓");
    NexoHud.ControlRow(right,y,"apuntar",ButtonName(Binding(Act.Aim)));NexoHud.ControlRow(right,y+step,"lanzar",ButtonName(Binding(Act.Shoot)));
    NexoHud.ControlRow(right,y+step*2,"recargar",ButtonName(Binding(Act.Reload)));NexoHud.ControlRow(right,y+step*3,"mochila",ButtonName(Binding(Act.Backpack)));
    NexoHud.ControlRow(right,y+step*4,"compañera",ButtonName(Binding(Act.Companion)));NexoHud.ControlRow(right,y+step*5,"menú",ButtonName(GamepadButton.Start));
   }
   else if(UsingTouch)
   {
    NexoHud.ControlRow(left,y,"caminar","Joystick");NexoHud.ControlRow(left,y+step,"correr","Correr");
    NexoHud.ControlRow(left,y+step*2,"voltereta","Esquiva");NexoHud.ControlRow(left,y+step*3,"recoger","Usar");
    NexoHud.ControlRow(left,y+step*4,"alimentar","Usar");NexoHud.ControlRow(left,y+step*5,"gestos","—");
    NexoHud.ControlRow(right,y,"apuntar","Apuntar");NexoHud.ControlRow(right,y+step,"lanzar","Lanzar");
    NexoHud.ControlRow(right,y+step*2,"recargar","Recarga");NexoHud.ControlRow(right,y+step*3,"mochila","HUD");
    NexoHud.ControlRow(right,y+step*4,"compañera","COMP");NexoHud.ControlRow(right,y+step*5,"menú","Ⅱ");
   }
   else
   {
    NexoHud.ControlRow(left,y,"caminar","W","A","S","D");NexoHud.ControlRow(left,y+step,"correr","Mayús");
    NexoHud.ControlRow(left,y+step*2,"voltereta","Ctrl","+","A","/","D");NexoHud.ControlRow(left,y+step*3,"recoger","E");
    NexoHud.ControlRow(left,y+step*4,"alimentar","F");NexoHud.ControlRow(left,y+step*5,"gestos","B");
    NexoHud.ControlRow(right,y,"apuntar","Clic der");NexoHud.ControlRow(right,y+step,"lanzar","Clic izq");
    NexoHud.ControlRow(right,y+step*2,"recargar","R");NexoHud.ControlRow(right,y+step*3,"mochila","Alt");
    NexoHud.ControlRow(right,y+step*4,"compañera","G");NexoHud.ControlRow(right,y+step*5,"menú","Esc");
   }
  }

  // ---- Settings page for remapping ----
  static int listening=-1,listenFrame;
  public static bool Listening=>listening>=0;
  // Returns true when the player leaves the page.
  public static bool DrawPage(Rect rect)
  {
   NexoUi.Title(new Rect(rect.x,rect.y,rect.width,70),"CONTROLES DEL MANDO",Pad==null?"No hay mando conectado. Conecta un DualSense por USB o Bluetooth.":"Mando: "+Pad.displayName+". Elige una acción y pulsa el botón nuevo.");
   bool wasListening=listening>=0;
   if(listening>=0&&Time.frameCount>listenFrame+1)
   {
    var b=AnyAssignablePressed();
    if(b.HasValue){SetBinding((Act)listening,b.Value);listening=-1;}
    else if(Pad!=null&&Pad.startButton.wasPressedThisFrame||Keyboard.current!=null&&Keyboard.current.escapeKey.wasPressedThisFrame)listening=-1;
   }
   float y=rect.y+80,half=(rect.width-16)/2,row=46;
   int slot=0;
   for(int i=0;i<ActNames.Length;i++)
   {
    float x=rect.x+(slot%2)*(half+16),yy=y+(slot/2)*row;slot++;
    NexoUi.Write(new Rect(x,yy,half*.5f,38),ActNames[i],14,NexoUi.Text);
    string label=listening==i?"PULSA UN BOTÓN…":ButtonName(Binding((Act)i));
    if(NexoUi.Button(new Rect(x+half*.5f,yy+2,half*.5f,36),label,listening==i)&&!wasListening&&listening<0){listening=i;listenFrame=Time.frameCount;}
   }
   y+=Mathf.Ceil(ActNames.Length/2f)*row+6;
   NexoUi.Write(new Rect(rect.x,y,rect.width,40),"Fijos: stick izq. mover · stick der. cámara · L1/R1 cambiar babosa · cruceta ranura 1–4 · L1 + abajo gestos · Options pausa.\nEn la mochila: cruceta mueve, X coge/suelta, Círculo cancela.",12,NexoUi.Muted);
   if(wasListening||listening>=0)return false;
   float half2=(rect.width-16)/2;
   if(NexoUi.Button(new Rect(rect.x,rect.yMax-50,half2,50),"POR DEFECTO"))ResetBindings();
   bool back=NexoHud.Button(new Rect(rect.x+half2+16,rect.yMax-50,half2,50),"VOLVER");
   return back||NexoUi.BackPressed;
  }
 }
}
