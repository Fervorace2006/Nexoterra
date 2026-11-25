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
  void Update(){if(companions.Count>0){if(!rangeRing)rangeRing=HealingRadiusVisual.Ring(transform,followRadius,new Color(.2f,.8f,1,.45f));HealingRadiusVisual.Resize(rangeRing,followRadius);}else if(rangeRing)Destroy(rangeRing.gameObject);if(GameSession.Blocked)return;var k=Keyboard.current;if(NexoInput.Companion)Release();if(k==null)return;if(k.gKey.wasPressedThisFrame)Release();if(k.kKey.wasPressedThisFrame)CollectWater();if(k.jKey.wasPressedThisFrame)Corrupt();}
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
  // Dark-water panel shown beside the backpack, with key caps for K / G / J.
  void OnGUI(){
   if(GameSession.Blocked||!inventory.IsOpen)return;var before=GUI.matrix;NexoHud.Scale();
   var panel=new Rect(8,34,156,262);NexoUi.Panel(panel);var inner=new Rect(panel.x+6,panel.y+6,panel.width-12,panel.height-12);
   NexoHud.Label(new Rect(inner.x,inner.y+6,inner.width,16),"AGUA OSCURA",11,NexoHud.Cyan,TextAnchor.MiddleCenter);
   NexoHud.Shadowed(new Rect(inner.x,inner.y+22,inner.width,34),inventory.darkWater.ToString(),26,Color.white,TextAnchor.MiddleCenter);
   float x=NexoHud.KeyCap(inner.x+8,inner.y+66,"K");NexoHud.Label(new Rect(x+6,inner.y+62,inner.width-(x-inner.x)-8,34),"recolectar cerca del lago",11,NexoUi.Muted);
   var release=new Rect(inner.x+4,inner.y+108,inner.width-8,56);if(NexoUi.Button(release,""))Release();
   x=NexoHud.KeyCap(release.x+8,release.y+15,"G");NexoHud.Label(new Rect(x+6,release.y,release.xMax-x-8,release.height),(companion?"Llamar":"Soltar")+"\nseleccionada",12,NexoUi.Text,TextAnchor.MiddleLeft);
   var convert=new Rect(inner.x+4,inner.y+172,inner.width-8,56);if(NexoUi.Button(convert,""))Corrupt();
   x=NexoHud.KeyCap(convert.x+8,convert.y+15,"J");NexoHud.Label(new Rect(x+6,convert.y,convert.xMax-x-8,convert.height),"Convertir\nen malvada",12,NexoUi.Text,TextAnchor.MiddleLeft);
   GUI.matrix=before;
  }
 }
 public sealed class LooseSlug:MonoBehaviour {
  bool following;bool recalling;bool hopping;Vector3 hopStart,hopEnd;float hopProgress;public bool IsRecalling=>recalling;public void Recall(){recalling=true;}
  SlugInventory owner;int index;public int InventoryIndex=>index;GameObject model;public bool changing;public string Species=>owner&&index<owner.owned.Count?owner.owned[index]:"";
  public void Initialize(SlugInventory inventory,int slot){owner=inventory;index=slot;gameObject.layer=2;var c=gameObject.AddComponent<SphereCollider>();c.radius=.3f;c.center=Vector3.up*.2f;Show();}
  void Show(){if(model)Destroy(model);var d=owner.catalog.Find(Species);if(d==null)return;model=Instantiate(d.normal,transform);foreach(var c in model.GetComponentsInChildren<Collider>())Destroy(c);WildSlug.Fit(transform,model.transform);}
  public void Purify(){if(Species.StartsWith("Malvada_")&&!changing&&owner.catalog.Find(SlugCatalog.BaseSpecies(Species))!=null){changing=true;SlugPurification.Begin(gameObject,()=>{owner.owned[index]=SlugCatalog.BaseSpecies(Species);Show();},()=>changing=false);}}
  public void Change(string id){if(changing||!owner||owner.catalog.Find(id)==null)return;StartCoroutine(Transition(id));}
  IEnumerator Transition(string id){changing=true;var fx=new GameObject("Conversión roja");fx.transform.SetParent(transform,false);fx.AddComponent<CorruptionConversionVfx>();yield return new WaitForSeconds(.7f);owner.owned[index]=id;Show();yield return new WaitForSeconds(.7f);Destroy(fx);changing=false;}
  // Hops back inside the follow ring when the player leaves it, and all the way back on G (recall).
  // The slug's own collider is on Ignore Raycast so path checks don't hit it; if no clear hop is found
  // it hops straight towards the player, and it catches up instantly if left far behind.
  void Update(){
   if(!owner||index>=owner.owned.Count){Destroy(gameObject);return;}
   if(changing||GameSession.Blocked)return;
   var settings=owner.GetComponent<CorruptionCompanion>();float outer=settings?settings.followRadius:18;float inner=settings?Mathf.Min(settings.stopRadius,outer-1):14;
   var flat=owner.transform.position-transform.position;flat.y=0;float distance=flat.magnitude;
   if(recalling&&distance<=1.4f){if(settings)settings.CompleteRecall(this);else Destroy(gameObject);return;}
   if(!recalling){if(distance>=outer)following=true;else if(distance<=inner)following=false;}
   if(distance>outer*3){Land(owner.transform.position-flat.normalized*(recalling?1:inner*.8f));hopping=false;return;}
   if(hopping)
   {
    hopProgress=Mathf.Min(1,hopProgress+Time.deltaTime*(recalling?6f:4.5f));
    transform.position=SlugHop.Sample(hopStart,hopEnd,hopProgress,.3f);
    if(flat.sqrMagnitude>.01f)transform.rotation=Quaternion.Slerp(transform.rotation,Quaternion.LookRotation(flat),Time.deltaTime*10);
    if(hopProgress>=1)hopping=false;
    return;
   }
   if(!recalling&&!following)return;
   float stride=recalling?1.2f:distance>outer+4?1.4f:.9f;
   if(!SlugHop.Plan(transform.position,owner.transform.position,stride,.3f,0,out var end,owner.transform))end=Ground(transform.position+flat.normalized*Mathf.Min(stride,distance));
   hopStart=transform.position;hopEnd=end;hopProgress=0;hopping=true;
  }
  Vector3 Ground(Vector3 p){return Physics.Raycast(p+Vector3.up*12f,Vector3.down,out var hit,30,SlugHop.WorldMask,QueryTriggerInteraction.Ignore)?hit.point:p;}
  void Land(Vector3 p){transform.position=Ground(p);}
 }
}
