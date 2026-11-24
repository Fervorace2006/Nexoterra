using UnityEngine;
using UnityEngine.InputSystem;
namespace Nexoterra.World
{
 // Nexoterra UI kit (IMGUI, 1280x720 virtual canvas scaled by NexoHud.Scale).
 // One palette, one type scale and a small set of controls that every screen shares.
 // Every interactive control has normal / hover / pressed / disabled / focused states and
 // takes part in keyboard (arrows, Tab, Enter) and gamepad (d-pad, A, B) navigation when a
 // screen wraps its drawing in BeginNav()/EndNav().
 public static class NexoUi
 {
  // ---- Palette ----
  public static readonly Color
   Background=new Color(.016f,.036f,.05f,.97f), // panels
   Surface=new Color(.03f,.066f,.085f,.98f),    // cards, inputs
   Raised=new Color(.055f,.11f,.135f,1f),       // buttons at rest
   RaisedHover=new Color(.075f,.165f,.19f,1f),
   Pressed=new Color(.025f,.07f,.085f,1f),
   Edge=new Color(.16f,.3f,.36f),               // hairlines
   Text=new Color(.9f,.96f,.98f),
   Muted=new Color(.56f,.69f,.74f),
   Teal=new Color(.22f,.88f,.82f),              // interaction / focus
   Amber=new Color(.98f,.66f,.22f),             // primary action / selection
   Danger=new Color(1f,.4f,.34f);

  // ---- Type ----
  static readonly System.Collections.Generic.Dictionary<int,GUIStyle> styles=new System.Collections.Generic.Dictionary<int,GUIStyle>();
  public static void Write(Rect r,string text,int size,Color color,TextAnchor anchor=TextAnchor.MiddleLeft,bool bold=false)
  {
   int key=size*64+(int)anchor*2+(bold?1:0);
   if(!styles.TryGetValue(key,out var s)){s=new GUIStyle(GUI.skin.label){fontSize=size,alignment=anchor,wordWrap=true,clipping=TextClipping.Clip,fontStyle=bold?FontStyle.Bold:FontStyle.Normal,padding=new RectOffset(0,0,0,0)};styles[key]=s;}
   s.normal.textColor=color;GUI.Label(r,text,s);
  }
  public static void Title(Rect r,string title,string subtitle=null)
  {
   Write(new Rect(r.x,r.y,r.width,34),title,26,Text,TextAnchor.MiddleLeft,true);
   NexoHud.Fill(new Rect(r.x,r.y+40,36,3),Amber);
   if(!string.IsNullOrEmpty(subtitle))Write(new Rect(r.x,r.y+48,r.width,20),subtitle,13,Muted);
  }
  public static void Section(Rect r,string label)
  {
   Write(new Rect(r.x,r.y,r.width,18),label,12,Teal,TextAnchor.MiddleLeft,true);
   NexoHud.Fill(new Rect(r.x,r.y+20,r.width,1),Edge);
  }
  static void Border(Rect r,Color c,float t=1)
  {
   NexoHud.Fill(new Rect(r.x,r.y,r.width,t),c);NexoHud.Fill(new Rect(r.x,r.yMax-t,r.width,t),c);
   NexoHud.Fill(new Rect(r.x,r.y,t,r.height),c);NexoHud.Fill(new Rect(r.xMax-t,r.y,t,r.height),c);
  }

  // ---- Surfaces ----
  public static void Panel(Rect r)
  {
   NexoHud.Fill(new Rect(r.x+3,r.y+5,r.width,r.height),new Color(0,0,0,.45f));
   NexoHud.Gradient(r,new Color(.03f,.07f,.09f,.97f),Background);
   Border(r,Edge);
   NexoHud.Fill(new Rect(r.x,r.y,r.width*.32f,2),Teal);NexoHud.Fill(new Rect(r.x+r.width*.32f,r.y,22,2),Amber);
  }
  public static void Card(Rect r,Color accent,bool selected=false)
  {
   NexoHud.Fill(new Rect(r.x+2,r.y+3,r.width,r.height),new Color(0,0,0,.35f));
   NexoHud.Fill(r,Surface);Border(r,selected?accent:Edge,selected?2:1);
   NexoHud.Fill(new Rect(r.x,r.y,r.width,2),selected?accent:accent*new Color(1,1,1,.55f));
  }

  // ---- Focus navigation ----
  static bool navActive,keyboardMode;static int navCount,lastCount,focus=-1,activate=-1,navFrame=-1;static Vector2 lastMouse;
  public static bool BackPressed {get;private set;}
  public static void BeginNav()
  {
   navActive=true;navCount=0;var e=Event.current;
   if(e.type==EventType.Layout&&navFrame!=Time.frameCount)
   {
    navFrame=Time.frameCount;BackPressed=false;
    var pad=Gamepad.current;
    if(pad!=null)
    {
     if(pad.dpad.down.wasPressedThisFrame||pad.leftStick.down.wasPressedThisFrame||pad.dpad.right.wasPressedThisFrame)Move(1);
     if(pad.dpad.up.wasPressedThisFrame||pad.leftStick.up.wasPressedThisFrame||pad.dpad.left.wasPressedThisFrame)Move(-1);
     if(pad.buttonSouth.wasPressedThisFrame&&focus>=0)activate=focus;
     if(pad.buttonEast.wasPressedThisFrame)BackPressed=true;
    }
   }
   if(e.type==EventType.KeyDown)
   {
    switch(e.keyCode)
    {
     case KeyCode.DownArrow:case KeyCode.RightArrow:Move(1);e.Use();break;
     case KeyCode.UpArrow:case KeyCode.LeftArrow:Move(-1);e.Use();break;
     case KeyCode.Tab:Move(e.shift?-1:1);e.Use();break;
     case KeyCode.Return:case KeyCode.KeypadEnter:if(focus>=0){activate=focus;e.Use();}break;
     case KeyCode.Escape:case KeyCode.Backspace:if(GUIUtility.keyboardControl==0){BackPressed=true;}break;
    }
   }
   if(e.type==EventType.Repaint){if((e.mousePosition-lastMouse).sqrMagnitude>4)keyboardMode=false;lastMouse=e.mousePosition;}
  }
  public static void EndNav(){navActive=false;lastCount=navCount;if(focus>=navCount)focus=navCount-1;}
  public static void ResetFocus(){focus=-1;activate=-1;}
  static void Move(int d){keyboardMode=true;if(lastCount<=0)return;focus=focus<0?(d>0?0:lastCount-1):(focus+d+lastCount)%lastCount;}

  // Shared click logic: returns clicked; reports hover/pressed/focused for drawing.
  static bool Interact(Rect r,out bool hover,out bool pressed,out bool focused)
  {
   int index=navActive?navCount++:-1;
   int id=GUIUtility.GetControlID(FocusType.Passive,r);var e=Event.current;bool enabled=GUI.enabled,clicked=false;
   hover=enabled&&r.Contains(e.mousePosition);
   switch(e.GetTypeForControl(id))
   {
    case EventType.MouseDown:if(hover&&e.button==0){GUIUtility.hotControl=id;e.Use();}break;
    case EventType.MouseDrag:if(GUIUtility.hotControl==id)e.Use();break;
    case EventType.MouseUp:if(GUIUtility.hotControl==id){GUIUtility.hotControl=0;e.Use();clicked=hover;}break;
   }
   if(hover&&index>=0&&!keyboardMode)focus=index;
   pressed=GUIUtility.hotControl==id&&hover;
   focused=index>=0&&index==focus&&keyboardMode&&enabled;
   if(index>=0&&index==activate){activate=-1;if(enabled)clicked=true;}
   return clicked;
  }

  // ---- Controls ----
  // Main action button. primary = the recommended action on a screen (amber accent).
  public static bool Action(Rect r,string label,bool primary=false)
  {
   bool clicked=Interact(r,out bool hover,out bool pressed,out bool focused);bool enabled=GUI.enabled;bool lit=hover||focused;
   if(Event.current.type==EventType.Repaint)
   {
    if(focused)Border(new Rect(r.x-3,r.y-3,r.width+6,r.height+6),Teal,2);
    NexoHud.Fill(new Rect(r.x+1,r.y+3,r.width,r.height),new Color(0,0,0,.35f));
    Color fill=!enabled?new Color(.04f,.07f,.085f):pressed?Pressed:lit?RaisedHover:Raised;
    NexoHud.Gradient(r,fill*1.12f,fill);
    Border(r,!enabled?Edge*.6f:lit?Teal:primary?Amber*.85f:Edge);
    if(enabled)NexoHud.Fill(new Rect(r.x,r.y,4,r.height),primary?Amber:lit?Teal:Edge);
    if(lit)NexoHud.Fill(new Rect(r.x+4,r.yMax-2,r.width-5,1),Teal*new Color(1,1,1,.6f));
    var text=new Rect(r.x+16,r.y+(pressed?1:0),r.width-32,r.height);
    Write(text,label,r.height<44?14:16,!enabled?Muted*new Color(1,1,1,.6f):primary&&!lit?Amber:Text,TextAnchor.MiddleCenter,true);
    if(lit&&enabled)Write(new Rect(r.xMax-26,r.y,16,r.height),"›",20,Teal,TextAnchor.MiddleCenter,true);
   }
   return clicked;
  }
  // Tab / segmented option. active = currently selected value.
  public static bool Button(Rect r,string label,bool active=false)
  {
   bool clicked=Interact(r,out bool hover,out bool pressed,out bool focused);bool enabled=GUI.enabled;bool lit=hover||focused;
   if(Event.current.type==EventType.Repaint)
   {
    if(focused)Border(new Rect(r.x-2,r.y-2,r.width+4,r.height+4),Teal,2);
    NexoHud.Fill(r,pressed?Pressed:active?new Color(.09f,.16f,.17f):lit?RaisedHover:Surface);
    Border(r,!enabled?Edge*.6f:active?Amber*.9f:lit?Teal:Edge);
    if(active)NexoHud.Fill(new Rect(r.x,r.yMax-3,r.width,3),Amber);
    Write(new Rect(r.x+6,r.y+(pressed?1:0),r.width-12,r.height),label,r.height<36?12:13,!enabled?Muted*new Color(1,1,1,.6f):active?Amber:Text,TextAnchor.MiddleCenter,true);
   }
   return clicked;
  }
  // Row of mutually exclusive options; returns the chosen index (or current).
  public static int Segmented(Rect r,string[] options,int current,float gap=6)
  {
   float w=(r.width-gap*(options.Length-1))/options.Length;int result=current;
   for(int i=0;i<options.Length;i++)if(Button(new Rect(r.x+i*(w+gap),r.y,w,r.height),options[i],i==current))result=i;
   return result;
  }
  // Labelled on/off switch row; returns the new value.
  public static bool Toggle(Rect r,string label,string hint,bool value)
  {
   bool clicked=Interact(r,out bool hover,out bool pressed,out bool focused);bool lit=hover||focused;
   if(Event.current.type==EventType.Repaint)
   {
    if(focused)Border(new Rect(r.x-2,r.y-2,r.width+4,r.height+4),Teal,2);
    NexoHud.Fill(r,pressed?Pressed:lit?RaisedHover:Surface);Border(r,lit?Teal:Edge);
    bool hasHint=!string.IsNullOrEmpty(hint);
    Write(new Rect(r.x+14,r.y+(hasHint?6:0),r.width-150,hasHint?22:r.height),label,15,Text,TextAnchor.MiddleLeft,true);
    if(hasHint)Write(new Rect(r.x+14,r.y+28,r.width-150,r.height-32),hint,12,Muted,TextAnchor.UpperLeft);
    var track=new Rect(r.xMax-66,r.center.y-12,50,24);
    NexoHud.Fill(track,value?Teal*new Color(1,1,1,.85f):new Color(.1f,.16f,.19f));Border(track,value?Teal:Edge);
    var knob=new Rect(value?track.xMax-21:track.x+3,track.y+3,18,18);NexoHud.Fill(knob,value?new Color(.02f,.1f,.11f):Muted);
    Write(new Rect(track.x-46,track.y,40,track.height),value?"SÍ":"NO",12,value?Teal:Muted,TextAnchor.MiddleRight,true);
   }
   return clicked?!value:value;
  }

  // ---- Slug canister (blaster chamber): rounded metal tube, teal glass, amber cap, round number socket ----
  static void Round(Rect r,Color c,float radius,float border=0){if(Event.current.type==EventType.Repaint)GUI.DrawTexture(r,Texture2D.whiteTexture,ScaleMode.StretchToFill,true,0,c,border,radius);}
  public static void SlugCanister(Rect r,SlugDefinition d,bool selected,int number,float alpha=1,string label=null)
  {
   float labelH=label!=null?20:0;var tube=new Rect(r.x,r.y,r.width,r.height-labelH);var accent=Accent(d);
   float rad=Mathf.Min(16,tube.width*.3f);
   Round(new Rect(tube.x+2,tube.y+4,tube.width,tube.height),new Color(0,0,0,.45f),rad);
   Round(tube,selected?Amber:new Color(.2f,.3f,.34f),rad);                                  // metal shell
   var shell=new Rect(tube.x+2,tube.y+2,tube.width-4,tube.height-4);Round(shell,new Color(.08f,.13f,.15f),rad-2);
   var glass=new Rect(shell.x+4,shell.y+10,shell.width-8,shell.height-36);
   Round(glass,new Color(.02f,.16f,.19f),rad-4);
   Round(new Rect(glass.x+2,glass.y+2,glass.width-4,glass.height*.45f),new Color(.2f,.75f,.8f,.18f),rad-5); // glass sheen
   Round(new Rect(glass.x+4,glass.y+4,3,glass.height-8),new Color(.7f,1,1,.18f),2);
   Round(new Rect(shell.x+6,shell.y+3,shell.width-12,5),selected?Amber:new Color(.75f,.45f,.15f),3);          // cap rim
   if(d!=null)Portrait(new Rect(glass.x+2,glass.y+2,glass.width-4,glass.height-4),d.portrait,accent,alpha);
   else Write(glass,"+",22,new Color(.35f,.55f,.6f),TextAnchor.MiddleCenter,true);
   if(d!=null&&d.IsElemental)Write(new Rect(glass.xMax-16,glass.y+2,14,14),"E",10,accent,TextAnchor.MiddleCenter,true);
   var socket=new Rect(tube.center.x-11,tube.yMax-25,22,22);
   Round(socket,selected?Amber:new Color(.25f,.35f,.4f),11);Round(new Rect(socket.x+3,socket.y+3,16,16),selected?new Color(1,.8f,.4f):new Color(.04f,.09f,.11f),8);
   if(number>0)Write(socket,number.ToString(),11,selected?new Color(.1f,.06f,0):Text,TextAnchor.MiddleCenter,true);
   if(label!=null)Write(new Rect(r.x-4,r.yMax-labelH,r.width+8,labelH),label,11,d!=null?Text:Muted,TextAnchor.MiddleCenter);
  }
  // ---- Content helpers ----
  static Texture2D glow;
  public static Color Accent(SlugDefinition d)
  {
   if(d==null)return Edge;string id=d.id;
   if(id.Contains("Fuego")||id=="Infierno")return new Color(1,.42f,.16f);
   if(id.Contains("Energia")||id.Contains("Doc"))return new Color(.33f,.93f,.63f);
   if(id.Contains("Agua")||id=="ice")return new Color(.2f,.77f,1);
   if(id=="tazerling")return new Color(.6f,.6f,1);
   if(id=="Arana")return new Color(.8f,.45f,1);
   return new Color(.83f,.7f,.42f);
  }
  public static void Portrait(Rect r,Texture2D portrait,Color accent,float alpha=1)
  {
   if(!glow){glow=new Texture2D(64,64,TextureFormat.RGBA32,false);glow.wrapMode=TextureWrapMode.Clamp;for(int y=0;y<64;y++)for(int x=0;x<64;x++){float d=Vector2.Distance(new Vector2(x,y),new Vector2(31.5f,31.5f))/44;glow.SetPixel(x,y,new Color(1,1,1,Mathf.Pow(Mathf.Clamp01(1-d),2)*.45f));}glow.Apply();}
   var old=GUI.color;GUI.color=accent;GUI.DrawTexture(r,glow);GUI.color=new Color(1,1,1,alpha);if(portrait)GUI.DrawTexture(r,portrait,ScaleMode.ScaleToFit,true);GUI.color=old;
  }
  public static string Ability(SlugDefinition d)
  {
   if(d==null)return "Elige una babosa de tu colección.";
   if(d.id.StartsWith("Malvada_"))return Ability(new SlugDefinition{id=SlugCatalog.BaseSpecies(d.id)})+"\n+5% de daño · cuesta 5 PV por disparo.";
   switch(d.id){case "Infierno":case "Elemental_Fuego":return "FUEGO\nImpacto y quemadura posterior.";case "Elemental_Agua":return "AGUA\nInmoviliza con un círculo de agua.";case "ice":return "HIELO\nCongela al objetivo temporalmente.";case "tazerling":return "ELECTRICIDAD\nBloquea movimiento y disparo.";case "Gon_Doc":case "Elemental_Energia":return "CURACIÓN\nRecupera vida al lanzarla.";case "Elemental_Aire":case "Tornado":return "VIENTO\nArrastra enemigos y aumenta tu velocidad.";case "Arana":return "TELARAÑA\nAtrapa los pies del enemigo.";default:return "IMPACTO\nGolpe contundente y derribo.";}
  }
 }
}
