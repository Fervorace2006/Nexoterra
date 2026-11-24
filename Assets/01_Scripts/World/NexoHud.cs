using UnityEngine;
using System.Linq;
using System.Collections.Generic;
namespace Nexoterra.World
{
 public sealed class NexoHud : MonoBehaviour
 {
  public Texture2D malePortrait,femalePortrait;
  public static readonly Color Cyan=new Color(.1f,.85f,.9f),Gold=new Color(.95f,.57f,.12f),Ink=new Color(.015f,.045f,.06f,.95f);
  PlayerHealth health;NexoterraThirdPerson player;BossEncounters boss;float shown=1,trail=1,damageHold;
  void Update(){if(!health)return;float target=health.Health/health.maxHealth;if(target<shown-.01f)damageHold=Time.unscaledTime+.5f;shown=Mathf.MoveTowards(shown,target,Time.unscaledDeltaTime*2.5f);if(target>=trail)trail=target;else if(Time.unscaledTime>damageHold)trail=Mathf.MoveTowards(trail,target,Time.unscaledDeltaTime*.55f);}
  void Start(){health=GetComponent<PlayerHealth>();player=GetComponent<NexoterraThirdPerson>();boss=FindAnyObjectByType<BossEncounters>();}
  public static void Scale(){float s=Mathf.Min(Screen.width/1280f,Screen.height/720f);GUI.matrix=Matrix4x4.TRS(new Vector3((Screen.width-1280*s)*.5f,(Screen.height-720*s)*.5f,0),Quaternion.identity,new Vector3(s,s,1));}
  public static void Fill(Rect r,Color c){var old=GUI.color;GUI.color=c;GUI.DrawTexture(r,Texture2D.whiteTexture);GUI.color=old;}
  public static void Panel(Rect r)=>NexoUi.Panel(r);
  // ---- Lanzador HUD kit: gunmetal plates, teal glass insets and orange corner brackets ----
  public static readonly Color Metal=new Color(.16f,.2f,.23f),MetalDark=new Color(.06f,.08f,.1f),Glass=new Color(.03f,.22f,.27f),Orange=new Color(.98f,.66f,.22f); // same amber as NexoUi.Amber
  // Vertical gradient (top→bottom) drawn as stacked bands; cheap and pipeline-independent.
  public static void Gradient(Rect r,Color top,Color bottom)
  {
   int bands=Mathf.Clamp(Mathf.RoundToInt(r.height/4),3,24);float h=r.height/bands;
   for(int i=0;i<bands;i++)Fill(new Rect(r.x,r.y+i*h,r.width,h+1),Color.Lerp(top,bottom,i/(bands-1f)));
  }
  public static void Brackets(Rect r,Color c,float len=14,float t=3)
  {
   Fill(new Rect(r.x,r.y,len,t),c);Fill(new Rect(r.x,r.y,t,len),c);
   Fill(new Rect(r.xMax-len,r.y,len,t),c);Fill(new Rect(r.xMax-t,r.y,t,len),c);
   Fill(new Rect(r.x,r.yMax-t,len,t),c);Fill(new Rect(r.x,r.yMax-len,t,len),c);
   Fill(new Rect(r.xMax-len,r.yMax-t,len,t),c);Fill(new Rect(r.xMax-t,r.yMax-len,t,len),c);
  }
  // Raised metal plate with bevel, optional glass inset and orange brackets.
  public static Rect Plate(Rect r,bool glass=true,Color? bracket=null)
  {
   Fill(new Rect(r.x+2,r.y+3,r.width,r.height),new Color(0,0,0,.45f));
   Fill(r,new Color(.02f,.03f,.04f));
   Gradient(new Rect(r.x+1,r.y+1,r.width-2,r.height-2),new Color(.32f,.38f,.42f),MetalDark);
   Fill(new Rect(r.x+2,r.y+2,r.width-4,1),new Color(.6f,.68f,.72f,.55f));
   var inner=new Rect(r.x+5,r.y+5,r.width-10,r.height-10);
   if(glass){Fill(inner,new Color(.01f,.03f,.04f));Gradient(new Rect(inner.x+1,inner.y+1,inner.width-2,inner.height-2),new Color(.06f,.36f,.42f),new Color(.01f,.09f,.12f));Fill(new Rect(inner.x+1,inner.y+1,inner.width-2,1),new Color(.4f,.95f,1f,.35f));}
   Brackets(r,bracket??Orange,Mathf.Min(16,r.height*.4f),3);
   return inner;
  }
  // Thin vertical ticks, like a charge gauge.
  public static void Ticks(Rect r,float fraction,float trail,Color on,Color lost)
  {
   Fill(r,new Color(.01f,.02f,.025f));
   int count=Mathf.Max(4,Mathf.FloorToInt((r.width-2)/5f));float w=(r.width-2)/count;
   for(int i=0;i<count;i++){float p=(i+.5f)/count;var cell=new Rect(r.x+1+i*w+1,r.y+2,w-2,r.height-4);
    Color c=p<=fraction?on:p<=trail?lost:new Color(.08f,.12f,.13f);Fill(cell,c);if(p<=fraction)Fill(new Rect(cell.x,cell.y,cell.width,2),Color.Lerp(on,Color.white,.55f));}
  }
  // One life bar for everyone (player, enemies, overhead): dark frame + segmented ticks.
  public static readonly Color PlayerLife=new Color(.3f,.95f,.35f),EnemyLife=new Color(.95f,.18f,.16f);
  public static void LifeBar(Rect r,float fraction,float trail,Color color)
  {
   Fill(new Rect(r.x-2,r.y-2,r.width+4,r.height+4),new Color(0,0,0,.6f));
   Fill(new Rect(r.x-1,r.y-1,r.width+2,r.height+2),new Color(.35f,.42f,.46f));
   Ticks(r,Mathf.Clamp01(fraction),Mathf.Clamp01(trail),color,new Color(1,.55f,.15f));
  }
  // Holographic vertical cells (painted sprites) filling a gauge: lit = life, orange = recent damage, dim = empty.
  static void Bars(Rect r,float fraction,float trail,Color on)
  {
   if(Event.current.type!=EventType.Repaint)return;fraction=Mathf.Clamp01(fraction);trail=Mathf.Clamp01(trail);
   float w=Mathf.Max(3,r.height*.32f),gap=2;int n=Mathf.Max(1,Mathf.FloorToInt((r.width+gap)/(w+gap)));float x=r.x+(r.width-(n*w+(n-1)*gap))*.5f;
   Color dim=new Color(on.r*.18f,on.g*.18f,on.b*.18f,1),lit=Color.Lerp(on,Color.white,.35f);
   for(int i=0;i<n;i++,x+=w+gap){float p=(i+.5f)/n;var b=new Rect(x,r.y,w,r.height);
    if(p<=fraction){Fill(b,on);Fill(new Rect(b.x,b.y,b.width,Mathf.Max(1,b.height*.35f)),lit);}
    else if(p<=trail)Fill(b,new Color(1,.55f,.15f));else Fill(b,dim);}
  }
  public static void Cells(Rect r,float fraction,float trail,string lit)
  {
   if(Event.current.type!=EventType.Repaint)return;
   float cw=r.height*.36f,gap=Mathf.Max(1,r.height*.06f);int n=Mathf.Max(1,Mathf.FloorToInt((r.width+gap)/(cw+gap)));
   float x=r.x+(r.width-(n*cw+(n-1)*gap))*.5f;fraction=Mathf.Clamp01(fraction);trail=Mathf.Clamp01(trail);
   for(int i=0;i<n;i++,x+=cw+gap){float p=(i+.5f)/n;var cell=new Rect(x,r.y,cw,r.height);
    if(p<=fraction)NexoUi.DrawKit(cell,lit);else if(p<=trail)NexoUi.DrawKit(cell,"Cell_Orange",.9f);else NexoUi.DrawKit(cell,"Cell_Grey",.28f);}
  }
  public static void PortraitFrame(Rect r,Texture2D portrait,Color accent)
  {
   NexoUi.DrawKit(r,"Frame_Orange");   // frame first: its center is opaque
   NexoUi.Portrait(new Rect(r.x+r.width*.13f,r.y+r.height*.13f,r.width*.74f,r.height*.74f),portrait,accent);
  }
  // Painted metal bar frame with the segmented gauge inside (player / enemy bars).
  public static void FramedLifeBar(Rect r,float fraction,float trail,Color color)
  {
   NexoUi.DrawKit(r,"Bar_Long");
   // Reference look: dark groove with thin bright vertical bars.
   var groove=new Rect(r.x+r.width*.065f,r.y+r.height*.32f,r.width*.855f,r.height*.36f);Fill(groove,new Color(.01f,.03f,.02f));
   Bars(new Rect(groove.x+2,groove.y+2,groove.width-4,groove.height-4),fraction,trail,color);
  }
  public static void Shadowed(Rect r,string text,int size,Color color,TextAnchor anchor=TextAnchor.MiddleLeft)
  {
   Label(new Rect(r.x+1,r.y+1,r.width,r.height),text,size,new Color(0,0,0,.8f),anchor);Label(r,text,size,color,anchor);
  }
  // Keyboard key cap (light metal key, dark legend). Returns the x right after the key.
  public static float KeyCap(float x,float y,string key,float h=26)
  {
   float w=Mathf.Max(h,14+key.Length*8.5f);
   Fill(new Rect(x+1,y+3,w,h),new Color(0,0,0,.55f));
   Fill(new Rect(x,y,w,h),new Color(.32f,.36f,.4f));
   Gradient(new Rect(x+1,y+1,w-2,h-5),new Color(.93f,.96f,.98f),new Color(.66f,.72f,.76f));
   Fill(new Rect(x+1,y+h-4,w-2,3),new Color(.42f,.47f,.5f));
   Label(new Rect(x,y-1,w,h-3),key,key.Length>2?11:13,new Color(.06f,.09f,.12f),TextAnchor.MiddleCenter);
   return x+w;
  }
  // One control line: keys ("+" and "/" act as separators) followed by a description.
  public static void ControlRow(float x,float y,string description,params string[] keys)
  {
   foreach(var k in keys){
    if(k=="+"||k=="/"){Label(new Rect(x,y,14,26),k,13,NexoUi.Muted,TextAnchor.MiddleCenter);x+=14;continue;}
    x=KeyCap(x,y,k)+4;
   }
   Label(new Rect(x+4,y,220,26),description,13,NexoUi.Text,TextAnchor.MiddleLeft);
  }
  static Texture2D vignette;static GUIStyle field;
  // Full-screen menu backdrop: key art cropped to fill, deep-water tint, vignette and slow caustic bands.
  public static void Backdrop(Texture2D art)
  {
   var screen=new Rect(0,0,Screen.width,Screen.height);
   Fill(screen,new Color(.004f,.02f,.03f));
   if(art){var old=GUI.color;GUI.color=new Color(.55f,.7f,.75f);GUI.DrawTexture(screen,art,ScaleMode.ScaleAndCrop);GUI.color=old;}
   Fill(screen,new Color(.0f,.05f,.07f,.62f));
   float t=Time.unscaledTime;
   for(int i=0;i<7;i++){float y=Screen.height*(.12f*i+.04f)+Mathf.Sin(t*.35f+i*1.7f)*Screen.height*.025f;Fill(new Rect(0,y,Screen.width,Screen.height*.012f),new Color(.2f,.9f,.95f,.025f));}
   if(!vignette){vignette=new Texture2D(64,64,TextureFormat.RGBA32,false){wrapMode=TextureWrapMode.Clamp};for(int y=0;y<64;y++)for(int x=0;x<64;x++){float d=Vector2.Distance(new Vector2(x,y),new Vector2(31.5f,31.5f))/45f;vignette.SetPixel(x,y,new Color(0,0,0,Mathf.SmoothStep(0,.85f,d)));}vignette.Apply();}
   GUI.DrawTexture(screen,vignette);
  }
  public static string Field(Rect r,string value,int max)
  {
   if(field==null){field=new GUIStyle(GUI.skin.textField){fontSize=19,alignment=TextAnchor.MiddleLeft,padding=new RectOffset(14,14,6,6)};foreach(var s in new[]{field.normal,field.hover,field.focused,field.active}){s.background=Texture2D.whiteTexture;}field.normal.textColor=field.hover.textColor=NexoUi.Text;field.focused.textColor=field.active.textColor=Color.white;}
   bool focus=GUI.enabled&&r.Contains(Event.current.mousePosition);
   Fill(r,focus?NexoUi.Teal:NexoUi.Edge);
   var old=GUI.backgroundColor;GUI.backgroundColor=GUI.enabled?new Color(.03f,.075f,.095f):new Color(.04f,.06f,.07f);
   string result=GUI.TextField(new Rect(r.x+1,r.y+1,r.width-2,r.height-2),value,max,field);
   GUI.backgroundColor=old;Fill(new Rect(r.x,r.yMax-2,r.width,2),focus?NexoUi.Teal:NexoUi.Edge*1.5f);
   return result;
  }
  static readonly System.Collections.Generic.Dictionary<int,GUIStyle> labelStyles=new System.Collections.Generic.Dictionary<int,GUIStyle>();
  public static void Label(Rect r,string value,int size,Color color,TextAnchor alignment=TextAnchor.UpperLeft){int key=size*16+(int)alignment;if(!labelStyles.TryGetValue(key,out var style)){style=new GUIStyle(GUI.skin.label){fontSize=size,fontStyle=size>=22?FontStyle.Bold:FontStyle.Normal,alignment=alignment,wordWrap=true};labelStyles[key]=style;}style.normal.textColor=color;GUI.Label(r,value,style);}
  // Menu buttons come from the shared UI kit (states + keyboard/gamepad focus).
  public static bool Button(Rect r,string title)=>NexoUi.Action(r,title);
  public static bool PrimaryButton(Rect r,string title)=>NexoUi.Action(r,title,true);
  public static void HealthBar(Rect r,float fraction,Color color)
  {
   Panel(r);var inner=new Rect(r.x+12,r.y+10,r.width-24,r.height-20);int segments=32;float w=inner.width/segments;
   for(int i=0;i<segments;i++){var cell=new Rect(inner.x+i*w,inner.y,w-2,inner.height);Fill(cell,i/(float)segments<Mathf.Clamp01(fraction)?color:new Color(.035f,.08f,.08f));if(i/(float)segments<fraction)Fill(new Rect(cell.x+1,cell.y+2,Mathf.Max(1,cell.width*.3f),cell.height-4),new Color(.8f,1,1,.25f));}
  }
  readonly Dictionary<EnemyCombatant,float> combatants=new Dictionary<EnemyCombatant,float>();
  float nextCombatScan;
  void DrawOpponents(){
   if(Time.unscaledTime>=nextCombatScan){
    nextCombatScan=Time.unscaledTime+.3f;
    foreach(var enemy in FindObjectsByType<EnemyCombatant>(FindObjectsSortMode.None)){
     var ai=enemy.GetComponent<EnemyShooter>();
     if(!enemy.IsDead&&((ai&&ai.IsPursuing)||Time.time-enemy.LastDamageTime<8))combatants[enemy]=Time.time;
    }
    foreach(var enemy in combatants.Keys.ToArray())if(!enemy||enemy.IsDead||Time.time-combatants[enemy]>8)combatants.Remove(enemy);
   }
   var targets=combatants.Keys.Where(e=>e&&!e.IsDead).OrderBy(e=>(e.transform.position-player.transform.position).sqrMagnitude).Take(4).ToArray();
   var red=EnemyLife;
   for(int i=0;i<targets.Length;i++){
    var e=targets[i];float fraction=e.Health/Mathf.Max(1,e.maxHealth);
    if(i==0){
     // Main target mirrors the player block: bar on the left, portrait on the right.
     PortraitFrame(new Rect(1166,14,98,98),e.portrait,red);
     var plate=new Rect(826,20,340,52);FramedLifeBar(plate,fraction,fraction,red);
     Shadowed(new Rect(846,72,316,20),e.displayName.ToUpperInvariant(),15,Color.white,TextAnchor.MiddleRight);
     Shadowed(new Rect(846,92,316,16),Mathf.CeilToInt(e.Health)+" / "+e.maxHealth+" PV",11,new Color(1,.75f,.7f),TextAnchor.MiddleRight);
    }else{
     float y=124+(i-1)*42;var plate=Plate(new Rect(1030,y,234,36),false,new Color(.7f,.35f,.1f));
     Label(new Rect(plate.x+4,plate.y,140,plate.height),e.displayName,11,NexoUi.Text,TextAnchor.MiddleLeft);
     LifeBar(new Rect(plate.x+140,plate.y+9,plate.width-150,plate.height-18),fraction,fraction,red);
    }
   }
  }
  void OnGUI()
  {
   if(GameSession.Blocked||!health||!player||GetComponent<SlugInventory>().IsOpen)return;var old=GUI.matrix;Scale();
   // Player block (top-left): framed portrait + segmented life gauge.
   bool critical=shown<.25f;
   PortraitFrame(new Rect(16,14,98,98),player.female.activeSelf?femalePortrait:malePortrait,Cyan);
   var plate=new Rect(114,20,340,52);
   Color life=critical?new Color(1,.25f,.2f):PlayerLife;
   if(critical&&Mathf.Repeat(Time.unscaledTime,.8f)<.4f)life=Color.Lerp(life,Color.white,.35f);
   FramedLifeBar(plate,shown,trail,life);
   NexoUi.DrawKit(new Rect(122,74,16,14),"Icon_Heart");Shadowed(new Rect(142,72,292,16),health.IsDead?"DERROTADO":Mathf.CeilToInt(health.Health)+" / "+health.maxHealth+" PV"+(critical?"  ·  VIDA CRÍTICA":""),11,critical?new Color(1,.5f,.45f):new Color(.7f,.95f,.9f));
   DrawOpponents();
   if(boss&&boss.CampaignComplete){NexoUi.Card(new Rect(350,115,580,85),Gold);Label(new Rect(365,130,550,60),"HISTORIA COMPLETADA\nEsc · guardar partida",22,Cyan,TextAnchor.MiddleCenter);}
   GUI.matrix=old;
  }
 }
}
