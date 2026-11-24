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
  static readonly Color Green=new Color(.12f,.78f,.22f),GreenHot=new Color(.25f,.95f,.35f),GreenEdge=new Color(.5f,1f,.55f),Olive=new Color(.32f,.36f,.24f),Cyan=new Color(.12f,.72f,.88f);
  // Main action button: green chamfered plate with light top band and faint chevrons.
  // primary = slightly brighter. States: hover (brighter), pressed (darker, text down), disabled (olive), focused (cyan ring).
  public static bool Action(Rect r,string label,bool primary=false)
  {
   bool clicked=Interact(r,out bool hover,out bool pressed,out bool focused);bool enabled=GUI.enabled;bool lit=hover||focused;
   if(Event.current.type==EventType.Repaint)
   {
    NexoHud.Fill(new Rect(r.x+2,r.y+4,r.width,r.height),new Color(0,0,0,.4f));
    if(focused)Chamfer(new Rect(r.x-4,r.y-4,r.width+8,r.height+8),ChamferRing,Teal,14);
    Color body=!enabled?Olive:pressed?Green*.7f:lit?GreenHot:primary?Green*1.08f:Green;body.a=1;
    Chamfer(r,ChamferFill,new Color(.05f,.12f,.07f),12);                                   // dark rim
    var face=new Rect(r.x+3,r.y+3,r.width-6,r.height-6);
    Chamfer(face,ChamferFill,body,10);
    if(enabled)Chamfer(face,ChamferRing,lit?Color.white*.95f:GreenEdge,10);
    NexoHud.Fill(new Rect(face.x+10,face.y+3,face.width-20,Mathf.Max(2,face.height*.12f)),new Color(1,1,1,enabled?.18f:.06f));
    Chevrons(face,new Color(1,1,1,enabled?.07f:.04f));
    var text=new Rect(r.x+14,r.y+(pressed?2:0),r.width-28,r.height);
    int size=r.height<44?15:18;
    Write(new Rect(text.x+1,text.y+2,text.width,text.height),label,size,new Color(0,.12f,0,.75f),TextAnchor.MiddleCenter,true);
    Write(text,label,size,enabled?Color.white:new Color(.75f,.78f,.62f),TextAnchor.MiddleCenter,true);
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
