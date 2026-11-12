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
  public static void Panel(Rect r)
  {
   Fill(r,Gold);Fill(new Rect(r.x+3,r.y+3,r.width-6,r.height-6),new Color(.14f,.23f,.25f));Fill(new Rect(r.x+8,r.y+8,r.width-16,r.height-16),Ink);
   Fill(new Rect(r.x+14,r.y+12,r.width-28,2),Cyan*.6f);
   foreach(float x in new[]{r.x,r.xMax-13})foreach(float y in new[]{r.y,r.yMax-13})Fill(new Rect(x,y,13,13),Gold);
  }
  // ---- Lanzador HUD kit: gunmetal plates, teal glass insets and orange corner brackets ----
  public static readonly Color Metal=new Color(.16f,.2f,.23f),MetalDark=new Color(.06f,.08f,.1f),Glass=new Color(.03f,.22f,.27f),Orange=new Color(1f,.55f,.08f);
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
  public static void PortraitFrame(Rect r,Texture2D portrait,Color accent)
  {
   var inner=Plate(r,true,Orange);NexoUi.Portrait(inner,portrait,accent);
   Fill(new Rect(inner.x,inner.yMax-3,inner.width,3),accent*new Color(1,1,1,.8f));
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
   bool focus=r.Contains(Event.current.mousePosition);
   Fill(r,focus?Cyan:NexoUi.Edge);
   var old=GUI.backgroundColor;GUI.backgroundColor=new Color(.02f,.07f,.09f);
   string result=GUI.TextField(new Rect(r.x+2,r.y+2,r.width-4,r.height-4),value,max,field);
   GUI.backgroundColor=old;Fill(new Rect(r.x,r.yMax-2,r.width,2),Gold*new Color(1,1,1,.8f));
   return result;
  }
  static readonly System.Collections.Generic.Dictionary<int,GUIStyle> labelStyles=new System.Collections.Generic.Dictionary<int,GUIStyle>();
  public static void Label(Rect r,string value,int size,Color color,TextAnchor alignment=TextAnchor.UpperLeft){int key=size*16+(int)alignment;if(!labelStyles.TryGetValue(key,out var style)){style=new GUIStyle(GUI.skin.label){fontSize=size,fontStyle=size>=22?FontStyle.Bold:FontStyle.Normal,alignment=alignment,wordWrap=true};labelStyles[key]=style;}style.normal.textColor=color;GUI.Label(r,value,style);}
  public static bool Button(Rect r,string title)
  {
   bool hover=GUI.enabled&&r.Contains(Event.current.mousePosition);
   Color edge=GUI.enabled?(hover?Cyan:Gold):new Color(.25f,.3f,.32f);
   Fill(new Rect(r.x+2,r.y+4,r.width,r.height),new Color(0,0,0,.5f));
   Fill(r,edge);Fill(new Rect(r.x+2,r.y+2,r.width-4,r.height-4),new Color(.15f,.22f,.24f));
   Fill(new Rect(r.x+6,r.y+6,r.width-12,r.height-12),hover?new Color(.025f,.28f,.21f):new Color(.015f,.12f,.105f));
   Fill(new Rect(r.x+12,r.y+9,r.width-24,2),new Color(.22f,.95f,.65f,hover?.8f:.35f));
   for(float y=r.y+15;y<r.yMax-10;y+=5)Fill(new Rect(r.x+10,y,r.width-20,1),new Color(.1f,.7f,.45f,.08f));
   foreach(float x in new[]{r.x,r.xMax-9})foreach(float y in new[]{r.y,r.yMax-9})Fill(new Rect(x,y,9,9),edge);
   Label(new Rect(r.x+12,r.y,r.width-24,r.height),title,r.width<320?18:21,GUI.enabled?new Color(.7f,1,.83f):NexoUi.Muted,TextAnchor.MiddleCenter);
   return GUI.Button(r,GUIContent.none,GUIStyle.none);
  }
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
   var red=new Color(.92f,.12f,.12f);
   for(int i=0;i<targets.Length;i++){
    var e=targets[i];float fraction=e.Health/Mathf.Max(1,e.maxHealth);
    if(i==0){
     // Main target mirrors the player block: bar on the left, portrait on the right.
     PortraitFrame(new Rect(1166,14,98,98),e.portrait,red);
     var plate=Plate(new Rect(846,24,316,44),false);Ticks(new Rect(plate.x+4,plate.y+4,plate.width-8,plate.height-8),fraction,fraction,red,new Color(1,.6f,.3f));
     Shadowed(new Rect(846,72,316,20),e.displayName.ToUpperInvariant(),15,Color.white,TextAnchor.MiddleRight);
     Shadowed(new Rect(846,92,316,16),Mathf.CeilToInt(e.Health)+" / "+e.maxHealth+" PV",11,new Color(1,.75f,.7f),TextAnchor.MiddleRight);
    }else{
     float y=124+(i-1)*42;var plate=Plate(new Rect(1030,y,234,36),false,new Color(.7f,.35f,.1f));
     Label(new Rect(plate.x+4,plate.y,140,plate.height),e.displayName,11,NexoUi.Text,TextAnchor.MiddleLeft);
     Ticks(new Rect(plate.x+140,plate.y+7,plate.width-146,plate.height-14),fraction,fraction,red,red);
    }
   }
  }
  void OnGUI()
  {
   if(GameSession.Blocked||!health||!player||GetComponent<SlugInventory>().IsOpen)return;var old=GUI.matrix;Scale();
   // Player block (top-left): framed portrait + segmented life gauge.
   bool critical=shown<.25f;
   PortraitFrame(new Rect(16,14,98,98),player.female.activeSelf?femalePortrait:malePortrait,Cyan);
   var plate=Plate(new Rect(118,24,316,44),false);
   Color life=critical?new Color(1,.25f,.2f):new Color(.3f,.95f,.35f);
   if(critical&&Mathf.Repeat(Time.unscaledTime,.8f)<.4f)life=Color.Lerp(life,Color.white,.35f);
   Ticks(new Rect(plate.x+4,plate.y+4,plate.width-8,plate.height-8),shown,trail,life,new Color(1,.55f,.15f));
   Shadowed(new Rect(122,72,200,20),player.female.activeSelf?"COMANDO":"LANZADOR",15,Color.white);
   Shadowed(new Rect(122,92,312,16),health.IsDead?"DERROTADO":Mathf.CeilToInt(health.Health)+" / "+health.maxHealth+" PV"+(critical?"  ·  VIDA CRÍTICA":""),11,critical?new Color(1,.5f,.45f):new Color(.7f,.95f,.9f));
   DrawOpponents();
   if(boss&&boss.CampaignComplete){NexoUi.Card(new Rect(350,115,580,85),Gold);Label(new Rect(365,130,550,60),"HISTORIA COMPLETADA\nEsc · guardar partida",22,Cyan,TextAnchor.MiddleCenter);}
   GUI.matrix=old;
  }
 }
}
