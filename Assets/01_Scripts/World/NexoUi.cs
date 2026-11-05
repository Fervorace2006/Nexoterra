using UnityEngine;
namespace Nexoterra.World
{
 public static class NexoUi
 {
  public static readonly Color Text=new Color(.9f,.96f,.98f),Muted=new Color(.54f,.68f,.74f),Edge=new Color(.16f,.3f,.36f),Surface=new Color(.025f,.055f,.075f,.98f);
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
  public static void Card(Rect r,Color accent,bool selected=false)
  {
   NexoHud.Fill(new Rect(r.x+2,r.y+4,r.width,r.height),new Color(0,0,0,.4f));NexoHud.Fill(r,selected?accent:Edge);
   NexoHud.Fill(new Rect(r.x+1,r.y+1,r.width-2,r.height-2),Surface);
   NexoHud.Fill(new Rect(r.x+1,r.y+1,r.width-2,selected?3:1),selected?accent:new Color(.25f,.4f,.45f));
   NexoHud.Fill(new Rect(r.x,r.y,10,3),accent);NexoHud.Fill(new Rect(r.xMax-10,r.yMax-3,10,3),accent);
  }
  public static void Portrait(Rect r,Texture2D portrait,Color accent,float alpha=1)
  {
   if(!glow){glow=new Texture2D(64,64,TextureFormat.RGBA32,false);glow.wrapMode=TextureWrapMode.Clamp;for(int y=0;y<64;y++)for(int x=0;x<64;x++){float d=Vector2.Distance(new Vector2(x,y),new Vector2(31.5f,31.5f))/44;glow.SetPixel(x,y,new Color(1,1,1,Mathf.Pow(Mathf.Clamp01(1-d),2)*.45f));}glow.Apply();}
   var old=GUI.color;GUI.color=accent;GUI.DrawTexture(r,glow);GUI.color=new Color(1,1,1,alpha);if(portrait)GUI.DrawTexture(r,portrait,ScaleMode.ScaleToFit,true);GUI.color=old;
  }
  public static bool Button(Rect r,string label,bool active=false)
  {
   bool hover=r.Contains(Event.current.mousePosition);var accent=active?NexoHud.Gold:hover?NexoHud.Cyan:Edge;Card(r,accent,active||hover);NexoHud.Label(r,label,14,GUI.enabled?Text:Muted,TextAnchor.MiddleCenter);return GUI.Button(r,GUIContent.none,GUIStyle.none);
  }
  public static string Ability(SlugDefinition d)
  {
   if(d==null)return "Elige una babosa de tu colección.";
   switch(d.id){case "Infierno":case "Elemental_Fuego":return "FUEGO\nImpacto y quemadura posterior.";case "Elemental_Agua":return "AGUA\nInmoviliza con un círculo de agua.";case "ice":return "HIELO\nCongela al objetivo temporalmente.";case "tazerling":return "ELECTRICIDAD\nBloquea movimiento y disparo.";case "Gon_Doc":case "Elemental_Energia":return "CURACIÓN\nRecupera vida al lanzarla.";case "Elemental_Aire":case "Tornado":return "VIENTO\nArrastra enemigos y aumenta tu velocidad.";case "Arana":return "TELARAÑA\nAtrapa los pies del enemigo.";default:return "IMPACTO\nGolpe contundente y derribo.";}
  }
 }
}
