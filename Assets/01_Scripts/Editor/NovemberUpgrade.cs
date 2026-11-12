using System;
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
  try{if(mode=="install")Install();else if(mode=="audit")Audit();else if(mode=="scenes")SceneWeapons();else if(mode=="water")WaterProbe();else if(mode=="jumplayer")JumpLayer();else if(mode=="gunshot")GunShot();else if(mode=="trimjump")TrimJump();else if(mode=="customjump")CustomJump();else AssetDatabase.Refresh();File.WriteAllText("Library/NovemberUpgrade.result","PASS "+mode);}
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
  var rise=BuildJumpClip("Nexo_JumpRise",idle,false,(0,0,0,0,0,0,0),(.12f,.55f*F,-.75f,.25f,.35f*F,-.55f,.2f),(.35f,.65f*F,-.95f,.3f,.42f*F,-.7f,.25f));
  // Falling: legs open up and reach for the floor.
  var fall=BuildJumpClip("Nexo_JumpFall",idle,false,(0,.6f*F,-.9f,.3f,.4f*F,-.65f,.25f),(.25f,.22f*F,-.28f,.12f,.12f*F,-.2f,.1f));
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
