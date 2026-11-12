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
  static readonly System.Collections.Generic.Dictionary<int,GUIStyle> labelStyles=new System.Collections.Generic.Dictionary<int,GUIStyle>();
  public static void Label(Rect r,string value,int size,Color color,TextAnchor alignment=TextAnchor.UpperLeft){int key=size*16+(int)alignment;if(!labelStyles.TryGetValue(key,out var style)){style=new GUIStyle(GUI.skin.label){fontSize=size,fontStyle=size>=22?FontStyle.Bold:FontStyle.Normal,alignment=alignment,wordWrap=true};labelStyles[key]=style;}style.normal.textColor=color;GUI.Label(r,value,style);}
  public static bool Button(Rect r,string title)
  {
   Panel(r);Fill(new Rect(r.x+10,r.y+10,r.width-20,r.height-20),GUI.enabled?new Color(.015f,.24f,.13f):new Color(.08f,.12f,.12f));
   for(float y=r.y+13;y<r.yMax-10;y+=4)Fill(new Rect(r.x+12,y,r.width-24,1),new Color(.1f,.8f,.5f,.1f));
   Label(r,title,22,GUI.enabled?new Color(.25f,1,.55f):Color.gray,TextAnchor.MiddleCenter);return GUI.Button(r,GUIContent.none,GUIStyle.none);
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
   var targets=combatants.Keys.Where(e=>e&&!e.IsDead).OrderBy(e=>(e.transform.position-player.transform.position).sqrMagnitude).ToArray();
   float rowHeight=Mathf.Min(100,500f/Mathf.Max(1,targets.Length));
   for(int i=0;i<targets.Length;i++){
    var e=targets[i];float y=20+i*rowHeight;float h=Mathf.Min(90,rowHeight-4);float face=h-10;
    var accent=new Color(.95f,.18f,.2f);NexoUi.Card(new Rect(906,y,354,h),accent);
    NexoUi.Card(new Rect(1254-face,y+5,face,face),Cyan);
    if(e.portrait)NexoUi.Portrait(new Rect(1256-face,y+7,face-4,face-4),e.portrait,Cyan);
    Label(new Rect(918,y+6,325-face,22),e.displayName,14,NexoUi.Text);
    var bar=new Rect(918,y+h*.4f,325-face,Mathf.Max(7,h*.22f));
    Fill(bar,new Color(.12f,.025f,.035f));float fraction=e.Health/Mathf.Max(1,e.maxHealth);
    for(int n=0;n<24;n++){float width=bar.width/24;var segment=new Rect(bar.x+n*width,bar.y,width-2,bar.height);float fill=Mathf.Clamp01(fraction*24-n);if(fill>0){segment.width*=fill;Fill(segment,accent);Fill(new Rect(segment.x,segment.y,segment.width,2),new Color(1,.6f,.5f));}}
    Label(new Rect(918,y+h-23,325-face,19),Mathf.CeilToInt(e.Health)+" / "+e.maxHealth+" PV",12,NexoUi.Text,TextAnchor.MiddleRight);
   }
  }
  void OnGUI()
  {
   if(GameSession.Blocked||!health||!player||GetComponent<SlugInventory>().IsOpen)return;var old=GUI.matrix;Scale();
   var box=new Rect(20,596,336,108);NexoUi.Card(box,Cyan);NexoUi.Card(new Rect(28,604,86,92),Cyan);
   NexoUi.Portrait(new Rect(30,606,82,88),player.female.activeSelf?femalePortrait:malePortrait,Cyan);
   Label(new Rect(126,607,210,22),player.female.activeSelf?"COMANDO":"JUGADOR",17,NexoUi.Text);
   Label(new Rect(126,633,190,18),health.IsDead?"DERROTADO":shown<.25f?"VIDA CRÍTICA":"VITALIDAD",11,shown<.25f?new Color(1,.4f,.35f):NexoUi.Muted);
   var bar=new Rect(126,657,212,17);Fill(bar,new Color(.07f,.13f,.17f));Fill(new Rect(bar.x,bar.y,bar.width*trail,bar.height),new Color(.8f,.4f,.18f));
   Color color=shown<.25f?new Color(1,.27f,.22f):new Color(.25f,.88f,.55f);Fill(new Rect(bar.x,bar.y,bar.width*shown,bar.height),color);Fill(new Rect(bar.x,bar.y,bar.width*shown,3),Color.Lerp(color,Color.white,.5f));
   for(int i=1;i<10;i++)Fill(new Rect(bar.x+i*bar.width/10,bar.y,1,bar.height),new Color(.02f,.08f,.09f,.65f));
   Label(new Rect(126,679,210,17),Mathf.CeilToInt(health.Health)+" / "+health.maxHealth+" PV",12,NexoUi.Text,TextAnchor.MiddleRight);
   DrawOpponents();
   if(boss&&boss.CampaignComplete){NexoUi.Card(new Rect(350,115,580,85),Gold);Label(new Rect(365,130,550,60),"HISTORIA COMPLETADA\nEsc · guardar partida",22,Cyan,TextAnchor.MiddleCenter);}
   GUI.matrix=old;
  }
 }
}
