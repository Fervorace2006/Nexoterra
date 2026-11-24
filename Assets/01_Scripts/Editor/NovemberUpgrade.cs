using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Nexoterra.World;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class NovemberUpgrade
{
 static NovemberUpgrade(){EditorApplication.update+=Poll;}
 static void Poll(){
  if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||!File.Exists("Library/NovemberUpgrade.request"))return;
  string mode=File.ReadAllText("Library/NovemberUpgrade.request").Trim();File.Delete("Library/NovemberUpgrade.request");
  try{if(mode=="install")Install();else if(mode=="audit")Audit();else if(mode=="scenes")SceneWeapons();else if(mode=="water")WaterProbe();else if(mode=="jumplayer")JumpLayer();else if(mode=="gunshot")GunShot();else if(mode=="trimjump")TrimJump();else if(mode=="customjump")CustomJump();else if(mode=="rockwalls")RockWalls();else if(mode=="cavecorners")CaveCorners();else if(mode=="uikit")UiKit();else if(mode=="openlevels")OpenLevels();else if(mode=="groundmats")GroundMats();else if(mode=="relief")Relief();else if(mode=="routes")Routes();else if(mode=="fixground")FixGround();else if(mode=="relief2")Relief(2.2f,1f);else if(mode=="reliefsoft"){soft=true;softMode=true;try{Relief(1f,2.2f);}finally{soft=false;softMode=false;}}else if(mode=="renav")Renav();else AssetDatabase.Refresh();File.WriteAllText("Library/NovemberUpgrade.result","PASS "+mode);}
  catch(Exception e){File.WriteAllText("Library/NovemberUpgrade.result",e.ToString());Debug.LogException(e);}
 }
 const string Gameplay="Assets/02_Prefabs/Gameplay";
 static void Install(){
  AssetDatabase.Refresh();
  InstallTornado(); // jumps are now hand-made clips (customjump)
  foreach(string role in new[]{"Enemy1","Enemy2","SirBlack","Sir_Black_2"}){
   string path=Gameplay+"/Enemies/"+role+".prefab";
   var root=PrefabUtility.LoadPrefabContents(path);
   try{ReplaceGun(root,role.StartsWith("Sir")?"Sir_Blast":"Enemies_Blast");PrefabUtility.SaveAsPrefabAsset(root,path);}
   finally{PrefabUtility.UnloadPrefabContents(root);}
  }
  // Use the editable normal prefab stats, which are also shared with evil variants.
  var catalog=AssetDatabase.LoadAssetAtPath<SlugCatalog>("Assets/NexoterraWorld/Slugs/Catalog.asset");
  foreach(var d in catalog.species.Where(d=>SlugCatalog.BaseSpecies(d.id).StartsWith("Elemental_"))){
   string baseId=SlugCatalog.BaseSpecies(d.id);
   float damage=baseId=="Elemental_Fuego"?42:baseId=="Elemental_Tierra"?40:baseId=="Elemental_Energia"?10:28;
   if(d.combat!=null)d.combat.impactDamage=damage;
   if(d.normal){
    var stats=d.normal.GetComponent<SlugPrefabStats>();
    if(stats){stats.combat.impactDamage=damage;EditorUtility.SetDirty(stats);}
   }
  }
  EditorUtility.SetDirty(catalog);
  // Persist only targeted collider repairs; preserve the campaign geometry and spawns.
  FixWaterColliders();
  AssetDatabase.SaveAssets();Audit();
 }
 static Bounds LocalBounds(GameObject root,Transform space){
  bool first=true;Bounds result=default;
  foreach(var r in root.GetComponentsInChildren<Renderer>(true)){
   if(!r.enabled||r is ParticleSystemRenderer||r is LineRenderer)continue;
   var b=r.localBounds;var matrix=space.worldToLocalMatrix*r.transform.localToWorldMatrix;
   for(int i=0;i<8;i++){
    Vector3 p=matrix.MultiplyPoint3x4(b.center+Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1)));
    if(first){result=new Bounds(p,Vector3.zero);first=false;}else result.Encapsulate(p);
   }
  }return result;
 }
 static void ReplaceGun(GameObject root,string name){
  var ai=root.GetComponent<EnemyShooter>();if(!ai||!ai.weapon)throw new Exception("Arma ausente: "+root.name);
  var weapon=ai.weapon;if(weapon.Find(name))return;
  var bounds=LocalBounds(weapon.gameObject,weapon);
  if(bounds.size.sqrMagnitude<.001f)throw new Exception("Bounds de arma inválidos");
  string folder="Assets/03_Models/Weapons/"+name;
  string model=Directory.GetFiles(folder,"*.fbx").First().Replace((char)92,'/');
  var source=AssetDatabase.LoadAssetAtPath<GameObject>(model);
  var go=(GameObject)PrefabUtility.InstantiatePrefab(source);go.name=name;go.transform.SetParent(weapon,false);
  foreach(var c in go.GetComponentsInChildren<Collider>(true))Object.DestroyImmediate(c);
  var mat=AssetDatabase.LoadAssetAtPath<Material>(folder+"/Blaster.mat");
  if(!mat){mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(mat,folder+"/Blaster.mat");}
  string texture=Directory.GetFiles(folder,"*",SearchOption.AllDirectories).First(x=>x.ToLower().Contains("basecolor")&&!x.EndsWith(".meta")).Replace((char)92,'/');
  mat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(texture));mat.SetFloat("_Smoothness",.35f);mat.SetFloat("_Metallic",.25f);
  foreach(var r in go.GetComponentsInChildren<Renderer>(true))r.sharedMaterials=Enumerable.Repeat(mat,r.sharedMaterials.Length).ToArray();
  var imported=LocalBounds(go,weapon);
  if((imported.size.x>imported.size.z)!=(bounds.size.x>bounds.size.z)){go.transform.localRotation=Quaternion.Euler(0,90,0);imported=LocalBounds(go,weapon);}
  float scale=Mathf.Max(bounds.size.x,bounds.size.z)/Mathf.Max(imported.size.x,imported.size.z);
  go.transform.localScale*=scale;imported=LocalBounds(go,weapon);go.transform.localPosition+=bounds.center-imported.center;
  // Keep the original socket, aim calibration and muzzle. Replace only visible geometry.
  foreach(var r in weapon.GetComponentsInChildren<Renderer>(true))if(!r.transform.IsChildOf(go.transform))r.enabled=false;
  foreach(var t in go.GetComponentsInChildren<Transform>(true))t.gameObject.layer=2;
 }
 static void InstallJumps(){
  AnimationClip start=ImportJump("jump_start_ue5_manny_fit_anim_only.fbx");
  AnimationClip land=ImportJump("jump_land_ue5_manny_fit_anim_only.fbx");
  foreach(var guid in AssetDatabase.FindAssets("t:AnimatorController",new[]{"Assets/NexoterraWorld/Locomotion/Imported"})){
   var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(AssetDatabase.GUIDToAssetPath(guid));
   var machine=controller.layers[0].stateMachine;
   foreach(var pair in new[]{("JumpStart",start),("JumpLand",land)}){
    var state=machine.states.Select(s=>s.state).FirstOrDefault(s=>s.name==pair.Item1)??machine.AddState(pair.Item1);
    state.motion=pair.Item2;state.speed=pair.Item1=="JumpLand"?pair.Item2.length/.25f:1;
   }
   foreach(var state in machine.states.Where(s=>s.state.name=="RollForward"||s.state.name=="RollBackward").ToArray())machine.RemoveState(state.state);
   EditorUtility.SetDirty(controller);
  }
 }
 static AnimationClip ImportJump(string file){
  string path="Assets/Characters/Animations/Jump/"+file;var importer=(ModelImporter)AssetImporter.GetAtPath(path);
  importer.animationType=ModelImporterAnimationType.Human;importer.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;importer.importAnimation=true;
  importer.importCameras=false;importer.importLights=false;importer.materialImportMode=ModelImporterMaterialImportMode.None;
  var clips=importer.defaultClipAnimations;
  foreach(var c in clips){c.loopTime=false;c.lockRootRotation=true;c.lockRootHeightY=true;c.lockRootPositionXZ=true;c.keepOriginalOrientation=true;c.keepOriginalPositionXZ=true;}
  importer.clipAnimations=clips;importer.SaveAndReimport();
  var avatar=AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();
  if(!avatar||!avatar.isValid||!avatar.isHuman)throw new Exception("Avatar de salto inválido: "+file);
  return AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().First(c=>!c.name.StartsWith("__preview"));
 }
 static void InstallTornado(){
  const string folder="Assets/Resources/ImportedSlugVfx";
  var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/fireAttackEffects/prefabs/tornadoVFX.prefab");
  var root=Object.Instantiate(source);
  try{
   foreach(var script in root.GetComponentsInChildren<MonoBehaviour>(true))Object.DestroyImmediate(script);
   var originals=root.GetComponentsInChildren<Renderer>(true).SelectMany(r=>r.sharedMaterials).Where(m=>m).Distinct().ToArray();
   foreach(var original in originals){
    string path=folder+"/Tornado_"+original.name.Replace(" ","_")+".mat";
    var mat=AssetDatabase.LoadAssetAtPath<Material>(path);if(!mat){mat=new Material(original);AssetDatabase.CreateAsset(mat,path);}
    if(mat.HasProperty("_Color"))mat.SetColor("_Color",new Color(.85f,.88f,.92f,1));
    if(mat.HasProperty("_color_2"))mat.SetColor("_color_2",new Color(.35f,.39f,.43f,.6f));
    foreach(var r in root.GetComponentsInChildren<Renderer>(true))r.sharedMaterials=r.sharedMaterials.Select(m=>m==original?mat:m).ToArray();
    EditorUtility.SetDirty(mat);
   }
   foreach(var vfx in root.GetComponentsInChildren<UnityEngine.VFX.VisualEffect>(true)){
    foreach(string key in new[]{"core color 1","core color 2","tornadoLayer1Color 1","tornadoLayer1Color 2","topRingColor 1","topRingColor 2","buttomRingColor 1","buttomRingColor 2"})
     if(vfx.HasVector4(key))vfx.SetVector4(key,key.EndsWith("2")?new Vector4(.4f,.43f,.46f,1):new Vector4(.88f,.91f,.94f,1));
    if(vfx.HasFloat("life time"))vfx.SetFloat("life time",4);
   }
   foreach(var ps in root.GetComponentsInChildren<ParticleSystem>(true)){var main=ps.main;main.loop=true;main.startColor=Color.white;main.scalingMode=ParticleSystemScalingMode.Hierarchy;}
   PrefabUtility.SaveAsPrefabAsset(root,folder+"/AirTornado.prefab");
  }finally{Object.DestroyImmediate(root);}
 }
 static void FixWaterColliders(){
  var active=SceneManager.GetActiveScene();
  foreach(string guid in AssetDatabase.FindAssets("t:Scene",new[]{"Assets/00_Scenes"})){
   string path=AssetDatabase.GUIDToAssetPath(guid);if(!path.Contains("Nexoterra_")||path.Contains("Menu"))continue;
   var scene=SceneManager.GetSceneByPath(path);bool opened=!scene.isLoaded;if(opened)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
   try{
    if(scene.isDirty)throw new Exception("Guarda antes de reparar agua: "+path);
    bool changed=false;
    foreach(var root in scene.GetRootGameObjects())foreach(var filter in root.GetComponentsInChildren<MeshFilter>(true)){
     var r=filter.GetComponent<Renderer>();if(!r||!r.sharedMaterial)continue;
     string shader=r.sharedMaterial.shader.name;
     if(shader=="Nexoterra/Cavern Water"||shader=="Nexoterra/Sanctuary Water"){
      foreach(var collider in filter.GetComponents<Collider>())if(!collider.isTrigger){collider.enabled=false;changed=true;}
     }else{
      var collider=filter.GetComponent<MeshCollider>();
      if(collider&&collider.sharedMesh!=filter.sharedMesh&&(filter.name.Contains("Suelo")||filter.name.Contains("lago")||filter.name.Contains("Lake"))){collider.sharedMesh=filter.sharedMesh;changed=true;}
     }
    }
    if(changed){EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);}
   }finally{if(opened)EditorSceneManager.CloseScene(scene,true);}
  }
  if(active.IsValid()&&active.isLoaded)SceneManager.SetActiveScene(active);
 }
 // Scene-only (unpacked) enemies, e.g. the final Sir Black, get the same weapon swap as the prefabs.
 static void SceneWeapons(){
  var report=new StringBuilder();var active=SceneManager.GetActiveScene().path;
  foreach(string guid in AssetDatabase.FindAssets("t:Scene",new[]{"Assets/00_Scenes"})){
   string path=AssetDatabase.GUIDToAssetPath(guid);if(!path.Contains("Nexoterra_")||path.Contains("Menu"))continue;
   var scene=SceneManager.GetSceneByPath(path);bool opened=!scene.isLoaded;if(opened)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
   try{
    bool changed=false;
    if(scene.isDirty){report.AppendLine(path+" OMITIDA: cambios sin guardar");continue;}
    foreach(var root in scene.GetRootGameObjects())foreach(var ai in root.GetComponentsInChildren<EnemyShooter>(true)){
     bool sir=ai.name.Replace(" ","").Replace("_","").ToLower().Contains("sirblack");
     if(!PrefabUtility.IsPartOfPrefabInstance(ai.gameObject)&&ai.weapon&&!ai.weapon.Find(sir?"Sir_Blast":"Enemies_Blast")){ReplaceGun(ai.gameObject,sir?"Sir_Blast":"Enemies_Blast");changed=true;}
     report.AppendLine(path+" | "+ai.name+" prefab="+PrefabUtility.IsPartOfPrefabInstance(ai.gameObject)+" weapon="+(ai.weapon?string.Join(",",ai.weapon.GetComponentsInChildren<Renderer>(true).Where(r=>r.enabled).Select(r=>r.name)):"none"));
    }
    if(changed){EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);}
   }finally{if(opened&&scene.path!=active)EditorSceneManager.CloseScene(scene,true);}
  }
  File.WriteAllText("Library/NovemberScenes.txt",report.ToString());
 }
 // Reports every water surface, its solid colliders and whether a lake bed exists beneath it.
 static void WaterProbe(){
  var report=new StringBuilder();var active=SceneManager.GetActiveScene().path;
  foreach(string guid in AssetDatabase.FindAssets("t:Scene",new[]{"Assets/00_Scenes"})){
   string path=AssetDatabase.GUIDToAssetPath(guid);if(!path.Contains("Nexoterra_")||path.Contains("Menu"))continue;
   var scene=SceneManager.GetSceneByPath(path);bool opened=!scene.isLoaded;if(opened)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
   try{
    Physics.SyncTransforms();var physics=scene.GetPhysicsScene();
    foreach(var root in scene.GetRootGameObjects())foreach(var r in root.GetComponentsInChildren<Renderer>(true)){
     if(!r.sharedMaterial||!r.sharedMaterial.shader.name.Contains("Water"))continue;
     var b=r.bounds;string solid=string.Join(",",r.GetComponents<Collider>().Select(c=>c.GetType().Name+(c.enabled?"":"(off)")+(c.isTrigger?"(trigger)":"")));
     int beds=0,samples=0;
     for(float x=-.8f;x<=.81f;x+=.4f)for(float z=-.8f;z<=.81f;z+=.4f){
      samples++;var o=new Vector3(b.center.x+b.extents.x*x,b.max.y+.5f,b.center.z+b.extents.z*z);
      var hits=new RaycastHit[8];int n=physics.Raycast(o,Vector3.down,hits,60,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
      for(int i=0;i<n;i++)if(hits[i].collider.gameObject!=r.gameObject&&hits[i].point.y<b.max.y-.05f){beds++;break;}
     }
     report.AppendLine(path+" | "+r.name+" shader="+r.sharedMaterial.shader.name+" colliders=["+solid+"] bed="+beds+"/"+samples+" top="+b.max.y.ToString("0.00"));
    }
   }finally{if(opened&&scene.path!=active)EditorSceneManager.CloseScene(scene,true);}
  }
  File.WriteAllText("Library/NovemberWater.txt",report.ToString());
 }
 // The UE5 jump clips come from an A-pose rig: on the full body the arms flare out.
 // Play them only on hips + legs (masked layer) so the arms keep the pistol pose.
 static void JumpLayer(){
  const string maskPath="Assets/Characters/Animations/Jump/JumpLowerBody.mask";
  var mask=AssetDatabase.LoadAssetAtPath<AvatarMask>(maskPath);
  if(!mask){mask=new AvatarMask();AssetDatabase.CreateAsset(mask,maskPath);}
  for(int i=0;i<(int)AvatarMaskBodyPart.LastBodyPart;i++){
   var part=(AvatarMaskBodyPart)i;
   mask.SetHumanoidBodyPartActive(part,part==AvatarMaskBodyPart.Root||part==AvatarMaskBodyPart.LeftLeg||part==AvatarMaskBodyPart.RightLeg||part==AvatarMaskBodyPart.LeftFootIK||part==AvatarMaskBodyPart.RightFootIK);
  }
  EditorUtility.SetDirty(mask);
  var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/NexoterraWorld/Locomotion/Imported/Player_Animations.controller");
  var baseMachine=controller.layers[0].stateMachine;
  Motion start=null,land=null;float landSpeed=1;
  foreach(var child in baseMachine.states.ToArray()){
   if(child.state.name=="JumpStart"){start=child.state.motion;baseMachine.RemoveState(child.state);}
   else if(child.state.name=="JumpLand"){land=child.state.motion;landSpeed=child.state.speed;baseMachine.RemoveState(child.state);}
  }
  var existing=controller.layers.FirstOrDefault(l=>l.name=="JumpLower");
  if(existing==null){controller.AddLayer("JumpLower");existing=controller.layers.First(l=>l.name=="JumpLower");}
  var machine=existing.stateMachine;
  if(!start)start=machine.states.Select(s=>s.state).FirstOrDefault(s=>s.name=="JumpStart")?.motion;
  if(!land){var old=machine.states.Select(s=>s.state).FirstOrDefault(s=>s.name=="JumpLand");if(old){land=old.motion;landSpeed=old.speed;}}
  if(!start||!land)throw new Exception("Clips de salto no encontrados");
  foreach(var child in machine.states.ToArray())machine.RemoveState(child.state);
  var empty=machine.AddState("Empty");machine.defaultState=empty;
  var s1=machine.AddState("JumpStart");s1.motion=start;
  var s2=machine.AddState("JumpLand");s2.motion=land;s2.speed=landSpeed;
  var layers=controller.layers;
  for(int i=0;i<layers.Length;i++)if(layers[i].name=="JumpLower"){layers[i].avatarMask=mask;layers[i].blendingMode=AnimatorLayerBlendingMode.Override;layers[i].defaultWeight=0;}
  controller.layers=layers;
  EditorUtility.SetDirty(controller);AssetDatabase.SaveAssets();
 }
 // Renders each enemy prefab's weapon close-up so the swap can be inspected.
 static void GunShot(){
  var report=new StringBuilder();
  foreach(string role in new[]{"Enemy1","Enemy2","SirBlack","Sir_Black_2"}){
   var go=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Gameplay+"/Enemies/"+role+".prefab"));
   try{
    go.transform.position=new Vector3(0,-500,0);
    var ai=go.GetComponent<EnemyShooter>();var rs=ai.weapon.GetComponentsInChildren<Renderer>(true);
    foreach(var r in rs)report.AppendLine(role+" "+r.name+" enabled="+r.enabled+" active="+r.gameObject.activeInHierarchy+" bounds="+r.bounds.size.ToString("F3")+" center="+(r.bounds.center-go.transform.position).ToString("F2")+" mat="+(r.sharedMaterial?r.sharedMaterial.name+"/"+r.sharedMaterial.shader.name:"none"));
    var shown=rs.Where(r=>r.enabled).ToArray();if(shown.Length==0)continue;
    var b=shown[0].bounds;var camGo=new GameObject("cam");var cam=camGo.AddComponent<Camera>();
    var body=new Bounds(go.transform.position+Vector3.up*1,Vector3.one*2);
    cam.transform.position=body.center+go.transform.forward*2.6f+go.transform.right*1.2f+Vector3.up*.3f;cam.transform.LookAt(body.center);cam.fieldOfView=45;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.3f,.35f,.4f);
    var light=new GameObject("l").AddComponent<Light>();light.type=LightType.Directional;light.transform.rotation=Quaternion.Euler(40,-30,0);light.intensity=1.5f;
    var rt=new RenderTexture(640,640,24);cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;var tex=new Texture2D(640,640,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,640,640),0,0);tex.Apply();RenderTexture.active=null;
    File.WriteAllBytes("Library/Gun_"+role+".png",tex.EncodeToPNG());Object.DestroyImmediate(camGo);Object.DestroyImmediate(light.gameObject);rt.Release();
   }finally{Object.DestroyImmediate(go);}
  }
  File.WriteAllText("Library/NovemberGuns.txt",report.ToString());
 }
 // Keep only the coherent part of each clip: airborne tuck (no take-off kick) and the landing impact.
 static void TrimJump(){
  var report=new StringBuilder();
  var spans=new[]{("jump_start_ue5_manny_fit_anim_only.fbx","JumpStart",.20f,.75f,.42f),("jump_land_ue5_manny_fit_anim_only.fbx","JumpLand",.05f,.45f,.18f)};
  var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/NexoterraWorld/Locomotion/Imported/Player_Animations.controller");
  var machine=controller.layers.First(l=>l.name=="JumpLower").stateMachine;
  foreach(var (file,stateName,from,to,duration) in spans){
   string path="Assets/Characters/Animations/Jump/"+file;var importer=(ModelImporter)AssetImporter.GetAtPath(path);
   var full=importer.defaultClipAnimations[0];float first=full.firstFrame,last=full.lastFrame;
   var clips=importer.clipAnimations;
   foreach(var c in clips){c.firstFrame=first+(last-first)*from;c.lastFrame=first+(last-first)*to;}
   importer.clipAnimations=clips;importer.SaveAndReimport();
   var clip=AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().First(c=>!c.name.StartsWith("__preview"));
   var state=machine.states.Select(x=>x.state).First(x=>x.name==stateName);state.motion=clip;state.speed=clip.length/duration;
   report.AppendLine(stateName+" frames "+first+"-"+last+" -> "+clips[0].firstFrame+"-"+clips[0].lastFrame+" len="+clip.length+" speed="+state.speed);
  }
  EditorUtility.SetDirty(controller);AssetDatabase.SaveAssets();
  File.WriteAllText("Library/NovemberTrim.txt",report.ToString());
 }
 // Hand-made jump: humanoid leg-muscle clips built from the Idle pose (no imported mocap).
 const string JumpFolder="Assets/Characters/Animations/Jump";
 static readonly string[] LegMuscles={"Upper Leg Front-Back","Upper Leg In-Out","Upper Leg Twist In-Out","Lower Leg Stretch","Lower Leg Twist In-Out","Foot Up-Down","Foot Twist In-Out","Toes Up-Down"};
 static AnimationClip IdleClip(AnimatorController c){
  foreach(var child in c.layers[0].stateMachine.states)if(child.state.name=="Idle"&&child.state.motion is AnimationClip clip)return clip;
  throw new Exception("Idle no encontrado");
 }
 static float Muscle(AnimationClip idle,string name){
  var binding=EditorCurveBinding.FloatCurve("",typeof(Animator),name);var curve=AnimationUtility.GetEditorCurve(idle,binding);
  return curve!=null&&curve.length>0?curve.Evaluate(0):0;
 }
 // keys: time -> per side (front-back, stretch, foot) deltas from idle
 static AnimationClip BuildJumpClip(string name,AnimationClip idle,bool loop,params (float t,float lf,float ls,float lfoot,float rf,float rs,float rfoot)[] keys){
  string path=JumpFolder+"/"+name+".anim";var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
  if(!clip){clip=new AnimationClip();AssetDatabase.CreateAsset(clip,path);}
  clip.ClearCurves();
  // Carry the idle pose for every other channel (root, spine, arms) so the clip is a full, valid pose;
  // the legs-only layer mask ignores those channels in game.
  float end=keys[keys.Length-1].t;
  foreach(var binding in AnimationUtility.GetCurveBindings(idle)){
   if(LegMuscles.Any(m=>binding.propertyName=="Left "+m||binding.propertyName=="Right "+m))continue;
   var src=AnimationUtility.GetEditorCurve(idle,binding);if(src==null)continue;float v=src.Evaluate(0);
   AnimationUtility.SetEditorCurve(clip,binding,new AnimationCurve(new Keyframe(0,v),new Keyframe(Mathf.Max(end,.01f),v)));
  }
  foreach(string side in new[]{"Left","Right"})foreach(string m in LegMuscles){
   string muscle=side+" "+m;float baseValue=Muscle(idle,muscle);var curve=new AnimationCurve();
   foreach(var k in keys){
    float d=0;bool left=side=="Left";
    if(m=="Upper Leg Front-Back")d=left?k.lf:k.rf;else if(m=="Lower Leg Stretch")d=left?k.ls:k.rs;else if(m=="Foot Up-Down")d=left?k.lfoot:k.rfoot;
    curve.AddKey(new Keyframe(k.t,Mathf.Clamp(baseValue+d,-1,1)));
   }
   for(int i=0;i<curve.length;i++)AnimationUtility.SetKeyLeftTangentMode(curve,i,AnimationUtility.TangentMode.ClampedAuto);
   for(int i=0;i<curve.length;i++)AnimationUtility.SetKeyRightTangentMode(curve,i,AnimationUtility.TangentMode.ClampedAuto);
   AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve("",typeof(Animator),muscle),curve);
  }
  var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=loop;AnimationUtility.SetAnimationClipSettings(clip,settings);
  EditorUtility.SetDirty(clip);return clip;
 }
 static void CustomJump(){
  var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/NexoterraWorld/Locomotion/Imported/Player_Animations.controller");
  var idle=IdleClip(controller);
  float F=JumpSign;
  // Rising: knees tuck up quickly, one leg a bit higher.
  var rise=BuildJumpClip("Nexo_JumpRise",idle,false,(0,0,0,0,0,0,0),(.12f,.24f*F,-.26f,-.08f,.2f*F,-.22f,-.08f),(.35f,.3f*F,-.32f,-.1f,.24f*F,-.26f,-.1f));
  // Falling: legs open up and reach for the floor.
  var fall=BuildJumpClip("Nexo_JumpFall",idle,false,(0,.3f*F,-.32f,-.1f,.24f*F,-.26f,-.1f),(.25f,.08f*F,-.08f,-.04f,.06f*F,-.06f,-.04f));
  // Landing: quick knee absorb, then back to idle (layer fades out).
  var land=BuildJumpClip("Nexo_JumpLand",idle,false,(0,.1f*F,-.14f,.05f,.08f*F,-.12f,.05f),(.06f,.16f*F,-.24f,.1f,.15f*F,-.23f,.1f),(.2f,0,0,0,0,0,0));
  var mask=AssetDatabase.LoadAssetAtPath<AvatarMask>(JumpFolder+"/JumpLowerBody.mask");
  mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Root,false);EditorUtility.SetDirty(mask);
  var machine=controller.layers.First(l=>l.name=="JumpLower").stateMachine;
  foreach(var child in machine.states.ToArray())if(child.state.name!="Empty")machine.RemoveState(child.state);
  machine.AddState("JumpStart").motion=rise;machine.AddState("JumpFall").motion=fall;machine.AddState("JumpLand").motion=land;
  EditorUtility.SetDirty(controller);AssetDatabase.SaveAssets();
  foreach(var fbx in Directory.GetFiles(JumpFolder,"*.fbx"))AssetDatabase.DeleteAsset(fbx.Replace((char)92,'/'));
  AssetDatabase.SaveAssets();
 }
 const float JumpSign=1;
 // Walls/ceilings share Techo_cafe_claro: switch it to the procedural cave-rock shader, keeping its color.
 static void RockWalls(){
  var shader=AssetDatabase.LoadAssetAtPath<Shader>("Assets/NexoterraWorld/Shaders/CavernRock.shader");
  var report=new StringBuilder("errors="+ShaderUtil.ShaderHasError(shader));report.AppendLine();
  foreach(var m in ShaderUtil.GetShaderMessages(shader))report.AppendLine(m.severity+" "+m.message+" line "+m.line);
  if(!ShaderUtil.ShaderHasError(shader)){
   var mat=AssetDatabase.LoadAssetAtPath<Material>("Assets/NexoterraWorld/MapRepair/Techo_cafe_claro.mat");
   var color=mat.HasProperty("_BaseColor")?mat.GetColor("_BaseColor"):mat.color;
   mat.shader=shader;mat.SetColor("_BaseColor",color);mat.SetFloat("_Scale",1);mat.SetFloat("_Relief",1.2f);mat.SetFloat("_Contrast",.95f);mat.SetFloat("_Smoothness",.18f);
   EditorUtility.SetDirty(mat);AssetDatabase.SaveAssets();report.AppendLine("applied color="+color);
  }
  File.WriteAllText("Library/NovemberRock.txt",report.ToString());
 }
 // Rounded, irregular rock fillets along the wall/ceiling edges and the vertical room corners,
 // so the campaign caverns stop looking like a box. Uses the same cave-rock material.
 static Mesh Fillet(Vector3 start,Vector3 dir,float length,Vector3 a,Vector3 b,float baseR,int seed){
  int n=Mathf.Max(2,Mathf.CeilToInt(length/2f)),m=12;var verts=new List<Vector3>();var tris=new List<int>();
  for(int i=0;i<=n;i++){
   float t=i/(float)n,along=t*length;var c=start+dir*along;
   float r=baseR*(.7f+.6f*Mathf.PerlinNoise(seed*.37f+along*.045f,seed*.11f));
   var o=c+(a+b)*r;
   for(int j=0;j<=m;j++){
    float th=j/(float)m*Mathf.PI*.5f;var normal=(a*Mathf.Cos(th)+b*Mathf.Sin(th)).normalized;
    float lump=(Mathf.PerlinNoise(seed+along*.18f,th*2.5f)-.45f)*r*.35f*Mathf.Sin(2*th);
    verts.Add(o-normal*r+normal*lump);
   }
  }
  for(int i=0;i<n;i++)for(int j=0;j<m;j++){int k=i*(m+1)+j;tris.AddRange(new[]{k,k+1,k+m+1,k+1,k+m+2,k+m+1});}
  var mesh=new Mesh{indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};mesh.SetVertices(verts);mesh.SetTriangles(tris,0);mesh.RecalculateNormals();
  // Faces must point into the room (towards a+b).
  var nrm=mesh.normals;if(Vector3.Dot(nrm[m/2],a+b)<0){tris.Reverse();mesh.SetTriangles(tris,0);mesh.RecalculateNormals();}
  mesh.RecalculateBounds();return mesh;
 }
 static GameObject FindIn(Scene scene,string name){foreach(var r in scene.GetRootGameObjects())foreach(var t in r.GetComponentsInChildren<Transform>(true))if(t.name==name)return t.gameObject;return null;}
 static void CaveCorners(){
  var report=new StringBuilder();string active=SceneManager.GetActiveScene().path;
  var mat=AssetDatabase.LoadAssetAtPath<Material>("Assets/NexoterraWorld/MapRepair/Techo_cafe_claro.mat");
  const string folder="Assets/NexoterraWorld/Campaign/CaveCorners";Directory.CreateDirectory(folder);
  foreach(var file in Directory.GetFiles("Assets/00_Scenes/Campaign","*.unity")){
   string path=file.Replace((char)92,'/');var scene=SceneManager.GetSceneByPath(path);bool opened=!scene.isLoaded;
   if(opened)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
   try{
    if(scene.isDirty){report.AppendLine(path+" OMITIDA (sin guardar)");continue;}
    var west=FindIn(scene,"Pared oeste");var east=FindIn(scene,"Pared este");var south=FindIn(scene,"Pared entrada");var north=FindIn(scene,"Pared salida izquierda");var roof=FindIn(scene,"Techo");
    if(!west||!east||!south||!north||!roof){report.AppendLine(path+" sin paredes estándar");continue;}
    float x0=west.GetComponent<Renderer>().bounds.max.x,x1=east.GetComponent<Renderer>().bounds.min.x,z0=south.GetComponent<Renderer>().bounds.max.z,z1=north.GetComponent<Renderer>().bounds.min.z,top=roof.GetComponent<Renderer>().bounds.min.y,bottom=-1;
    var old=FindIn(scene,"Esquinas de caverna");if(old)Object.DestroyImmediate(old);
    var root=new GameObject("Esquinas de caverna");SceneManager.MoveGameObjectToScene(root,scene);
    string name=Path.GetFileNameWithoutExtension(path);
    var parts=new (string id,Vector3 start,Vector3 dir,float len,Vector3 a,Vector3 b,float r)[]{
     ("techo_oeste",new Vector3(x0,top,z0-4),Vector3.forward,z1-z0+8,Vector3.right,Vector3.down,6),
     ("techo_este",new Vector3(x1,top,z0-4),Vector3.forward,z1-z0+8,Vector3.left,Vector3.down,6),
     ("techo_entrada",new Vector3(x0-4,top,z0),Vector3.right,x1-x0+8,Vector3.forward,Vector3.down,6),
     ("techo_salida",new Vector3(x0-4,top,z1),Vector3.right,x1-x0+8,Vector3.back,Vector3.down,6),
     ("rincon_so",new Vector3(x0,bottom,z0),Vector3.up,top-bottom,Vector3.right,Vector3.forward,4.5f),
     ("rincon_se",new Vector3(x1,bottom,z0),Vector3.up,top-bottom,Vector3.left,Vector3.forward,4.5f),
     ("rincon_no",new Vector3(x0,bottom,z1),Vector3.up,top-bottom,Vector3.right,Vector3.back,4.5f),
     ("rincon_ne",new Vector3(x1,bottom,z1),Vector3.up,top-bottom,Vector3.left,Vector3.back,4.5f)};
    int seed=name.GetHashCode()&1023;
    foreach(var p in parts){
     var mesh=Fillet(p.start,p.dir,p.len,p.a,p.b,p.r,seed+=7);
     string assetPath=folder+"/"+name+"_"+p.id+".asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
     if(existing){EditorUtility.CopySerialized(mesh,existing);Object.DestroyImmediate(mesh);mesh=existing;EditorUtility.SetDirty(existing);}else AssetDatabase.CreateAsset(mesh,assetPath);
     var go=new GameObject("Roca "+p.id);go.transform.SetParent(root.transform,false);go.isStatic=true;
     go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=mat;go.AddComponent<MeshCollider>().sharedMesh=mesh;
    }
    EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
    report.AppendLine(path+" OK x["+x0+","+x1+"] z["+z0+","+z1+"] techo "+top);
   }finally{if(opened&&scene.path!=active)EditorSceneManager.CloseScene(scene,true);}
  }
  AssetDatabase.SaveAssets();File.WriteAllText("Library/NovemberCorners.txt",report.ToString());
 }
 // UI kit sprites: crisp import. Game icon (.exe): Branding/Nexoterra_Icono_Protagonista_1024.png as default icon.
 static void UiKit(){
  var report=new StringBuilder();
  foreach(var file in Directory.GetFiles("Assets/Resources/Nexoterra/UI/Kit","*.png")){
   var path=file.Replace((char)92,'/');var ti=(TextureImporter)AssetImporter.GetAtPath(path);
   ti.textureType=TextureImporterType.Default;ti.alphaIsTransparency=true;ti.mipmapEnabled=false;ti.textureCompression=TextureImporterCompression.Uncompressed;ti.wrapMode=TextureWrapMode.Clamp;ti.npotScale=TextureImporterNPOTScale.None;ti.SaveAndReimport();
  }
  const string iconPath="Assets/Branding/Nexoterra_Icono_Protagonista_1024.png";
  var ii=(TextureImporter)AssetImporter.GetAtPath(iconPath);
  if(ii){ii.textureType=TextureImporterType.Default;ii.alphaIsTransparency=true;ii.maxTextureSize=1024;ii.textureCompression=TextureImporterCompression.Uncompressed;ii.SaveAndReimport();
   var icon=AssetDatabase.LoadAssetAtPath<Texture2D>(iconPath);
   PlayerSettings.SetIcons(UnityEditor.Build.NamedBuildTarget.Unknown,new[]{icon},IconKind.Any);
   report.AppendLine("icon="+icon.name+" "+icon.width);}
  else report.AppendLine("icono no encontrado");
  PlayerSettings.productName="Nexoterra";
  AssetDatabase.SaveAssets();File.WriteAllText("Library/NovemberKit.txt",report.ToString());
 }
 // Open the campaign levels: no box. Roof, wall visuals, roof fillets and ceiling spikes go away (wall colliders stay as an
 // invisible boundary), an irregular rock rim surrounds the play area with a cave mouth at the exit tunnel, and a dark
 // cavern sky replaces the roof. Nexoterra_Tierra keeps its roof with falling stone spikes.
 static Mesh RimSide(Vector3 origin,Vector3 along,Vector3 outward,float length,int seed,float gapFrom,float gapTo){
  var verts=new List<Vector3>();var tris=new List<int>();
  var profile=new[]{(-6.5f,-.03f),(-4.5f,.35f),(-2f,.82f),(0f,1f),(5f,.97f),(18f,.6f)};int m=profile.Length;
  int n=Mathf.CeilToInt(length/2f);
  for(int i=0;i<=n;i++){
   float t=-length/2+length*i/n;float H=16+12*Mathf.PerlinNoise(seed+t*.06f,seed*.3f)+5f*Mathf.PerlinNoise(seed*2+t*.25f,1.7f); // tall cliffs: 16-33 m
   float push=(t>gapFrom&&t<gapTo)?6.5f:0;float jitter=(Mathf.PerlinNoise(seed+t*.2f,4.2f)-.5f)*3f;
   for(int j=0;j<m;j++){var (d,h)=profile[j];float dd=d+push+(j<3?jitter*(1-j/3f):0);
    verts.Add(origin+along*t+outward*dd+Vector3.up*(h*H+(j>0&&j<5?(Mathf.PerlinNoise(t*.4f+j,seed)-.5f)*1.4f:0)));}
  }
  for(int i=0;i<n;i++)for(int j=0;j<m-1;j++){int k=i*m+j;tris.AddRange(new[]{k,k+m,k+1,k+1,k+m,k+m+1});}
  var mesh=new Mesh{indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};mesh.SetVertices(verts);mesh.SetTriangles(tris,0);mesh.RecalculateNormals();
  var nrm=mesh.normals;if(Vector3.Dot(nrm[1],-outward+Vector3.up)<0){tris.Reverse();mesh.SetTriangles(tris,0);mesh.RecalculateNormals();}
  mesh.RecalculateBounds();return mesh;
 }
 static void OpenLevels(){
  var report=new StringBuilder();string active=SceneManager.GetActiveScene().path;
  var rock=AssetDatabase.LoadAssetAtPath<Material>("Assets/NexoterraWorld/MapRepair/Techo_cafe_claro.mat");
  const string folder="Assets/NexoterraWorld/Campaign/OpenRim";Directory.CreateDirectory(folder);
  var sky=AssetDatabase.LoadAssetAtPath<Material>(folder+"/CavernSky.mat");
  if(!sky){sky=new Material(Shader.Find("Skybox/Procedural"));AssetDatabase.CreateAsset(sky,folder+"/CavernSky.mat");}
  sky.SetFloat("_SunSize",0);sky.SetFloat("_AtmosphereThickness",.55f);sky.SetColor("_SkyTint",new Color(.32f,.22f,.45f));sky.SetColor("_GroundColor",new Color(.08f,.06f,.12f));sky.SetFloat("_Exposure",.55f);EditorUtility.SetDirty(sky);
  foreach(var file in Directory.GetFiles("Assets/00_Scenes/Campaign","*.unity")){
   string path=file.Replace((char)92,'/');string name=Path.GetFileNameWithoutExtension(path);
   if(name.Contains("Tierra")||name.Contains("Arena")){report.AppendLine(name+" se conserva");continue;}
   var scene=SceneManager.GetSceneByPath(path);bool opened=!scene.isLoaded;if(opened)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
   try{
    if(scene.isDirty){report.AppendLine(name+" OMITIDA (sin guardar)");continue;}
    foreach(var root in scene.GetRootGameObjects())foreach(var t in root.GetComponentsInChildren<Transform>(true)){
     string n=t.name;
     if(n=="Techo"||n=="Arco sobre túnel"){foreach(var c in t.GetComponents<Collider>())c.enabled=false;var r=t.GetComponent<Renderer>();if(r)r.enabled=false;}
     else if(n.StartsWith("Pared ")){var r=t.GetComponent<Renderer>();if(r)r.enabled=false;}
     else if(n=="Esquinas de caverna"||n.StartsWith("CeilingRock"))t.gameObject.SetActive(false);
    }
    var old=FindIn(scene,"Borde rocoso");if(old)Object.DestroyImmediate(old);
    var rim=new GameObject("Borde rocoso");SceneManager.MoveGameObjectToScene(rim,scene);
    var exit=FindIn(scene,"Salida al próximo bioma");float exitX=exit?exit.transform.position.x:80;
    var sides=new (string id,Vector3 o,Vector3 along,Vector3 outv,float gf,float gt)[]{
     ("norte",new Vector3(0,0,125),Vector3.right,Vector3.forward,exitX-11,exitX+11),("sur",new Vector3(0,0,-125),Vector3.right,Vector3.back,1,-1),
     ("este",new Vector3(125,0,0),Vector3.forward,Vector3.right,1,-1),("oeste",new Vector3(-125,0,0),Vector3.forward,Vector3.left,1,-1)};
    int seed=name.GetHashCode()&511;
    foreach(var s in sides){
     var mesh=RimSide(s.o,s.along,s.outv,282,seed+=13,s.gf,s.gt);string ap=folder+"/"+name+"_"+s.id+".asset";var ex=AssetDatabase.LoadAssetAtPath<Mesh>(ap);
     if(ex){EditorUtility.CopySerialized(mesh,ex);Object.DestroyImmediate(mesh);mesh=ex;EditorUtility.SetDirty(ex);}else AssetDatabase.CreateAsset(mesh,ap);
     var go=new GameObject("Roca borde "+s.id);go.transform.SetParent(rim.transform,false);go.isStatic=true;
     go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=rock;go.AddComponent<MeshCollider>().sharedMesh=mesh;
    }
    SceneManager.SetActiveScene(scene);RenderSettings.skybox=sky;RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Trilight;
    RenderSettings.fogColor=new Color(.13f,.1f,.18f);
    EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);report.AppendLine(name+" abierta");
   }finally{if(opened&&scene.path!=active)EditorSceneManager.CloseScene(scene,true);}
  }
  var a=SceneManager.GetSceneByPath(active);if(a.IsValid())SceneManager.SetActiveScene(a);
  AssetDatabase.SaveAssets();File.WriteAllText("Library/NovemberOpen.txt",report.ToString());
 }
 // Biome floors (Superficie_0..5) use the procedural ground shader, keeping each biome's color.
 static void GroundMats(){
  var shader=AssetDatabase.LoadAssetAtPath<Shader>("Assets/NexoterraWorld/Shaders/CavernGround.shader");
  var report=new StringBuilder("errors="+ShaderUtil.ShaderHasError(shader));report.AppendLine();
  foreach(var m in ShaderUtil.GetShaderMessages(shader))report.AppendLine(m.severity+" "+m.message+" line "+m.line);
  if(!ShaderUtil.ShaderHasError(shader)){
   var paths=new[]{new Color(.5f,.34f,.2f),new Color(.6f,.65f,.7f),new Color(.42f,.34f,.22f),new Color(.62f,.44f,.26f),new Color(.26f,.21f,.19f),new Color(.36f,.16f,.17f)};
   var flowers=new[]{.6f,0f,.4f,.3f,.1f,.25f};
   for(int i=0;i<6;i++){
    var mat=AssetDatabase.LoadAssetAtPath<Material>("Assets/NexoterraWorld/Habitats/Materials/Superficie_"+i+".mat");if(!mat)continue;
    var c=mat.HasProperty("_BaseColor")?mat.GetColor("_BaseColor"):Color.green;
    mat.shader=shader;mat.SetColor("_BaseColor",c);mat.SetColor("_PathColor",paths[i]);mat.SetFloat("_Flowers",flowers[i]);mat.SetFloat("_Paths",.6f);mat.SetFloat("_Scale",1);mat.SetFloat("_Relief",1);mat.SetFloat("_Smoothness",i==1?.3f:.1f);
    EditorUtility.SetDirty(mat);report.AppendLine("Superficie_"+i+" ok");
   }
   AssetDatabase.SaveAssets();
  }
  File.WriteAllText("Library/NovemberGround.txt",report.ToString());
 }
 // Terraced hills for the open campaign levels. Height is 0 around the entrance, exit, water and landmark
 // structures; everything resting on the floor is lifted by the same height and the NavMesh is rebuilt.
 // Idempotent: the flat original mesh is kept as <mesh>_plano.asset and scenes get a "Relieve aplicado" marker.
 static float Relief(float x,float z,int seed){
  float b=0,a=.6f,f=.011f;for(int o=0;o<3;o++){b+=a*Mathf.PerlinNoise(seed*.37f+x*f+31,seed*.53f+z*f+17);a*=.45f;f*=2.1f;}
  b=Mathf.Clamp01((b-.28f)*1.6f);float t=b*4f,level=Mathf.Floor(t),frac=t-level;
  return (level+Mathf.SmoothStep(0,1,Mathf.InverseLerp(.72f,1f,frac)))*1.35f;
 }
 // Smooth rolling hills (no steps), max ~2.6 m with gentle walkable slopes.
 static float SoftRelief(float x,float z,int seed){
  float b=.65f*Mathf.PerlinNoise(seed*.37f+x*.009f+31,seed*.53f+z*.009f+17)+.35f*Mathf.PerlinNoise(seed*.21f+x*.02f+5,seed*.11f+z*.02f+9);
  b=Mathf.SmoothStep(0,1,Mathf.Clamp01((b-.3f)*1.7f));return b*2.6f;
 }
 static bool softMode,soft;
 static void Relief(){Relief(1f,0f);}
 static void Relief(float scale,float previous){
  var report=new StringBuilder();string active=SceneManager.GetActiveScene().path;var doneMeshes=new HashSet<string>();
  foreach(var file in Directory.GetFiles("Assets/00_Scenes/Campaign","*.unity")){
   string path=file.Replace((char)92,'/');string name=Path.GetFileNameWithoutExtension(path);
   if(name.Contains("Arena")){continue;}
   var scene=SceneManager.GetSceneByPath(path);bool opened=!scene.isLoaded;if(opened)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
   try{
    if(scene.isDirty){report.AppendLine(name+" OMITIDA (sin guardar)");continue;}
    var prevMarker=FindIn(scene,"Relieve aplicado");if(previous<=0&&prevMarker){report.AppendLine(name+" ya tenía relieve");continue;}if(previous>0&&!prevMarker){report.AppendLine(name+" sin relieve previo");continue;}if(prevMarker)Object.DestroyImmediate(prevMarker);
    var ground=FindIn(scene,"Suelo del bioma");if(!ground){report.AppendLine(name+" sin suelo");continue;}
    // Protected zones (kept flat).
    var keep=new List<Bounds>();
    foreach(var root in scene.GetRootGameObjects())foreach(var r in root.GetComponentsInChildren<Renderer>(true)){
     string n=r.name;bool water=r.sharedMaterial&&r.sharedMaterial.shader.name.Contains("Water");
     if(water||n.StartsWith("Cornisa")||n.StartsWith("Puente")||n.StartsWith("Sendero")||n.StartsWith("Santuario")||n.StartsWith("Túnel")||n.StartsWith("Islote")||n.StartsWith("Orillas")||n.StartsWith("Acceso"))keep.Add(r.bounds);
    }
    foreach(var n in new[]{"Entrada","Salida al próximo bioma","Recompensa del guardián"}){var g=FindIn(scene,n);if(g)keep.Add(new Bounds(g.transform.position,Vector3.one*14));}
    int seed=name.GetHashCode()&255;
    float H(Vector3 p){
     float m=1;foreach(var b in keep){float d=Mathf.Sqrt(b.SqrDistance(new Vector3(p.x,b.center.y,p.z)));m=Mathf.Min(m,Mathf.SmoothStep(0,1,Mathf.InverseLerp(4,16,d)));}
     return (softMode?SoftRelief(p.x,p.z,seed):Relief(p.x,p.z,seed))*m;
    }
    var mf=ground.GetComponent<MeshFilter>();var mesh=mf.sharedMesh;string mp=AssetDatabase.GetAssetPath(mesh);
    string flatPath=mp.Replace(".asset","_plano.asset");var flat=AssetDatabase.LoadAssetAtPath<Mesh>(flatPath);
    if(!flat){flat=Object.Instantiate(mesh);AssetDatabase.CreateAsset(flat,flatPath);}
    var shaped=Object.Instantiate(flat);string shapedPath=mp.Replace(".asset","_"+name+"_relieve.asset");
    var tr=ground.transform;var v=flat.vertices;
    for(int i=0;i<v.Length;i++){var w=tr.TransformPoint(v[i]);w.y+=H(w)*scale;v[i]=tr.InverseTransformPoint(w);}
    float OldH(Vector3 q){softMode=false;float o=H(q)*previous;softMode=soft;return o;}
    shaped.vertices=v;shaped.RecalculateNormals();shaped.RecalculateBounds();
    var ex=AssetDatabase.LoadAssetAtPath<Mesh>(shapedPath);if(ex){EditorUtility.CopySerialized(shaped,ex);Object.DestroyImmediate(shaped);shaped=ex;}else AssetDatabase.CreateAsset(shaped,shapedPath);
    mf.sharedMesh=shaped;var mc=ground.GetComponent<MeshCollider>();if(mc){mc.sharedMesh=null;mc.sharedMesh=shaped;}
    // Lift things that stand on the floor.
    int moved=0;var skip=new[]{"Suelo del bioma","Borde rocoso","Techo","Esquinas de caverna","Luz de caverna","Main Camera","Navegación","Objetivo de historia"};
    void Lift(Transform t){var p=t.position;float h=soft?H(p)*scale-OldH(p):H(p)*(scale-previous);if(Mathf.Abs(h)>.01f){t.position=p+Vector3.up*h;moved++;}}
    foreach(var root in scene.GetRootGameObjects()){
     if(skip.Any(s=>root.name.StartsWith(s))||root.name.StartsWith("Pared")||root.name.StartsWith("Túnel")||root.name.StartsWith("Fondo")||root.name.StartsWith("Arco"))continue;
     bool container=!root.GetComponent<Renderer>()&&!root.GetComponent<Collider>()&&!root.GetComponent<MonoBehaviour>()&&root.transform.childCount>0&&root.transform.position.sqrMagnitude<.01f;
     if(container)foreach(Transform c in root.transform)Lift(c);else Lift(root.transform);
    }
    var marker=new GameObject("Relieve aplicado");SceneManager.MoveGameObjectToScene(marker,scene);
    // Rebuild navigation.
    foreach(var root in scene.GetRootGameObjects())foreach(var comp in root.GetComponentsInChildren<MonoBehaviour>(true)){
     if(comp&&comp.GetType().Name=="NavMeshSurface"){comp.GetType().GetMethod("BuildNavMesh")?.Invoke(comp,null);report.AppendLine("  navmesh "+comp.name);}
    }
    EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);report.AppendLine(name+" relieve OK, objetos elevados: "+moved+", zonas planas: "+keep.Count);
   }finally{if(opened&&scene.path!=active)EditorSceneManager.CloseScene(scene,true);}
  }
  AssetDatabase.SaveAssets();File.WriteAllText("Library/NovemberRelief.txt",report.ToString());
 }
 static void Renav(){
  var report=new StringBuilder();string active=SceneManager.GetActiveScene().path;
  foreach(var file in Directory.GetFiles("Assets/00_Scenes/Campaign","*.unity")){
   string path=file.Replace((char)92,'/');if(path.Contains("Arena"))continue;
   var scene=SceneManager.GetSceneByPath(path);bool opened=!scene.isLoaded;if(opened)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
   try{
    var level=scene.GetRootGameObjects().Select(r=>r.GetComponentInChildren<CampaignLevel>(true)).FirstOrDefault(l=>l);
    if(!level){report.AppendLine(path+" sin nivel");continue;}
    CampaignLandmarkRestore.RebuildNavigation(scene,level.index);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);report.AppendLine(path+" navmesh nivel "+level.index);
   }catch(Exception e){report.AppendLine(path+" ERROR "+e.Message);}
   finally{if(opened&&scene.path!=active)EditorSceneManager.CloseScene(scene,true);}
  }
  File.WriteAllText("Library/NovemberRenav.txt",report.ToString());
 }
 // Coherent dirt paths: trails from the entrance to a central clearing, on to the exit, and branches to the
 // guardian and the reward, plus a loop around the clearing. Stored in the ground mesh colors (R = path, G = 0).
 static List<Vector2> Trail(Vector2 a,Vector2 b,int seed){
  var pts=new List<Vector2>();float len=Vector2.Distance(a,b);int n=Mathf.Max(2,Mathf.CeilToInt(len/3));var dir=(b-a).normalized;var side=new Vector2(-dir.y,dir.x);
  for(int i=0;i<=n;i++){float t=i/(float)n;float wob=(Mathf.PerlinNoise(seed+t*len*.025f,seed*.7f)-.5f)*Mathf.Min(18,len*.18f)*Mathf.Sin(t*Mathf.PI);pts.Add(Vector2.Lerp(a,b,t)+side*wob);}
  return pts;
 }
 static float SegDist(Vector2 p,Vector2 a,Vector2 b){var ab=b-a;float t=Mathf.Clamp01(Vector2.Dot(p-a,ab)/Mathf.Max(.0001f,ab.sqrMagnitude));return Vector2.Distance(p,a+ab*t);}
 static void Routes(){
  var report=new StringBuilder();string active=SceneManager.GetActiveScene().path;
  foreach(var file in Directory.GetFiles("Assets/00_Scenes/Campaign","*.unity")){
   string path=file.Replace((char)92,'/');string name=Path.GetFileNameWithoutExtension(path);if(name.Contains("Arena"))continue;
   var scene=SceneManager.GetSceneByPath(path);bool opened=!scene.isLoaded;if(opened)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
   try{
    var ground=FindIn(scene,"Suelo del bioma");var level=scene.GetRootGameObjects().Select(r=>r.GetComponentInChildren<CampaignLevel>(true)).FirstOrDefault(l=>l);
    if(!ground||!level){report.AppendLine(name+" sin suelo/nivel");continue;}
    Vector2 P(Vector3 v)=>new Vector2(v.x,v.z);int seed=name.GetHashCode()&255;
    var entry=FindIn(scene,"Entrada");var e=entry?P(entry.transform.position):new Vector2(0,-118);
    var exitGo=FindIn(scene,"Salida al próximo bioma");var x=exitGo?P(exitGo.transform.position):new Vector2(80,120);
    var c=new Vector2((Mathf.PerlinNoise(seed,1)-.5f)*30,(Mathf.PerlinNoise(seed,2)-.5f)*30);
    var trails=new List<List<Vector2>>{Trail(e,c,seed),Trail(c,x,seed+1)};
    if(level.guardian)trails.Add(Trail(c,P(level.guardian.transform.position),seed+2));
    if(level.rewardPoint)trails.Add(Trail(c,P(level.rewardPoint.position),seed+3));
    var ring=new List<Vector2>();for(int i=0;i<=24;i++){float a=i/24f*Mathf.PI*2;float r=38+8*Mathf.PerlinNoise(seed+i*.4f,3);ring.Add(c+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*r);}trails.Add(ring);
    var mf=ground.GetComponent<MeshFilter>();var mesh=mf.sharedMesh;var tr=ground.transform;var v=mesh.vertices;var colors=new Color[v.Length];
    for(int i=0;i<v.Length;i++){
     var p=P(tr.TransformPoint(v[i]));float d=float.MaxValue;
     foreach(var t in trails)for(int k=1;k<t.Count;k++)d=Mathf.Min(d,SegDist(p,t[k-1],t[k]));
     float w=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(1.4f,3.6f,d));colors[i]=new Color(w,0,0,1);
    }
    mesh.colors=colors;EditorUtility.SetDirty(mesh);report.AppendLine(name+" caminos: "+trails.Count+" ("+AssetDatabase.GetAssetPath(mesh)+")");
   }finally{if(opened&&scene.path!=active)EditorSceneManager.CloseScene(scene,true);}
  }
  AssetDatabase.SaveAssets();File.WriteAllText("Library/NovemberRoutes.txt",report.ToString());
 }
 // Rebuild each level's ground from the builder's ORIGINAL mesh + smooth hills (objects already match it),
 // then drop the intermediate relief meshes.
 static void FixGround(){
  var report=new StringBuilder();string active=SceneManager.GetActiveScene().path;var used=new HashSet<string>();
  foreach(var file in Directory.GetFiles("Assets/00_Scenes/Campaign","*.unity")){
   string path=file.Replace((char)92,'/');string name=Path.GetFileNameWithoutExtension(path);if(name.Contains("Arena"))continue;
   var scene=SceneManager.GetSceneByPath(path);bool opened=!scene.isLoaded;if(opened)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
   try{
    var ground=FindIn(scene,"Suelo del bioma");if(!ground)continue;var mf=ground.GetComponent<MeshFilter>();
    string cur=AssetDatabase.GetAssetPath(mf.sharedMesh);string basePath=cur.Contains("_Nexoterra_")?cur.Substring(0,cur.IndexOf("_Nexoterra_"))+".asset":cur;
    var original=AssetDatabase.LoadAssetAtPath<Mesh>(basePath);if(!original){report.AppendLine(name+" sin malla original "+basePath);continue;}
    var keep=new List<Bounds>();
    foreach(var root in scene.GetRootGameObjects())foreach(var r in root.GetComponentsInChildren<Renderer>(true)){
     string n=r.name;bool water=r.sharedMaterial&&r.sharedMaterial.shader.name.Contains("Water");
     if(water||n.StartsWith("Cornisa")||n.StartsWith("Puente")||n.StartsWith("Sendero")||n.StartsWith("Santuario")||n.StartsWith("Túnel")||n.StartsWith("Islote")||n.StartsWith("Orillas")||n.StartsWith("Acceso"))keep.Add(r.bounds);
    }
    foreach(var n in new[]{"Entrada","Salida al próximo bioma","Recompensa del guardián"}){var g=FindIn(scene,n);if(g)keep.Add(new Bounds(g.transform.position,Vector3.one*14));}
    int seed=name.GetHashCode()&255;
    float H(Vector3 p){float m=1;foreach(var b in keep){float d=Mathf.Sqrt(b.SqrDistance(new Vector3(p.x,b.center.y,p.z)));m=Mathf.Min(m,Mathf.SmoothStep(0,1,Mathf.InverseLerp(4,16,d)));}return SoftRelief(p.x,p.z,seed)*m;}
    var shaped=Object.Instantiate(original);var tr=ground.transform;var v=original.vertices;
    for(int i=0;i<v.Length;i++){var w=tr.TransformPoint(v[i]);w.y+=H(w);v[i]=tr.InverseTransformPoint(w);}
    shaped.vertices=v;shaped.RecalculateNormals();shaped.RecalculateBounds();
    string outPath=basePath.Replace(".asset","_"+name+"_suave.asset");var ex=AssetDatabase.LoadAssetAtPath<Mesh>(outPath);
    if(ex){EditorUtility.CopySerialized(shaped,ex);Object.DestroyImmediate(shaped);shaped=ex;}else AssetDatabase.CreateAsset(shaped,outPath);
    mf.sharedMesh=shaped;var mc=ground.GetComponent<MeshCollider>();if(mc){mc.sharedMesh=null;mc.sharedMesh=shaped;}used.Add(outPath);
    EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);report.AppendLine(name+" -> "+outPath);
   }finally{if(opened&&scene.path!=active)EditorSceneManager.CloseScene(scene,true);}
  }
  foreach(var f in Directory.GetFiles("Assets/NexoterraWorld/Campaign","*.asset")){var p=f.Replace((char)92,'/');if((p.Contains("_relieve")||p.Contains("_plano"))&&!used.Contains(p)){AssetDatabase.DeleteAsset(p);report.AppendLine("borrado "+Path.GetFileName(p));}}
  AssetDatabase.SaveAssets();File.WriteAllText("Library/NovemberFix.txt",report.ToString());
 }
 public static void Audit(){
  var report=new StringBuilder();
  foreach(var file in Directory.GetFiles("Assets/Characters/Animations/Jump","*.fbx")){
   var avatar=AssetDatabase.LoadAllAssetsAtPath(file.Replace((char)92,'/')).OfType<Avatar>().FirstOrDefault();report.AppendLine(file+" human="+(avatar&&avatar.isHuman&&avatar.isValid));
  }
  foreach(string role in new[]{"Enemy1","Enemy2","SirBlack","Sir_Black_2"}){
   var go=AssetDatabase.LoadAssetAtPath<GameObject>(Gameplay+"/Enemies/"+role+".prefab");var ai=go.GetComponent<EnemyShooter>();
   report.AppendLine(role+" weapon="+string.Join(",",ai.weapon.GetComponentsInChildren<Renderer>(true).Where(r=>r.enabled).Select(r=>r.name)));
  }
  foreach(var path in new[]{"Assets/NexoterraWorld/Shaders/CavernWater.shader","Assets/NexoterraWorld/Shaders/SanctuaryWater.shader"}){
   var shader=AssetDatabase.LoadAssetAtPath<Shader>(path);report.AppendLine(path+" errors="+ShaderUtil.ShaderHasError(shader));
  }
  File.WriteAllText("Library/NovemberAudit.txt",report.ToString());
 }
}
