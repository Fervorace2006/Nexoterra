using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;
namespace Nexoterra.World {
 public sealed class CorruptionCompanion:MonoBehaviour {
  [Tooltip("Distancia a la que la babosa empieza a seguirte (metros).")]
  [Min(3)] public float followRadius=18;
  [Tooltip("Distancia a la que deja de seguirte. Menor que Follow Radius.")]
  [Min(1)] public float stopRadius=14;
  LineRenderer rangeRing;
  SlugInventory inventory;NexoterraThirdPerson player;readonly Dictionary<int,LooseSlug> companions=new Dictionary<int,LooseSlug>();
  public LooseSlug companion { get {
   if(!inventory||inventory.selected<0||inventory.selected>=inventory.equipped.Length)return null;
   return companions.TryGetValue(inventory.equipped[inventory.selected],out var slug)?slug:null;
  }}
  public bool IsLoose(string species){foreach(var slug in companions.Values)if(slug&&slug.Species==species)return true;return false;}
  void OnDestroy(){foreach(var slug in companions.Values)if(slug)Destroy(slug.gameObject);}
  float collectAt;
  void Awake(){inventory=GetComponent<SlugInventory>();player=GetComponent<NexoterraThirdPerson>();}
  void Update(){if(companions.Count>0){if(!rangeRing)rangeRing=HealingRadiusVisual.Ring(transform,followRadius,new Color(.2f,.8f,1,.45f));HealingRadiusVisual.Resize(rangeRing,followRadius);}else if(rangeRing)Destroy(rangeRing.gameObject);if(GameSession.Blocked)return;var k=Keyboard.current;if(k==null)return;if(k.gKey.wasPressedThisFrame)Release();if(k.kKey.wasPressedThisFrame)CollectWater();if(k.jKey.wasPressedThisFrame)Corrupt();}
  public void Release(){if(companion&&companion.changing){player.Notify("Espera a que termine la transformación");return;}if(companion){Recall();return;}var d=inventory.SelectedDefinition();if(d==null||inventory.IsBusy(d.id)){player.Notify("Espera a que regrese la babosa");return;}int index=inventory.equipped[inventory.selected];var go=new GameObject("Compañera suelta");Vector3 point=transform.position;
   // Keep the player's horizontal position and place the slug on the ground at their feet.
   if(Physics.Raycast(point+Vector3.up*.25f,Vector3.down,out var ground,2f,SlugHop.WorldMask,QueryTriggerInteraction.Ignore))
    point.y=ground.point.y;
   go.transform.position=point;var released=go.AddComponent<LooseSlug>();companions[index]=released;released.Initialize(inventory,index);player.Notify("G: llamar esta babosa · J: convertir con agua oscura");}
  public void Recall(){
   if(!companion)return;
   if(Vector3.Distance(companion.transform.position,transform.position)<=1.1f){CompleteRecall(companion);return;}
   companion.Recall();
   player.Notify("Babosa regresando al inventario");
  }
  public void CompleteRecall(LooseSlug slug){
   if(!slug||!companions.TryGetValue(slug.InventoryIndex,out var registered)||registered!=slug)return;
   companions.Remove(slug.InventoryIndex);
   Destroy(slug.gameObject);
   player.Notify("Babosa guardada");
  }
  public void CollectWater(){if(Time.time<collectAt)return;foreach(var r in FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None)){var mat=r.sharedMaterial;if(!mat||!mat.name.ToLower().Contains("carmesi"))continue;if(Vector3.Distance(r.bounds.ClosestPoint(transform.position),transform.position)>3)continue;inventory.darkWater++;collectAt=Time.time+2;player.Notify("Agua oscura: "+inventory.darkWater);return;}player.Notify("Acércate al agua carmesí y pulsa K");}
  public void Corrupt(){if(!companion||companion.changing||companion.IsRecalling)return;var id=companion.Species;if(id.StartsWith("Malvada_")){player.Notify("Esta babosa ya es malvada");return;}if(id=="Elemental_Energia"||inventory.catalog.Find("Malvada_"+id)==null){player.Notify("Esta especie no tiene versión malvada disponible");return;}if(inventory.darkWater<=0){player.Notify("Recoge agua oscura con K");return;}inventory.darkWater--;companion.Change("Malvada_"+id);}
  void OnGUI(){if(GameSession.Blocked||!inventory.IsOpen)return;GUI.Label(new Rect(20,80,450,65),"Agua oscura: "+inventory.darkWater+"   K: recolectar cerca del lago");if(GUI.Button(new Rect(20,115,160,35),companion?"Llamar seleccionada (G)":"Soltar seleccionada (G)"))Release();if(GUI.Button(new Rect(20,155,160,35),"Convertir (J)"))Corrupt();}
 }
 public sealed class LooseSlug:MonoBehaviour {
  bool following;bool recalling;bool hopping;Vector3 hopStart,hopEnd;float hopProgress;public bool IsRecalling=>recalling;public void Recall(){recalling=true;}
  SlugInventory owner;int index;public int InventoryIndex=>index;GameObject model;public bool changing;public string Species=>owner&&index<owner.owned.Count?owner.owned[index]:"";
  public void Initialize(SlugInventory inventory,int slot){owner=inventory;index=slot;var c=gameObject.AddComponent<SphereCollider>();c.radius=.3f;c.center=Vector3.up*.25f;Show();}
  void Show(){if(model)Destroy(model);var d=owner.catalog.Find(Species);if(d==null)return;model=Instantiate(d.normal,transform);foreach(var c in model.GetComponentsInChildren<Collider>())Destroy(c);var rs=model.GetComponentsInChildren<Renderer>();if(rs.Length==0)return;var b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);float scale=.35f/Mathf.Max(.01f,b.size.y);var center=b.center-transform.position;model.transform.localScale*=scale;model.transform.localPosition=-center*scale+Vector3.up*.175f;}
  public void Purify(){if(Species.StartsWith("Malvada_")&&!changing){changing=true;SlugPurification.Begin(gameObject,()=>{owner.owned[index]=SlugCatalog.BaseSpecies(Species);Show();},()=>changing=false);}}
  public void Change(string id){StartCoroutine(Transition(id));}
  IEnumerator Transition(string id){changing=true;var fx=new GameObject("Conversión roja");fx.transform.SetParent(transform,false);fx.AddComponent<CorruptionConversionVfx>();yield return new WaitForSeconds(.7f);owner.owned[index]=id;Show();yield return new WaitForSeconds(.7f);Destroy(fx);changing=false;}
  void Update(){if(!owner||index>=owner.owned.Count){Destroy(gameObject);return;}if(changing||GameSession.Blocked)return;var delta=owner.transform.position-transform.position;var settings=owner.GetComponent<CorruptionCompanion>();float outer=settings?settings.followRadius:18;float inner=settings?Mathf.Min(settings.stopRadius,outer-1):14;if(recalling){
   if(delta.magnitude<=1.1f){if(settings)settings.CompleteRecall(this);else Destroy(gameObject);return;}
   if(!hopping&&SlugHop.Plan(transform.position,owner.transform.position,.7f,.3f,0,out var destination)){
    hopStart=transform.position;hopEnd=destination;hopProgress=0;hopping=true;
   }
   if(hopping){
    hopProgress=Mathf.Min(1,hopProgress+Time.deltaTime*5);
    transform.position=SlugHop.Sample(hopStart,hopEnd,hopProgress,.3f);
    if(hopProgress>=1)hopping=false;
   }
   return;
  }
  delta.y=0;if(delta.magnitude>=outer)following=true;if(delta.magnitude<=inner)following=false;if(following&&SlugHop.Plan(transform.position,owner.transform.position,.6f,.3f,0,out var end))transform.position=Vector3.MoveTowards(transform.position,end,Time.deltaTime*2);if(model)model.transform.localPosition+=Vector3.up*(Mathf.Sin(Time.time*9)*.0015f);}
 }
}
