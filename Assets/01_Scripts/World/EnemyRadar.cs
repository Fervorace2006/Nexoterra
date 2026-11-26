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
  static Texture2D arrow,dot,ring;
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
    mapCamera.cullingMask=~(1<<5);mapCamera.depth=-50;
    var data=go.AddComponent<UniversalAdditionalCameraData>();data.renderPostProcessing=false;data.renderShadows=false;
   }
   // Below cave roofs (y≈25) and above the player, rotated with the view so "up" is where you look.
   float yaw=Camera.main?Camera.main.transform.eulerAngles.y:player.transform.eulerAngles.y;
   mapCamera.transform.position=player.transform.position+Vector3.up*18;
   mapCamera.transform.rotation=Quaternion.Euler(90,yaw,0);
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
     float size=Mathf.Clamp(900f/dist,16,boss?42:32)*(Screen.height/720f);
     float bob=Mathf.Sin(t*4+e.GetHashCode())*size*.15f;
     var r=new Rect(p.x-size/2,Screen.height-p.y-size+bob-6,size,size);
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
     float size=26*(Screen.height/720f);var r=new Rect(pos.x-size/2,pos.y-size/2,size,size);
     float angle=Mathf.Atan2(-d.y,d.x)*Mathf.Rad2Deg-90;   // arrow texture points down
     var m=GUI.matrix;GUIUtility.RotateAroundPivot(angle,pos);Draw(arrow,r,new Color(1,.12f,.1f,.9f));GUI.matrix=m;
    }
   }
  }
  // ---- Minimap + radar (in the 1280×720 HUD space) ----
  void DrawMinimap()
  {
   var r=MapRect;var inner=new Rect(r.x+6,r.y+6,r.width-12,r.height-12);
   NexoHud.Fill(new Rect(r.x+2,r.y+4,r.width,r.height),new Color(0,0,0,.45f));
   NexoHud.Fill(r,new Color(.05f,.07f,.08f,.95f));
   if(mapTexture){var o=GUI.color;GUI.color=new Color(.75f,.95f,.95f,1);GUI.DrawTexture(inner,mapTexture,ScaleMode.StretchToFill,false);GUI.color=o;}
   NexoHud.Fill(inner,new Color(0,.18f,.2f,.28f));
   // radar rings + sweep
   Draw(ring,inner,new Color(.3f,1,.95f,.45f));
   var ci=new Rect(inner.center.x-inner.width*.25f,inner.center.y-inner.height*.25f,inner.width*.5f,inner.height*.5f);Draw(ring,ci,new Color(.3f,1,.95f,.3f));
   float sweep=(Time.unscaledTime*120)%360;var m=GUI.matrix;GUIUtility.RotateAroundPivot(sweep,inner.center);
   for(int i=0;i<10;i++)NexoHud.Fill(new Rect(inner.center.x-1,inner.y+8,2,inner.height/2-8),new Color(.4f,1,.9f,.035f*(10-i)));
   GUI.matrix=m;
   // enemy blips (rotated so up = view direction), clamped to the edge
   float yaw=Camera.main?Camera.main.transform.eulerAngles.y:0;var rot=Quaternion.Euler(0,-yaw,0);
   float scale=(inner.width/2)/MapRange;float pulse=.75f+.25f*Mathf.Sin(Time.unscaledTime*6);
   foreach(var e in enemies)
   {
    if(!e||e.IsDead)continue;var rel=rot*(e.transform.position-player.transform.position);
    var v=new Vector2(rel.x,-rel.z)*scale;bool edge=v.magnitude>inner.width/2-6;if(edge)v=v.normalized*(inner.width/2-6);
    bool boss=e.GetComponent<EnemyShooter>()&&e.GetComponent<EnemyShooter>().boss;float s=(boss?12:8)*(edge?.8f:pulse);
    Draw(dot,new Rect(inner.center.x+v.x-s/2,inner.center.y+v.y-s/2,s,s),boss?new Color(1,.35f,.1f):new Color(1,.12f,.1f));
   }
   // exit marker
   var level=CampaignLevel.Current;
   if(level&&level.exit){var rel=rot*(level.exit.position-player.transform.position);var v=new Vector2(rel.x,-rel.z)*scale;if(v.magnitude>inner.width/2-6)v=v.normalized*(inner.width/2-6);Draw(dot,new Rect(inner.center.x+v.x-5,inner.center.y+v.y-5,10,10),new Color(.3f,1,.4f));}
   // player arrow (points up = view direction)
   var pr=new Rect(inner.center.x-8,inner.center.y-8,16,16);var mm=GUI.matrix;GUIUtility.RotateAroundPivot(180,inner.center);Draw(arrow,pr,new Color(.4f,1,1));GUI.matrix=mm;
   NexoHud.Brackets(new Rect(r.x-3,r.y-3,r.width+6,r.height+6),NexoHud.Orange,18,4);
   NexoHud.Label(new Rect(r.x,r.yMax+2,r.width,16),enemies.Count>0?"ENEMIGOS: "+enemies.Count:"ZONA DESPEJADA",11,enemies.Count>0?new Color(1,.55f,.5f):new Color(.5f,1,.6f),TextAnchor.UpperCenter);
  }
  static void Draw(Texture2D tex,Rect r,Color c){var o=GUI.color;GUI.color=c;GUI.DrawTexture(r,tex,ScaleMode.StretchToFill,true);GUI.color=o;}
  static void EnsureTextures()
  {
   if(arrow&&dot&&ring)return;
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
   for(int y=0;y<32;y++)for(int x=0;x<32;x++){float d=Vector2.Distance(new Vector2(x,y),new Vector2(15.5f,15.5f));dot.SetPixel(x,y,new Color(1,1,1,Mathf.Clamp01(15-d)));}dot.Apply();
   for(int y=0;y<128;y++)for(int x=0;x<128;x++){float d=Vector2.Distance(new Vector2(x,y),new Vector2(63.5f,63.5f));ring.SetPixel(x,y,new Color(1,1,1,Mathf.Clamp01(1.5f-Mathf.Abs(d-61))));}ring.Apply();
  }
 }
}
