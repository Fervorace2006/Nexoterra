using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering.Universal;
namespace Nexoterra.World
{
 // HUD helpers for finding the remaining enemies:
 //  • a red arrow over every living enemy's head (clamped to the screen edge, pointing at it, when off screen);
 //  • a minimap (top-down camera, rotates with the view) with a radar sweep and red enemy blips.
 public sealed class EnemyRadar : MonoBehaviour
 {
  const float MapRange=42f;            // metres from the centre to the map edge
  static readonly Rect MapRect=new Rect(16,128,176,176);
  NexoterraThirdPerson player;SlugInventory inventory;PlayerHealth health;
  readonly List<EnemyCombatant> enemies=new List<EnemyCombatant>();float nextScan;
  Camera mapCamera;RenderTexture mapTexture;
  static Texture2D arrow,dot,ring,heading;
  void Start(){player=GetComponent<NexoterraThirdPerson>();inventory=GetComponent<SlugInventory>();health=GetComponent<PlayerHealth>();}
  void Scan()
  {
   enemies.Clear();var level=CampaignLevel.Current;
   IEnumerable<EnemyCombatant> source=level&&level.enemies!=null&&level.enemies.Length>0?level.enemies:FindObjectsByType<EnemyCombatant>(FindObjectsSortMode.None);
   foreach(var e in source)if(e&&e.gameObject.activeInHierarchy&&!e.IsDead)enemies.Add(e);
  }
  void LateUpdate()
  {
   if(Time.unscaledTime>=nextScan){nextScan=Time.unscaledTime+.4f;Scan();}
   if(!player)return;
   if(!mapCamera)
   {
    mapTexture=new RenderTexture(256,256,16){name="Minimapa"};
    var go=new GameObject("Cámara del minimapa");go.transform.SetParent(transform,false);
    mapCamera=go.AddComponent<Camera>();mapCamera.orthographic=true;mapCamera.orthographicSize=MapRange;mapCamera.targetTexture=mapTexture;
    mapCamera.clearFlags=CameraClearFlags.SolidColor;mapCamera.backgroundColor=new Color(.02f,.06f,.07f);mapCamera.nearClipPlane=.3f;mapCamera.farClipPlane=60;
    mapCamera.cullingMask=~((1<<5)|(1<<2));mapCamera.depth=-50;   // skip UI and the cave shell (layer 2) so the roof never covers the map
    var data=go.AddComponent<UniversalAdditionalCameraData>();data.renderPostProcessing=false;data.renderShadows=false;
   }
   // Below cave roofs (y≈25) and above the player, rotated with the view so "up" is where you look.
   float yaw=Camera.main?Camera.main.transform.eulerAngles.y:player.transform.eulerAngles.y;
   mapCamera.transform.position=player.transform.position+Vector3.up*18;
   mapCamera.transform.rotation=Quaternion.Euler(90,0,0);   // north-up; the player arrow shows the view direction
   mapCamera.enabled=!GameSession.Blocked;
  }
  void OnDestroy(){if(mapTexture)mapTexture.Release();}
  bool Hidden=>GameSession.Blocked||!player||(inventory&&inventory.IsOpen)||(health&&health.IsDead);
  void OnGUI()
  {
   if(Hidden||Event.current.type!=EventType.Repaint)return;
   EnsureTextures();
   DrawHeadArrows();
   var old=GUI.matrix;NexoHud.Scale();DrawMinimap();GUI.matrix=old;
  }
  // ---- Arrows over enemy heads (screen space, unscaled) ----
  void DrawHeadArrows()
  {
   var cam=Camera.main;if(!cam)return;
   float t=Time.unscaledTime;
   foreach(var e in enemies)
   {
    if(!e||e.IsDead)continue;
    var body=e.GetComponent<CharacterController>();float h=body?body.height+.9f:2.9f;
    Vector3 world=e.transform.position+Vector3.up*h;var p=cam.WorldToScreenPoint(world);
    float dist=Vector3.Distance(cam.transform.position,world);
    bool boss=e.GetComponent<EnemyShooter>()&&e.GetComponent<EnemyShooter>().boss;
    Color red=new Color(1,.12f,.1f,1);
    bool onScreen=p.z>0&&p.x>20&&p.x<Screen.width-20&&p.y>20&&p.y<Screen.height-20;
    if(onScreen)
    {
     float size=Mathf.Clamp(480f/dist,9,boss?20:15)*(Screen.height/720f);
     float bob=Mathf.Sin(t*4+e.GetHashCode())*size*.15f;
     var r=new Rect(p.x-size/2,Screen.height-p.y-size+bob-3,size,size);
     Draw(arrow,r,red);
    }
    else
    {
     // Edge indicator pointing toward the enemy.
     Vector2 c=new Vector2(Screen.width/2f,Screen.height/2f);
     Vector2 d=new Vector2(p.x,p.y)-c;if(p.z<0)d=-d;if(d.sqrMagnitude<1)d=Vector2.down;
     d.Normalize();float mx=Screen.width/2f-40,my=Screen.height/2f-40;
     float k=Mathf.Min(mx/Mathf.Max(.001f,Mathf.Abs(d.x)),my/Mathf.Max(.001f,Mathf.Abs(d.y)));
     Vector2 pos=c+d*k;pos.y=Screen.height-pos.y;
     float size=15*(Screen.height/720f);var r=new Rect(pos.x-size/2,pos.y-size/2,size,size);
     float angle=Mathf.Atan2(-d.y,d.x)*Mathf.Rad2Deg-90;   // arrow texture points down
     var m=GUI.matrix;GUIUtility.RotateAroundPivot(angle,pos);Draw(arrow,r,new Color(1,.12f,.1f,.9f));GUI.matrix=m;
    }
   }
  }
  // ---- Minimap (in the 1280×720 HUD space). North-up; the cyan arrow in the centre is the player,
  // rotated to where the camera looks. Red dots = enemies (boss larger/orange), green = exit. ----
  void DrawMinimap()
  {
   var r=MapRect;var inner=new Rect(r.x+7,r.y+7,r.width-14,r.height-14);
   // metal frame
   NexoHud.Fill(new Rect(r.x+3,r.y+5,r.width,r.height),new Color(0,0,0,.5f));
   NexoHud.Fill(r,new Color(.02f,.03f,.04f));
   NexoHud.Gradient(new Rect(r.x+1,r.y+1,r.width-2,r.height-2),new Color(.3f,.36f,.4f),NexoHud.MetalDark);
   NexoHud.Fill(new Rect(r.x+2,r.y+2,r.width-4,1),new Color(.65f,.72f,.76f,.6f));
   NexoHud.Fill(new Rect(inner.x-1,inner.y-1,inner.width+2,inner.height+2),new Color(.01f,.03f,.04f));
   // map image
   if(mapTexture){var o=GUI.color;GUI.color=new Color(.8f,.95f,.95f,1);GUI.DrawTexture(inner,mapTexture,ScaleMode.StretchToFill,false);GUI.color=o;}
   NexoHud.Fill(inner,new Color(0,.16f,.18f,.25f));
   // range rings
   Draw(ring,new Rect(inner.x+2,inner.y+2,inner.width-4,inner.height-4),new Color(.35f,1,.95f,.35f));
   Draw(ring,new Rect(inner.center.x-inner.width*.25f,inner.center.y-inner.height*.25f,inner.width*.5f,inner.height*.5f),new Color(.35f,1,.95f,.22f));
   float half=inner.width/2-6,scale=(inner.width/2)/MapRange,pulse=.8f+.2f*Mathf.Sin(Time.unscaledTime*6);
   Vector2 ToMap(Vector3 world,out bool edge){var d=world-player.transform.position;var v=new Vector2(d.x,-d.z)*scale;edge=v.magnitude>half;if(edge)v=v.normalized*half;return inner.center+v;}
   foreach(var e in enemies)
   {
    if(!e||e.IsDead)continue;var p=ToMap(e.transform.position,out bool edge);
    bool boss=e.GetComponent<EnemyShooter>()&&e.GetComponent<EnemyShooter>().boss;float s=(boss?13:9)*(edge?.75f:pulse);
    Draw(dot,new Rect(p.x-s/2-1,p.y-s/2-1,s+2,s+2),new Color(0,0,0,.6f));
    Draw(dot,new Rect(p.x-s/2,p.y-s/2,s,s),boss?new Color(1,.45f,.1f):new Color(1,.15f,.12f));
   }
   var arena=ArenaSession.Current;int rivalCount=0;
   if(arena)foreach(var rival in arena.RivalPositions())
   {
    rivalCount++;var p=ToMap(rival.Value,out bool edge);float s=edge?8:11;
    Draw(dot,new Rect(p.x-s/2-2,p.y-s/2-2,s+4,s+4),new Color(0,0,0,.8f));
    Draw(dot,new Rect(p.x-s/2,p.y-s/2,s,s),new Color(1,.12f,.16f));
   }
   var level=CampaignLevel.Current;
   if(level&&level.exit){var p=ToMap(level.exit.position,out _);Draw(dot,new Rect(p.x-6,p.y-6,12,12),new Color(0,0,0,.6f));Draw(dot,new Rect(p.x-5,p.y-5,10,10),new Color(.35f,1,.45f));}
   // compass letters
   var lc=new Color(.85f,.95f,.95f,.9f);
   NexoHud.Shadowed(new Rect(inner.center.x-8,inner.y,16,14),"N",11,new Color(1,.7f,.3f),TextAnchor.MiddleCenter);
   NexoHud.Shadowed(new Rect(inner.center.x-8,inner.yMax-14,16,14),"S",10,lc,TextAnchor.MiddleCenter);
   NexoHud.Shadowed(new Rect(inner.x+1,inner.center.y-7,14,14),"O",10,lc,TextAnchor.MiddleCenter);
   NexoHud.Shadowed(new Rect(inner.xMax-15,inner.center.y-7,14,14),"E",10,lc,TextAnchor.MiddleCenter);
   // player arrow: rotate in HUD space around the map centre (texture points down → +180°)
   float yaw=Camera.main?Camera.main.transform.eulerAngles.y:player.transform.eulerAngles.y;
   var saved=GUI.matrix;var c=new Vector3(inner.center.x,inner.center.y,0);
   GUI.matrix=saved*Matrix4x4.TRS(c,Quaternion.Euler(0,0,180+yaw),Vector3.one)*Matrix4x4.TRS(-c,Quaternion.identity,Vector3.one);
   var pr=new Rect(inner.center.x-12,inner.center.y-12,24,24);
   Draw(heading,new Rect(pr.x-3,pr.y-3,pr.width+6,pr.height+6),new Color(0,0,0,.8f));Draw(heading,pr,new Color(.35f,1,1));
   GUI.matrix=saved;
   NexoHud.Brackets(new Rect(r.x-3,r.y-3,r.width+6,r.height+6),NexoHud.Orange,18,4);
   int tracked=arena?rivalCount:enemies.Count;string status=arena?"JUGADORES: "+rivalCount:tracked>0?"ENEMIGOS: "+tracked:"ZONA DESPEJADA";
   NexoHud.Shadowed(new Rect(r.x,r.yMax+3,r.width,16),status,11,tracked>0?new Color(1,.6f,.55f):new Color(.5f,1,.6f),TextAnchor.UpperCenter);
  }
  static void Draw(Texture2D tex,Rect r,Color c){var o=GUI.color;GUI.color=c;GUI.DrawTexture(r,tex,ScaleMode.StretchToFill,true);GUI.color=o;}
  static void EnsureTextures()
  {
   if(arrow&&dot&&ring&&heading)return;
   const int n=64;
   arrow=new Texture2D(n,n,TextureFormat.RGBA32,false){wrapMode=TextureWrapMode.Clamp,name="Flecha"};
   dot=new Texture2D(32,32,TextureFormat.RGBA32,false){wrapMode=TextureWrapMode.Clamp,name="Punto"};
   ring=new Texture2D(128,128,TextureFormat.RGBA32,false){wrapMode=TextureWrapMode.Clamp,name="Anillo"};
   // Downward arrow: triangle with a white rim (tinted at draw time), y=0 is the bottom row.
   for(int y=0;y<n;y++)for(int x=0;x<n;x++)
   {
    float fy=y/(n-1f),fx=x/(n-1f);float half=.5f*fy;               // tip at bottom
    float dx=Mathf.Abs(fx-.5f);float inside=half-dx;               // >0 inside triangle
    float top=1-fy;inside=Mathf.Min(inside,top*.5f+.02f);
    float a=Mathf.Clamp01(inside*n*.6f);float rim=Mathf.Clamp01(1-Mathf.Abs(inside*n-1.2f)*.5f);
    var col=Color.Lerp(new Color(1,1,1,a),new Color(1,.9f,.9f,a),rim);arrow.SetPixel(x,y,col);
   }
   arrow.Apply();
   // Player heading: slim arrowhead with a notch at the back (tip at the bottom row, like `arrow`).
   heading=new Texture2D(n,n,TextureFormat.RGBA32,false){wrapMode=TextureWrapMode.Clamp,name="Rumbo"};
   for(int y=0;y<n;y++)for(int x=0;x<n;x++)
   {
    float fy=y/(n-1f),dx=Mathf.Abs(x/(n-1f)-.5f);
    float edge=.42f*fy-dx;                       // sides
    float notch=fy-(.72f+.5f*dx);                // back notch (inside when <0)
    float inside=Mathf.Min(edge,-notch*.5f);
    heading.SetPixel(x,y,new Color(1,1,1,Mathf.Clamp01(inside*n*.7f)));
   }
   heading.Apply();
   for(int y=0;y<32;y++)for(int x=0;x<32;x++){float d=Vector2.Distance(new Vector2(x,y),new Vector2(15.5f,15.5f));dot.SetPixel(x,y,new Color(1,1,1,Mathf.Clamp01(15-d)));}dot.Apply();
   for(int y=0;y<128;y++)for(int x=0;x<128;x++){float d=Vector2.Distance(new Vector2(x,y),new Vector2(63.5f,63.5f));ring.SetPixel(x,y,new Color(1,1,1,Mathf.Clamp01(1.5f-Mathf.Abs(d-61))));}ring.Apply();
  }
 }
}
