using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
namespace Nexoterra.World
{
 // PS5 (DualSense) / any gamepad layer, combined with keyboard and mouse by the callers.
 // Cross=collect/feed, Square=reload, Circle+stick=side roll, Triangle=backpack,
 // L2=aim, R2=shoot, L1/R1=cycle slugs, D-pad=slot, L3=sprint, Options=pause, Create/touchpad=map,
 // R3=release companion (G), D-pad down while L1 held=emotes.
 public static class NexoInput
 {
  public static Gamepad Pad=>Gamepad.current;
  static bool Down(ButtonControl b)=>b!=null&&b.wasPressedThisFrame;
  static bool Held(ButtonControl b)=>b!=null&&b.isPressed;
  public static Vector2 Move{get{var p=Pad;if(p==null)return Vector2.zero;var v=p.leftStick.ReadValue();return v.magnitude<.15f?Vector2.zero:v;}}
  public static Vector2 Look{get{var p=Pad;if(p==null)return Vector2.zero;var v=p.rightStick.ReadValue();return v.magnitude<.12f?Vector2.zero:v*v.magnitude;}}
  public static bool Sprint=>Pad!=null&&(Held(Pad.leftStickButton)||Pad.leftStick.ReadValue().magnitude>.92f);
  public static bool Aim=>Pad!=null&&Pad.leftTrigger.ReadValue()>.4f;
  public static bool Shoot=>Pad!=null&&Down(Pad.rightTrigger);
  public static bool Reload=>Pad!=null&&Down(Pad.buttonWest);
  public static bool Interact=>Pad!=null&&Down(Pad.buttonSouth);
  public static bool Dodge=>Pad!=null&&Down(Pad.buttonEast);
  public static bool Backpack=>Pad!=null&&Down(Pad.buttonNorth);
  public static bool Pause=>Pad!=null&&Down(Pad.startButton);
  public static bool Map=>Pad!=null&&Down(Pad.selectButton);
  public static bool Companion=>Pad!=null&&Down(Pad.rightStickButton);
  public static bool Emotes=>Pad!=null&&Held(Pad.leftShoulder)&&Down(Pad.dpad.down);
  public static int Cycle=>Pad==null||Held(Pad.leftShoulder)&&Pad.dpad.ReadValue().sqrMagnitude>0?0:Down(Pad.rightShoulder)?1:Down(Pad.leftShoulder)?-1:0;
  // D-pad picks slots: up=1, right=2, down=3, left=4 (only when L1 is not held).
  public static int Slot{get{var p=Pad;if(p==null||Held(p.leftShoulder))return -1;if(Down(p.dpad.up))return 0;if(Down(p.dpad.right))return 1;if(Down(p.dpad.down))return 2;if(Down(p.dpad.left))return 3;return -1;}}
  public static bool AnyActivity{get{var p=Pad;if(p==null)return false;return Move!=Vector2.zero||Look!=Vector2.zero||p.rightTrigger.isPressed||p.leftTrigger.isPressed||p.buttonSouth.isPressed||p.buttonWest.isPressed||p.buttonEast.isPressed;}}
 }
}
