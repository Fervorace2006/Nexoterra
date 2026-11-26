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
  // Dark metal console panel with orange corner brackets (menu container look).
  public static void Panel(Rect r)
  {
   NexoHud.Fill(new Rect(r.x+3,r.y+5,r.width,r.height),new Color(0,0,0,.5f));
   Chamfer(r,ChamferFill,new Color(.11f,.14f,.16f),14);
   Chamfer(new Rect(r.x+4,r.y+4,r.width-8,r.height-8),ChamferFill,new Color(.035f,.07f,.085f),11);
   Chamfer(r,ChamferRing,new Color(.3f,.38f,.42f),14);
   NexoHud.Brackets(new Rect(r.x-3,r.y-3,r.width+6,r.height+6),new Color(1f,.58f,.12f),18,4);
  }
  public static void Card(Rect r,Color accent,bool selected=false)
  {
   NexoHud.Fill(new Rect(r.x+2,r.y+3,r.width,r.height),new Color(0,0,0,.35f));
   NexoHud.Fill(r,Surface);Border(r,selected?accent:Edge,selected?2:1);
   NexoHud.Fill(new Rect(r.x,r.y,r.width,2),selected?accent:accent*new Color(1,1,1,.55f));
  }

  // ---- Focus navigation ----
  static int hoverId;static bool navActive,keyboardMode;static int navCount,lastCount,focus=-1,activate=-1,navFrame=-1;static Vector2 lastMouse;
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
   bool touchHover=MobileTouchControls.TryGetPointer(out var touchPosition)&&r.Contains(touchPosition);
   hover=enabled&&(r.Contains(e.mousePosition)||touchHover);
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
   if(enabled&&MobileTouchControls.TryConsumeTap(r)){hover=true;clicked=true;}
   // Sounds: hover when the pointer/focus enters a control, click when it activates.
   if(e.type==EventType.Repaint){bool over=hover||focused;if(over&&hoverId!=id){hoverId=id;NexoAudio.Play("ui_hover",.22f);}else if(!over&&hoverId==id)hoverId=0;}
   if(clicked)NexoAudio.Play("ui_click",.5f);
   return clicked;
  }

  // ---- Controls ----
  // ---- Chamfered (cut-corner) shapes, 9-sliced so corners keep their size ----
  static Texture2D fillTex,ringTex;static GUIStyle slice;
  static Texture2D Bake(bool ring)
  {
   const int S=64,C=12;var t=new Texture2D(S,S,TextureFormat.RGBA32,false){wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};
   for(int y=0;y<S;y++)for(int x=0;x<S;x++)
   {
    float dx=Mathf.Min(x,S-1-x),dy=Mathf.Min(y,S-1-y),edge=Mathf.Min(Mathf.Min(dx,dy),(dx+dy-C)*.7071f);
    float a=Mathf.Clamp01(edge+.5f);if(ring)a*=1-Mathf.Clamp01(edge-1.5f);
    float shade=ring?1:Mathf.Lerp(.72f,1f,y/(float)(S-1));       // lighter at the top (texture y up)
    t.SetPixel(x,y,new Color(shade,shade,shade,a));
   }
   t.Apply();return t;
  }
  static Texture2D ChamferFill=>fillTex?fillTex:(fillTex=Bake(false));
  static Texture2D ChamferRing=>ringTex?ringTex:(ringTex=Bake(true));
  static void Chamfer(Rect r,Texture2D tex,Color c,int corner=12)
  {
   if(Event.current.type!=EventType.Repaint)return;
   if(slice==null)slice=new GUIStyle();slice.normal.background=tex;slice.border=new RectOffset(corner+2,corner+2,corner+2,corner+2);
   var old=GUI.color;GUI.color=c;slice.Draw(r,false,false,false,false);GUI.color=old;
  }
  static void Chevrons(Rect r,Color c)
  {
   for(float x=r.x+r.width*.12f;x<r.xMax-r.height*.6f;x+=r.height*.9f)
    for(int k=0;k<3;k++){float y=r.center.y+(k-1)*r.height*.18f;NexoHud.Fill(new Rect(x+(1-Mathf.Abs(k-1))*r.height*.18f,y-1,r.height*.3f,2),c);}
  }
  static readonly Color Green=new Color(.05f,.52f,.13f),GreenHot=new Color(.09f,.68f,.2f),GreenEdge=new Color(.3f,.95f,.38f),Olive=new Color(.32f,.36f,.24f),Cyan=new Color(.12f,.72f,.88f);
  // Main action button: green chamfered plate with light top band and faint chevrons.
  // primary = slightly brighter. States: hover (brighter), pressed (darker, text down), disabled (olive), focused (cyan ring).
  public static bool Action(Rect r,string label,bool primary=false)
  {
   bool clicked=Interact(r,out bool hover,out bool pressed,out bool focused);bool enabled=GUI.enabled;bool lit=hover||focused;
   if(Event.current.type==EventType.Repaint)
   {
    // Gunmetal frame (like the HUD bars) holding a green hologram glass, orange end caps, sheen on hover.
    if(pressed)r=new Rect(r.x+2,r.y+1,r.width-4,r.height-2);
    NexoHud.Fill(new Rect(r.x+3,r.y+5,r.width-2,r.height),new Color(0,0,0,.5f));
    float pulse=.5f+.5f*Mathf.Sin(Time.unscaledTime*2.2f);
    if(enabled)Chamfer(new Rect(r.x-4,r.y-4,r.width+8,r.height+8),ChamferRing,new Color(.25f,1f,.4f,focused||lit?.6f:.14f+.08f*pulse),15);   // outer glow
    Chamfer(r,ChamferFill,new Color(.09f,.11f,.12f),12);                                     // dark metal rim
    Chamfer(new Rect(r.x+1,r.y+1,r.width-2,r.height*.5f),ChamferFill,new Color(.36f,.4f,.43f,.55f),11);   // lit upper bevel
    Chamfer(r,ChamferRing,new Color(.5f,.56f,.6f),12);
    var face=new Rect(r.x+5,r.y+5,r.width-10,r.height-10);
    Color top=!enabled?new Color(.3f,.33f,.25f):pressed?new Color(.05f,.42f,.13f):lit?new Color(.16f,.78f,.3f):new Color(.1f,.6f,.2f);
    Color bottom=!enabled?new Color(.14f,.16f,.12f):pressed?new Color(.01f,.2f,.06f):lit?new Color(.03f,.38f,.12f):new Color(.02f,.26f,.08f);
    Chamfer(face,ChamferFill,new Color(.01f,.06f,.03f),9);
    int bands=8;float bh=(face.height-4)/bands;
    for(int i=0;i<bands;i++)NexoHud.Fill(new Rect(face.x+4,face.y+2+i*bh,face.width-8,bh+.5f),Color.Lerp(top,bottom,i/(bands-1f)));
    if(enabled)Chamfer(face,ChamferRing,lit?new Color(.75f,1f,.8f):new Color(.35f,.95f,.45f,.9f),9);
    for(float y=face.y+4;y<face.yMax-3;y+=3)NexoHud.Fill(new Rect(face.x+6,y,face.width-12,1),new Color(0,0,0,enabled?.12f:.05f));   // scanlines
    NexoHud.Fill(new Rect(face.x+8,face.y+3,face.width-16,face.height*.38f),new Color(1,1,1,enabled?(lit?.13f:.08f):.03f));          // glass highlight
    NexoHud.Fill(new Rect(face.x+8,face.y+3,face.width-16,1),new Color(.85f,1,.9f,enabled?.5f:.1f));
    if(enabled&&lit){float sweep=Mathf.Repeat(Time.unscaledTime*.9f,1.6f)-.3f;float sx=face.x+face.width*sweep;
     for(int k=0;k<6;k++){float x=sx+k*4;if(x>face.x+6&&x<face.xMax-10)NexoHud.Fill(new Rect(x,face.y+4,3,face.height-8),new Color(1,1,1,.05f+.02f*(3-Mathf.Abs(k-2.5f))));}}
    // Orange end caps, like the bar frames.
    Color cap=enabled?NexoHud.Orange:new Color(.45f,.4f,.33f);float ch=Mathf.Min(r.height-12,r.height*.62f);
    NexoHud.Fill(new Rect(r.x-2,r.center.y-ch/2,4,ch),cap);NexoHud.Fill(new Rect(r.xMax-2,r.center.y-ch/2,4,ch),cap);
    NexoHud.Fill(new Rect(r.x-2,r.center.y-ch/2,4,2),Color.Lerp(cap,Color.white,.4f));NexoHud.Fill(new Rect(r.xMax-2,r.center.y-ch/2,4,2),Color.Lerp(cap,Color.white,.4f));
    var text=new Rect(r.x+14,r.y,r.width-28,r.height);
    int size=r.height<44?15:r.height<58?19:22;
    // Silver: dark drop shadow, darker steel lower edge, bright silver face.
    Write(new Rect(text.x+1,text.y+2,text.width,text.height),label,size,new Color(0,.06f,.02f,.85f),TextAnchor.MiddleCenter,true);
    Write(new Rect(text.x,text.y+1,text.width,text.height),label,size,!enabled?new Color(.45f,.47f,.45f):new Color(.55f,.6f,.64f),TextAnchor.MiddleCenter,true);
    Write(text,label,size,!enabled?new Color(.7f,.72f,.7f):lit?new Color(.98f,.99f,1f):new Color(.86f,.89f,.92f),TextAnchor.MiddleCenter,true);
   }
   return clicked;
  }
  // Horizontal volume slider: label + percentage, green hologram track with a draggable knob.
  public static float Slider(Rect r,string label,float value)
  {
   int id=GUIUtility.GetControlID(FocusType.Passive,r);var e=Event.current;
   var track=new Rect(r.x+10,r.y+30,r.width-20,8);var hit=new Rect(r.x,r.y+18,r.width,r.height-14);
   if(GUI.enabled&&MobileTouchControls.TryConsumeTap(hit,out var touchPoint)){value=Mathf.Clamp01((touchPoint.x-track.x)/track.width);GUI.changed=true;NexoAudio.Play("ui_click",.5f);}
   switch(e.GetTypeForControl(id))
   {
    case EventType.MouseDown:if(GUI.enabled&&hit.Contains(e.mousePosition)&&e.button==0){GUIUtility.hotControl=id;value=Mathf.Clamp01((e.mousePosition.x-track.x)/track.width);GUI.changed=true;e.Use();}break;
    case EventType.MouseDrag:if(GUIUtility.hotControl==id){value=Mathf.Clamp01((e.mousePosition.x-track.x)/track.width);GUI.changed=true;e.Use();}break;
    case EventType.MouseUp:if(GUIUtility.hotControl==id){GUIUtility.hotControl=0;NexoAudio.Play("ui_click",.5f);e.Use();}break;
   }
   if(e.type==EventType.Repaint)
   {
    bool active=GUIUtility.hotControl==id||hit.Contains(e.mousePosition);
    NexoHud.Fill(r,Surface);Border(r,active?Teal:Edge);
    Write(new Rect(r.x+10,r.y+4,r.width-70,20),label,14,Text,TextAnchor.MiddleLeft,true);
    Write(new Rect(r.xMax-60,r.y+4,50,20),Mathf.RoundToInt(value*100)+"%",13,new Color(.62f,1f,.48f),TextAnchor.MiddleRight,true);
    NexoHud.Fill(track,new Color(.02f,.08f,.05f));
    NexoHud.Fill(new Rect(track.x,track.y,track.width*value,track.height),new Color(.1f,.75f,.25f));
    NexoHud.Fill(new Rect(track.x,track.y,track.width*value,2),new Color(.6f,1f,.65f,.7f));
    var knob=new Rect(track.x+track.width*value-7,track.y-5,14,18);NexoHud.Fill(knob,active?Color.white:new Color(.85f,.88f,.9f));NexoHud.Fill(new Rect(knob.x+2,knob.y+2,knob.width-4,knob.height-4),new Color(.35f,.95f,.45f));
   }
   return value;
  }
  // Square icon button (top-corner style): teal glass face, metal rim, orange corner brackets.
  public static bool IconButton(Rect r,string icon)
  {
   bool clicked=Interact(r,out bool hover,out bool pressed,out bool focused);bool lit=hover||focused;
   if(Event.current.type==EventType.Repaint)
   {
    NexoHud.Fill(new Rect(r.x+2,r.y+4,r.width,r.height),new Color(0,0,0,.45f));
    Chamfer(r,ChamferFill,new Color(.16f,.2f,.22f),12);
    var face=new Rect(r.x+6,r.y+6,r.width-12,r.height-12);
    Chamfer(face,ChamferFill,pressed?new Color(.03f,.22f,.24f):lit?new Color(.08f,.45f,.48f):new Color(.05f,.32f,.35f),9);
    Chamfer(face,ChamferRing,lit?new Color(.6f,1,1):new Color(.25f,.75f,.78f),9);
    NexoHud.Brackets(new Rect(r.x-3,r.y-3,r.width+6,r.height+6),new Color(1f,.58f,.12f),14,4);
    var t=Kit(icon);if(t){float s=Mathf.Min(face.width,face.height)*.66f;var old=GUI.color;GUI.color=lit?Color.white:new Color(.75f,.95f,.95f);GUI.DrawTexture(new Rect(face.center.x-s/2,face.center.y-s/2+(pressed?1:0),s,s),t,ScaleMode.ScaleToFit,true);GUI.color=old;}
   }
   return clicked;
  }
  // Tab / segmented option: dark teal plate; the active one is bright cyan (selected view).
  public static bool Button(Rect r,string label,bool active=false)
  {
   bool clicked=Interact(r,out bool hover,out bool pressed,out bool focused);bool enabled=GUI.enabled;bool lit=hover||focused;
   if(Event.current.type==EventType.Repaint)
   {
    if(focused)Chamfer(new Rect(r.x-3,r.y-3,r.width+6,r.height+6),ChamferRing,Teal,10);
    Chamfer(r,ChamferFill,new Color(.03f,.08f,.1f),9);
    var face=new Rect(r.x+2,r.y+2,r.width-4,r.height-4);
    Color body=active?(pressed?Cyan*.75f:Cyan):pressed?new Color(.05f,.14f,.17f):lit?new Color(.1f,.24f,.28f):new Color(.07f,.17f,.2f);body.a=1;
    Chamfer(face,ChamferFill,body,8);Chamfer(face,ChamferRing,active?new Color(.6f,.95f,1f):lit?Teal:new Color(.25f,.42f,.47f),8);
    NexoHud.Fill(new Rect(face.x+8,face.y+2,face.width-16,2),new Color(1,1,1,active?.25f:.08f));
    Write(new Rect(r.x+6,r.y+(pressed?1:0),r.width-12,r.height),label,r.height<36?12:14,!enabled?Muted*new Color(1,1,1,.6f):active?Color.white:new Color(.8f,.9f,.93f),TextAnchor.MiddleCenter,true);
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
  // Painted UI kit sprites (Resources/Nexoterra/UI/Kit), cached.
  static readonly System.Collections.Generic.Dictionary<string,Texture2D> kit=new System.Collections.Generic.Dictionary<string,Texture2D>();
  public static Texture2D Kit(string name){if(!kit.TryGetValue(name,out var t)||!t){t=Resources.Load<Texture2D>("Nexoterra/UI/Kit/"+name);kit[name]=t;}return t;}
  public static void DrawKit(Rect r,string name,float alpha=1){var t=Kit(name);if(!t||Event.current.type!=EventType.Repaint)return;var old=GUI.color;GUI.color=new Color(1,1,1,alpha);GUI.DrawTexture(r,t,ScaleMode.StretchToFill,true);GUI.color=old;}
  // Slug slot: painted canister (teal = occupied, steel = empty) with a round number socket below.
  public static void SlugCanister(Rect r,SlugDefinition d,bool selected,int number,float alpha=1,string label=null)
  {
   float labelH=label!=null?18:0,socket=Mathf.Min(28,r.width*.42f);
   float h=Mathf.Min(r.height-labelH-socket*.55f,r.width*1.15f),w=h/1.15f;
   var tube=new Rect(r.center.x-w/2,r.y,w,h);
   if(selected)Round(new Rect(tube.x-3,tube.y-3,tube.width+6,tube.height+6),new Color(1,.65f,.2f,.35f),10);
   DrawKit(tube,d!=null||selected?"Canister_Full":"Canister_Empty");
   var glass=new Rect(tube.x+tube.width*.14f,tube.y+tube.height*.2f,tube.width*.72f,tube.height*.6f);
   if(d!=null)Portrait(glass,d.portrait,Accent(d),alpha);
   else Write(glass,"+",20,new Color(.6f,.75f,.8f,.7f),TextAnchor.MiddleCenter,true);
   if(d!=null&&d.IsElemental)Write(new Rect(glass.xMax-14,glass.y,14,14),"E",10,Accent(d),TextAnchor.MiddleCenter,true);
   var sock=new Rect(r.center.x-socket/2,tube.yMax-socket*.45f,socket,socket);
   DrawKit(sock,selected?"Socket_Orange":"Socket_Grey");
   if(number>0)Write(sock,number.ToString(),Mathf.RoundToInt(socket*.42f),selected?new Color(.15f,.07f,0):Text,TextAnchor.MiddleCenter,true);
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
