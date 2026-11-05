using UnityEngine;
namespace Nexoterra.World
{
 // Movement and weapon locks are independent: a web never disables the blaster.
 public sealed class EnemyControlEffects : MonoBehaviour
 {
  public GameObject webPrefab;
  public float RootRemaining {get;private set;}
  public float FreezeRemaining {get;private set;}
  public float ShockRemaining {get;private set;}
  public float WindRemaining {get;private set;}
  public bool CanMove=>RootRemaining<=0&&FreezeRemaining<=0&&ShockRemaining<=0;
  public bool CanShoot=>FreezeRemaining<=0&&ShockRemaining<=0;
  GameObject rootVisual,iceVisual,windVisual,shockVisual;Vector3 windDirection;
  Material waterMat,iceMat,windMat,shockMat;
  EnemyCombatant health;
  void Awake(){health=GetComponent<EnemyCombatant>();}
  public void Apply(string species,Vector3 direction)
  {
   switch(SlugCatalog.Canonical(species.Replace("Malvada_","")))
   {
    case "Arana":RootRemaining=5.5f;ReplaceRoot(false);break;
    case "Elemental_Agua":RootRemaining=4;ReplaceRoot(true);break;
    case "ice":FreezeRemaining=4; if(!iceVisual)iceVisual=Ice();break;
    case "Infierno":case "Elemental_Fuego":FreezeRemaining=0;Clear(ref iceVisual);break;
    case "tazerling":ShockRemaining=4.5f;if(!shockVisual)shockVisual=SlugFlightEffects.Electrify(transform).gameObject;break;
    case "Tornado":case "Elemental_Aire":WindRemaining=3;direction.y=0;windDirection=direction.normalized;if(!windVisual)windVisual=Vortex();break;
   }
  }
  void Update(){Advance(Time.deltaTime);}
  public void Advance(float dt)
  {
   if(health&&health.IsDead){ResetEffects();return;}
   RootRemaining=Mathf.Max(0,RootRemaining-dt);FreezeRemaining=Mathf.Max(0,FreezeRemaining-dt);ShockRemaining=Mathf.Max(0,ShockRemaining-dt);WindRemaining=Mathf.Max(0,WindRemaining-dt);
   if(RootRemaining<=0)Clear(ref rootVisual);if(FreezeRemaining<=0)Clear(ref iceVisual);if(ShockRemaining<=0)Clear(ref shockVisual);
   if(WindRemaining<=0)Clear(ref windVisual);
   else {if(windVisual)windVisual.transform.Rotate(0,420*dt,0);var motor=GetComponent<CharacterController>();if(CanMove&&motor&&motor.enabled)motor.Move(windDirection*(2.5f*dt));}
  }
  static void Clear(ref GameObject obj){if(obj)Destroy(obj);obj=null;}
  public void ResetEffects(){RootRemaining=FreezeRemaining=ShockRemaining=WindRemaining=0;Clear(ref rootVisual);Clear(ref iceVisual);Clear(ref windVisual);Clear(ref shockVisual);}
  void ReplaceRoot(bool water)
  {
   Clear(ref rootVisual);
   if(water){rootVisual=new GameObject("Prisión acuática");rootVisual.transform.SetParent(transform,false);rootVisual.AddComponent<WaterSnareVisual>();}
   else if(webPrefab){rootVisual=Instantiate(webPrefab,transform);rootVisual.transform.localPosition=Vector3.up*.03f;rootVisual.AddComponent<WebSnareAnchor>();}
   else rootVisual=Ring(water?"Agua acumulada":"Red",.06f,1.1f,water?new Color(.1f,.7f,1,.7f):Color.white,ref waterMat);
  }
  Material Transparent(Color color)
  {
   var mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));mat.SetColor("_BaseColor",color);mat.SetFloat("_Surface",1);mat.SetFloat("_SrcBlend",5);mat.SetFloat("_DstBlend",10);mat.SetFloat("_ZWrite",0);mat.SetFloat("_Smoothness",.85f);mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");mat.renderQueue=3000;return mat;
  }
  GameObject Ice()
  {
   if(!iceMat)iceMat=Transparent(new Color(.25f,.8f,1,.28f));var go=GameObject.CreatePrimitive(PrimitiveType.Cube);Destroy(go.GetComponent<Collider>());go.name="Prisión de hielo";go.transform.SetParent(transform,false);go.transform.localPosition=Vector3.up*1.15f;go.transform.localScale=new Vector3(1.35f,2.4f,1.35f);go.GetComponent<Renderer>().sharedMaterial=iceMat;
   // Bright edges keep transparent ice readable against every biome.
   for(int axis=0;axis<3;axis++)for(int a=-1;a<=1;a+=2)for(int b=-1;b<=1;b+=2){var line=new GameObject("Arista").AddComponent<LineRenderer>();line.transform.SetParent(go.transform,false);line.useWorldSpace=false;line.positionCount=2;Vector3 p=Vector3.zero,q=Vector3.zero;p[axis]=-.5f;q[axis]=.5f;p[(axis+1)%3]=q[(axis+1)%3]=a*.5f;p[(axis+2)%3]=q[(axis+2)%3]=b*.5f;line.SetPositions(new[]{p,q});line.widthMultiplier=.018f;line.sharedMaterial=iceMat;}
   return go;
  }
  GameObject Ring(string name,float height,float radius,Color color,ref Material material)
  {
   if(!material)material=Transparent(color);var go=new GameObject(name);go.transform.SetParent(transform,false);go.transform.localPosition=Vector3.up*height;var line=go.AddComponent<LineRenderer>();line.useWorldSpace=false;line.loop=true;line.positionCount=48;line.widthMultiplier=name=="Agua acumulada"?.32f:.055f;line.sharedMaterial=material;
   for(int i=0;i<48;i++){float t=i*Mathf.PI*2/48;line.SetPosition(i,new Vector3(Mathf.Cos(t)*radius,Mathf.Sin(t*5)*.025f,Mathf.Sin(t)*radius));}return go;
  }
  GameObject Vortex()
  {
   if(!windMat)windMat=Transparent(new Color(.6f,1,1,.6f));var go=new GameObject("Tornado de arrastre");go.transform.SetParent(transform,false);
   for(int strand=0;strand<3;strand++){var line=new GameObject("Espiral").AddComponent<LineRenderer>();line.transform.SetParent(go.transform,false);line.useWorldSpace=false;line.positionCount=70;line.widthMultiplier=.065f;line.sharedMaterial=windMat;for(int i=0;i<70;i++){float h=i/69f,t=h*Mathf.PI*6+strand*2.094f,r=.35f+h*.7f;line.SetPosition(i,new Vector3(Mathf.Cos(t)*r,h*2.5f,Mathf.Sin(t)*r));}}return go;
  }
  void OnDestroy(){foreach(var m in new[]{waterMat,iceMat,windMat,shockMat})if(m)Destroy(m);}
 }
}
