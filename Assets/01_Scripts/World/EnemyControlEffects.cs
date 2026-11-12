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
  const float FallSeconds=54f/30f, StandSeconds=43f/30f;
  float knockdownRemaining;
  public string KnockdownPose=>knockdownRemaining>StandSeconds?"CaidaAtras":knockdownRemaining>0?"levantarsedecaida":null;
  public static bool CausesKnockdown(string id)
  {
   string species=SlugCatalog.BaseSpecies(id);
   return species=="Carnero"||species=="Armashelt"||species=="Elemental_Tierra";
  }
  public void BeginKnockdown()
  {
   if(knockdownRemaining<=StandSeconds)knockdownRemaining=FallSeconds+StandSeconds;
  }
  public bool CanMove=>WindRemaining<=0&&knockdownRemaining<=0&&RootRemaining<=0&&FreezeRemaining<=0&&ShockRemaining<=0;
  public bool CanShoot=>knockdownRemaining<=0&&FreezeRemaining<=0&&ShockRemaining<=0;
  GameObject rootVisual,iceVisual,windVisual,shockVisual;Vector3 windDirection;
  Material waterMat,iceMat,shockMat;
  bool corruptedWind;
  EnemyCombatant health;
  void Awake(){health=GetComponent<EnemyCombatant>();}
  public void Apply(string species,Vector3 direction)
  {
   bool corrupted=species.StartsWith("Malvada_");
   switch(SlugCatalog.Canonical(species.Replace("Malvada_","")))
   {
    case "Arana":RootRemaining=5.5f;ReplaceRoot(false);break;
    case "Elemental_Agua":RootRemaining=4;ReplaceRoot(true);break;
    case "ice":FreezeRemaining=4; if(!iceVisual)iceVisual=Ice();if(iceMat)iceMat.SetColor("_BaseColor",corrupted?new Color(.04f,.35f,.65f,.5f):new Color(.25f,.8f,1,.28f));break;
    case "Infierno":case "Elemental_Fuego":FreezeRemaining=0;Clear(ref iceVisual);break;
    case "tazerling":ShockRemaining=4.5f;if(!shockVisual)shockVisual=ImportedSlugVfx.ShockCharacter(transform);if(corrupted)ImportedSlugVfx.Tint(shockVisual,new Color(.15f,1,.55f));break;
    case "Tornado":case "Elemental_Aire":
     WindRemaining=4f;
     direction.y=0;
     windDirection=direction.sqrMagnitude>.001f?direction.normalized:-transform.forward;
     if(windVisual&&corrupted!=corruptedWind)Clear(ref windVisual);
     corruptedWind=corrupted;
     if(!windVisual)windVisual=Vortex(corrupted);
     break;
   }
  }
  void Update(){Advance(Time.deltaTime);}
  public void Advance(float dt)
  {
   if(health&&health.IsDead){ResetEffects();return;}
   knockdownRemaining=Mathf.Max(0,knockdownRemaining-dt);
   RootRemaining=Mathf.Max(0,RootRemaining-dt);FreezeRemaining=Mathf.Max(0,FreezeRemaining-dt);ShockRemaining=Mathf.Max(0,ShockRemaining-dt);WindRemaining=Mathf.Max(0,WindRemaining-dt);
   if(RootRemaining<=0)Clear(ref rootVisual);if(FreezeRemaining<=0)Clear(ref iceVisual);if(ShockRemaining<=0)Clear(ref shockVisual);
   if(WindRemaining<=0)Clear(ref windVisual);
   else if(knockdownRemaining<=0&&RootRemaining<=0&&FreezeRemaining<=0)
   {
    var motor=GetComponent<CharacterController>();
    if(motor&&motor.enabled)
    {
     Vector3 swirl=Vector3.Cross(Vector3.up,windDirection)*Mathf.Sin(WindRemaining*5);
     motor.Move((windDirection*4f+swirl*1.8f)*dt);
    }
   }
  }
  static void Clear(ref GameObject obj){if(obj)Destroy(obj);obj=null;}
  public void ResetEffects(){knockdownRemaining=RootRemaining=FreezeRemaining=ShockRemaining=WindRemaining=0;Clear(ref rootVisual);Clear(ref iceVisual);Clear(ref windVisual);Clear(ref shockVisual);}
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
  GameObject Vortex(bool corrupted)
  {
   var vortex=new GameObject("Tornado de arrastre");
   vortex.transform.SetParent(transform,false);
   var funnel=ImportedSlugVfx.Spawn("AirTornado",vortex.transform,Vector3.zero,1,Quaternion.identity);
   if(funnel)
   {
    var fx=funnel.GetComponent<Hun0FX.HunFXController>();
    if(fx){fx.intensity=.55f;fx.SetColor(corrupted?Color.red:Color.white);}
    var body=GetComponent<CharacterController>();
    float height=body?body.height:2f;
    funnel.transform.localScale=Vector3.one*(height*.26f);
   }
   if(corrupted)
   {
    var lightning=ImportedSlugVfx.Spawn("Shock",vortex.transform,Vector3.up*1.2f,1.15f,Quaternion.identity);
    ImportedSlugVfx.Tint(lightning,new Color(1f,.16f,.12f));
   }
   return vortex;
  }
  void OnDestroy(){foreach(var m in new[]{waterMat,iceMat,shockMat})if(m)Destroy(m);}
 }
}
