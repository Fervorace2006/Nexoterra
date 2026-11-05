using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using Nexoterra.World;
using Random=UnityEngine.Random;
using Geo=Nexoterra.Editor.NexoterraCavernBuilder.Geometry;

namespace Nexoterra.Editor
{
    [InitializeOnLoad]
    public static class NexoterraBiomeBuilder
    {
        const string Folder="Assets/NexoterraWorld/Biomes";
        public const string ScenePath="Assets/00_Scenes/Nexoterra_CuatroNodos.unity";
        const string Request="Library/NexoterraBiomes.request",Result="Library/NexoterraBiomes.result.txt";
        static Transform world;
        static Material rock,ice,cyan,lava,dirty,frozen,stem;
        static Material[] glows;
        static readonly Vector3 VolcanoCenter=new Vector3(190,0,230);
        static readonly string[] BiomeNames={"Jardines del Nexo","Cavernas de hielo","Tierras antiguas","Caldera volcanica","Galeria acuatica","Cienaga carmesi"};
        static readonly Vector3[] Visits={new Vector3(275,0,-270),new Vector3(-225,0,5),new Vector3(-210,0,195),new Vector3(190,0,170),new Vector3(105,0,-48),new Vector3(-230,0,-220)};
        static readonly Color[] Palette={new Color(.29f,.47f,.20f),new Color(.49f,.77f,.88f),new Color(.45f,.29f,.15f),new Color(.33f,.065f,.035f),new Color(.12f,.37f,.38f),new Color(.13f,.035f,.045f)};
        static NexoterraBiomeBuilder(){EditorApplication.update+=Poll;}
        static void Poll()
        {
            if(EditorApplication.isCompiling||EditorApplication.isUpdating||!File.Exists(Request))return;
            var command=File.ReadAllText(Request).Trim();File.Delete(Request);
            try{if(command=="raise-roof")RaiseRoofAndRefreshSpider();else if(command=="build")Build();else if(command=="capture")Capture();else if(command=="validate")Validate();else if(command=="open"){var s=SceneManager.GetActiveScene();if(s.isDirty&&!string.IsNullOrEmpty(s.path))EditorSceneManager.SaveScene(s);EditorSceneManager.OpenScene(ScenePath);}else if(command=="play")EditorApplication.isPlaying=true;else if(command=="stop")EditorApplication.isPlaying=false;}
            catch(Exception e){File.WriteAllText(Result,"FAIL\n"+e);Debug.LogException(e);}
        }
        static int Biome(Vector3 p){if(p.x>=0)return p.z>80?3:p.z>-80?4:0;return p.z>80?2:p.z>-80?1:5;}
        static float RoofLift(Vector3 p){float d=Vector2.Distance(new Vector2(p.x,p.z),new Vector2(VolcanoCenter.x,VolcanoCenter.z));return 4+(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(22,78,d)))*19;}

        [MenuItem("Nexoterra/Mundo/Construir cuatro nodos con biomas")]
        public static void BuildAndCapture(){Build();Capture();}

        public static void Build()
        {
            if(EditorApplication.isPlaying)throw new Exception("Salir de Play antes de construir.");
            // Preserve any current scene edits, including the user's recently placed models.
            var active=SceneManager.GetActiveScene();
            if(active.isDirty&&!string.IsNullOrEmpty(active.path))EditorSceneManager.SaveScene(active);
            var sourceScene=SceneManager.GetSceneByPath(NexoterraCavernBuilder.ScenePath);
            if(!sourceScene.isLoaded)sourceScene=EditorSceneManager.OpenScene(NexoterraCavernBuilder.ScenePath,Application.isBatchMode?OpenSceneMode.Single:OpenSceneMode.Additive);
            var previousWorld=SceneManager.GetSceneByPath(ScenePath);
            if(previousWorld.isLoaded)EditorSceneManager.CloseScene(previousWorld,true);
            var source=sourceScene.GetRootGameObjects().FirstOrDefault(g=>g.name.StartsWith("NEXOTERRA ·"));
            if(!source)throw new Exception("No se encontro la cuenca de origen.");
            if(Mathf.Abs(source.transform.localScale.x-1)>.001f)throw new Exception("La cuenca de origen cambio de escala; revisar antes de duplicarla.");
            if(File.Exists(ScenePath))File.Copy(ScenePath,"Library/NexoterraCuatroNodos_backup_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".unity");
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);SceneManager.SetActiveScene(scene);
            world=new GameObject("NEXOTERRA · Cuatro nodos · 640 x 640 m").transform;
            new GameObject("Ajuste de boveda +4 m").transform.SetParent(world,false);
            Directory.CreateDirectory(Folder+"/Meshes");Directory.CreateDirectory(Folder+"/Materials");Directory.CreateDirectory(Folder+"/Creatures");
            AssetDatabase.Refresh();Random.InitState(91823);MakeMaterials();
            AssetDatabase.StartAssetEditing();
            try
            {
                for(int z=0;z<2;z++)for(int x=0;x<2;x++)
                {
                    int index=z*2+x;Vector3 offset=new Vector3(x==0?-160:160,0,z==0?-160:160);
                    var tile=UnityEngine.Object.Instantiate(source);SceneManager.MoveGameObjectToScene(tile,scene);tile.transform.SetParent(world);
                    tile.name="Nodo "+(index+1)+" · "+(x==0?"Oeste":"Este")+" "+(z==0?"Sur":"Norte");tile.transform.position=offset;
                    foreach(var light in tile.GetComponentsInChildren<Light>())UnityEngine.Object.DestroyImmediate(light.gameObject);
                    foreach(var volume in tile.GetComponentsInChildren<Volume>())UnityEngine.Object.DestroyImmediate(volume.gameObject);
                    foreach(var filter in tile.GetComponentsInChildren<MeshFilter>())
                    {
                        if(filter.name=="Paredes_exteriores"){UnityEngine.Object.DestroyImmediate(filter.gameObject);continue;}
                        var renderer=filter.GetComponent<MeshRenderer>();if(!renderer)continue;
                        if(filter.name.StartsWith("Lago_"))
                        {
                            int bio=Biome(renderer.bounds.center);renderer.sharedMaterial=bio==5?dirty:bio==3?lava:bio==1?frozen:cyan;
                            if(bio==1)filter.gameObject.AddComponent<MeshCollider>().sharedMesh=filter.sharedMesh;
                            continue;
                        }
                        var mesh=UnityEngine.Object.Instantiate(filter.sharedMesh);mesh.name="N"+index+"_"+filter.name;
                        Vector3[] v=mesh.vertices;Color[] c=mesh.colors;Vector3[] normals=mesh.normals;
                        if(c.Length!=v.Length)c=Enumerable.Repeat(Color.white,v.Length).ToArray();
                        bool roof=filter.name.StartsWith("Techo_")||filter.name.StartsWith("Estalactitas_");
                        bool floor=filter.name.StartsWith("Suelo_");
                        bool glow=filter.name.Contains("Cristal")||filter.name.Contains("luces");
                        for(int i=0;i<v.Length;i++)
                        {
                            Vector3 wp=filter.transform.TransformPoint(v[i]);int bio=Biome(wp);
                            if(roof)v[i].y+=RoofLift(wp);
                            if(bio==0&&!roof)continue;
                            float variation=.82f+Mathf.PerlinNoise(wp.x*.08f+100,wp.z*.08f+200)*.3f;
                            Color baseColor=Palette[bio];
                            if(roof)baseColor=Color.Lerp(baseColor,new Color(.1f,.1f,.12f),.38f);
                            if(floor&&bio==1&&normals[i].y>.9f)baseColor=new Color(.7f,.85f,.88f);
                            if(glow)baseColor=Color.white;
                            c[i]=baseColor*variation;c[i].a=1;
                        }
                        mesh.vertices=v;mesh.colors=c;if(roof)mesh.RecalculateNormals();mesh.RecalculateBounds();
                        mesh=SaveMesh(mesh);filter.sharedMesh=mesh;var collider=filter.GetComponent<MeshCollider>();if(collider)collider.sharedMesh=mesh;
                        if(glow)renderer.sharedMaterial=glows[Biome(renderer.bounds.center)%3];
                    }
                }
            }
            finally{AssetDatabase.StopAssetEditing();}
            EditorSceneManager.CloseScene(sourceScene,true);
            // Remove the former camera only in the new world, never in the saved source scene.
            BuildOuterWalls();BuildVolcano();BuildIce();Physics.SyncTransforms();
            MakeMushrooms();BuildActors();BuildSpawns();Lighting();
            AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(scene,ScenePath);
            if(SceneView.lastActiveSceneView)SceneView.lastActiveSceneView.LookAt(Visits[0]+Vector3.up*2,Quaternion.Euler(15,-35,0),12);
            Selection.activeGameObject=world.gameObject;
            Validate();Debug.Log("NEXOTERRA_BIOMES_BUILD_OK");
        }
        [MenuItem("Nexoterra/Mundo/Elevar techo 4 m y actualizar arana")]
        public static void RaiseRoofAndRefreshSpider()
        {
            if(EditorApplication.isPlaying)throw new Exception("Salir de Play antes de modificar la escena.");
            var scene=SceneManager.GetActiveScene();
            if(scene.path!=ScenePath)throw new Exception("Abrir primero CuatroNodos.");
            EditorSceneManager.SaveScene(scene);
            File.Copy(ScenePath,"Library/NexoterraAntesTecho_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".unity");
            var root=scene.GetRootGameObjects().First(g=>g.name.StartsWith("NEXOTERRA ·"));
            const string marker="Ajuste de boveda +4 m";
            int raised=0;
            if(!root.transform.Find(marker))
            {
                foreach(var f in root.GetComponentsInChildren<MeshFilter>(true))
                {
                    if(f.name.StartsWith("Techo_")||f.name.StartsWith("Estalactitas_")||f.name=="Union_bovedas")
                    {Undo.RecordObject(f.transform,"Elevar boveda");f.transform.position+=Vector3.up*4;raised++;}
                    if(f.name=="Pared_exterior_640")
                    {
                        Undo.RecordObject(f.transform,"Extender pared");
                        float bottom=f.sharedMesh.bounds.min.y;
                        float factor=(f.sharedMesh.bounds.size.y+4)/f.sharedMesh.bounds.size.y;
                        f.transform.localScale=new Vector3(1,factor,1);
                        f.transform.localPosition+=Vector3.up*(bottom*(1-factor));
                    }
                }
                new GameObject(marker).transform.SetParent(root.transform,false);
            }
            var prefab=Creature("Arana","Assets/NexoterraWorld/Biomes/Araña.prefab");int spiders=0;
            foreach(var spawn in root.GetComponentsInChildren<SlugSpawnPoint>(true))
            {
                if(spawn.species!="Arana")continue;
                spawn.creaturePrefab=prefab;
                if(spawn.editorPreview)UnityEngine.Object.DestroyImmediate(spawn.editorPreview.gameObject);
                var preview=(GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);
                preview.name="Vista previa · se sustituye al jugar";
                preview.transform.SetParent(spawn.transform,false);spawn.editorPreview=preview.transform;spiders++;
            }
            Physics.SyncTransforms();AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            Validate();
            File.WriteAllText("Library/NexoterraRoofUpdate.txt","PASS\nBoveda elevada 4 metros. Objetos movidos: "+raised+"\nSpawns de arana actualizados: "+spiders);
        }
        static Mesh SaveMesh(Mesh m)
        {
            string path=Folder+"/Meshes/"+m.name+".asset";
            var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(old){EditorUtility.CopySerialized(m,old);EditorUtility.SetDirty(old);UnityEngine.Object.DestroyImmediate(m);return old;}
            AssetDatabase.CreateAsset(m,path);return m;
        }
        static Material Surface(string name,Color color,float emission=0)
        {
            string path=Folder+"/Materials/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!m){m=new Material(Shader.Find("Nexoterra/Cavern Surface"));AssetDatabase.CreateAsset(m,path);}
            m.SetColor("_BaseColor",color);m.SetFloat("_Emission",emission);m.SetFloat("_Strata",.5f);EditorUtility.SetDirty(m);return m;
        }
        static Material Water(string name,Color a,Color b,Color foam)
        {
            string path=Folder+"/Materials/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!m){m=new Material(Shader.Find("Nexoterra/Cavern Water"));AssetDatabase.CreateAsset(m,path);}
            m.SetColor("_ShallowColor",a);m.SetColor("_DeepColor",b);m.SetColor("_FoamColor",foam);m.SetFloat("_FlowSpeed",name=="Hielo"?.015f:.4f);EditorUtility.SetDirty(m);return m;
        }
        static void MakeMaterials()
        {
            rock=Surface("Roca_biomas",Color.white);ice=Surface("Hielo_luminoso",new Color(.48f,.83f,.95f),.3f);
            stem=Surface("Tallo_hongo",new Color(.28f,.24f,.30f));
            glows=new[]{Surface("Luz_celeste",new Color(.08f,.66f,.85f),1.3f),Surface("Luz_violeta",new Color(.48f,.13f,.85f),1.6f),Surface("Luz_rosa",new Color(.85f,.08f,.28f),1.4f)};
            cyan=Water("Agua_azul",new Color(.13f,.65f,.8f),new Color(.025f,.19f,.29f),new Color(.50f,.90f,.96f));
            dirty=Water("Agua_sucia_carmesi",new Color(.3f,.025f,.04f),new Color(.055f,.006f,.016f),new Color(.46f,.055f,.08f));
            lava=Water("Lava",new Color(1,.20f,.015f),new Color(.5f,.03f,.005f),new Color(1,.6f,.03f));
            frozen=Water("Hielo",new Color(.57f,.83f,.96f),new Color(.18f,.49f,.67f),Color.white);
        }
        static GameObject MeshObject(string name,Mesh mesh,Material mat,Transform parent,bool collider=false)
        {
            mesh.name=name;mesh=SaveMesh(mesh);
            var saved=AssetDatabase.LoadAssetAtPath<Mesh>(Folder+"/Meshes/"+name+".asset");
            var go=new GameObject(name);go.transform.SetParent(parent);go.AddComponent<MeshFilter>().sharedMesh=saved?saved:mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial=mat;if(collider)go.AddComponent<MeshCollider>().sharedMesh=go.GetComponent<MeshFilter>().sharedMesh;
            return go;
        }
        static Transform Group(string name){var g=new GameObject(name);g.transform.SetParent(world);return g.transform;}
        static void BuildOuterWalls()
        {
            var group=Group("Perimetro exterior · nodos comunicados");var b=new Geo();
            for(int side=0;side<4;side++)for(int i=0;i<80;i++)
            {
                float a=-320+i*8,bb=a+8;Vector3 p,q;
                if(side==0){p=new Vector3(a,-3,-320);q=new Vector3(bb,-3,-320);}else if(side==1){p=new Vector3(320,-3,a);q=new Vector3(320,-3,bb);}
                else if(side==2){p=new Vector3(-a,-3,320);q=new Vector3(-bb,-3,320);}else{p=new Vector3(-320,-3,-a);q=new Vector3(-320,-3,-bb);}
                Color color=Palette[Biome(p)]*.65f;color.a=1;b.Quad(p,q,q+Vector3.up*26,p+Vector3.up*26,color);
                b.Rock((p+q)*.5f,new Vector3(6,29,6),color,7);
            }
            MeshObject("Pared_exterior_640",b.Mesh(),rock,group,true);
            // Narrow overlaps seal the four roof joins without obstructing ground travel.
            var seam=new Geo();
            for(int i=0;i<160;i++)
            {float a=-320+i*4;Color c=Palette[Biome(new Vector3(a,0,0))]*.5f;c.a=1;seam.Quad(new Vector3(a,18.5f,-1),new Vector3(a+4,18.5f,-1),new Vector3(a+4,18.5f,1),new Vector3(a,18.5f,1),c);seam.Quad(new Vector3(-1,18.5f,a),new Vector3(1,18.5f,a),new Vector3(1,18.5f,a+4),new Vector3(-1,18.5f,a+4),c);}
            var roof=MeshObject("Union_bovedas",seam.Mesh(),rock,group,true);roof.GetComponent<MeshRenderer>().shadowCastingMode=ShadowCastingMode.Off;
        }
        static void BuildVolcano()
        {
            var parent=Group("VOLCAN · caldera reemplazable");var b=new Geo();const int sides=80,rings=20;
            for(int ring=0;ring<=rings;ring++)for(int i=0;i<=sides;i++)
            {
                float a=i*Mathf.PI*2/sides,r=ring/(float)rings*48;
                float height=r<9?11+Mathf.Pow(r/9,2)*3.5f:14.5f*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(9,48,r)));
                height+=Mathf.Sin(a*7)*.35f*Mathf.Sin(r/48*Mathf.PI);
                var p=VolcanoCenter+new Vector3(Mathf.Cos(a)*r,height,Mathf.Sin(a)*r);
                Color c=Color.Lerp(new Color(.10f,.025f,.02f),new Color(.43f,.09f,.025f),Mathf.PerlinNoise(p.x*.12f,p.z*.12f));b.Add(p,c,Vector2.zero);
            }
            for(int r=0;r<rings;r++)for(int i=0;i<sides;i++){int a=r*(sides+1)+i;b.Tri(a,a+1,a+sides+1);b.Tri(a+1,a+sides+2,a+sides+1);}
            MeshObject("Volcan_caldera",b.Mesh(),rock,parent,true);
            var pool=new Geo();pool.Add(VolcanoCenter+Vector3.up*11.8f,Color.white,Vector2.zero);
            for(int i=0;i<=sides;i++){float a=i*Mathf.PI*2/sides;pool.Add(VolcanoCenter+new Vector3(Mathf.Cos(a)*4,11.8f,Mathf.Sin(a)*4),Color.white,new Vector2(1,i/(float)sides));if(i>0)pool.Tri(0,i+1,i);}
            MeshObject("Lava_en_crater",pool.Mesh(),lava,parent);
            // A separate emissive stream makes the volcanic landmark visible from the approach.
            var stream=new Geo();
            for(int i=0;i<20;i++)
            {
                float r=10+i*1.7f,r2=r+1.7f;float h=14.5f*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(9,48,r)))+.13f,h2=14.5f*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(9,48,r2)))+.13f;
                float x=Mathf.Sin(r*.2f)*1.3f,x2=Mathf.Sin(r2*.2f)*1.3f;
                stream.Quad(VolcanoCenter+new Vector3(x-1,h,-r),VolcanoCenter+new Vector3(x+1,h,-r),VolcanoCenter+new Vector3(x2+1,h2,-r2),VolcanoCenter+new Vector3(x2-1,h2,-r2),Color.white);
            }
            MeshObject("Colada_de_lava",stream.Mesh(),lava,parent);
        }
        static void BuildIce()
        {
            var parent=Group("HIELO · agujas inclinadas");var b=new Geo();
            for(int i=0;i<28;i++)
            {
                Vector3 p=new Vector3(Random.Range(-285,-50),0,Random.Range(-65,65));p.y=Ground(p);
                if(p.y<-.1f)continue;
                var small=new Geo();small.Spire(Vector3.zero,Random.Range(1,2.8f),Random.Range(3,6),Color.white,false);
                var mesh=small.Mesh();var v=mesh.vertices;
                for(int j=0;j<v.Length;j++)v[j]=Quaternion.Euler(0,0,Random.Range(22,24))*v[j]+p;
                mesh.vertices=v;mesh.RecalculateNormals();mesh.RecalculateBounds();MeshObject("Aguja_hielo_"+i,mesh,ice,parent,true);
            }
        }
        static float Ground(Vector3 p)
        {
            // Probe from below the cavern roof and keep the highest upward ground surface.
            float y=-2;
            foreach(var hit in Physics.RaycastAll(new Vector3(p.x,11,p.z),Vector3.down,16,1,QueryTriggerInteraction.Ignore))
                if(hit.normal.y>.45f&&hit.point.y>y)y=hit.point.y;
            return y;
        }
        static Mesh MushroomCap()
        {
            var b=new Geo();const int sides=24;
            float[] radii={0,.6f,1,.98f,.65f,0};float[] heights={1.15f,1.03f,.75f,.62f,.57f,.57f};
            for(int r=0;r<radii.Length;r++)for(int s=0;s<=sides;s++){float a=s*Mathf.PI*2/sides;b.Add(new Vector3(Mathf.Cos(a)*radii[r],heights[r],Mathf.Sin(a)*radii[r]),r>=3?Color.white:new Color(.48f,.46f,.56f),new Vector2(s/(float)sides,r/5f));}
            for(int r=0;r<radii.Length-1;r++)for(int s=0;s<sides;s++){int a=r*(sides+1)+s;b.Tri(a,a+1,a+sides+1);b.Tri(a+1,a+sides+2,a+sides+1);}
            return b.Mesh();
        }
        static void MakeMushrooms()
        {
            var group=Group("Hongos · comida y bioluminiscencia");var cap=MushroomCap();cap.name="Sombrero_hongo";cap=SaveMesh(cap);
            var stalk=new Geo();stalk.Rock(Vector3.zero,new Vector3(.15f,.7f,.15f),Color.white,10);var stalkMesh=stalk.Mesh();stalkMesh.name="Tallo_hongo";stalkMesh=SaveMesh(stalkMesh);
            for(int zone=0;zone<6;zone++)for(int i=0;i<16;i++)
            {
                Vector3 p=Visits[zone]+new Vector3(Random.Range(-22,22),0,Random.Range(-22,22));
                if(zone==0&&i<4)p=Visits[0]+new Vector3(-2-i*.8f,0,3+i*.5f);
                p.y=Ground(p);if(p.y<-.1f)continue;
                bool large=i%4==0;float size=large?Random.Range(2,3.1f):Random.Range(.35f,.65f);
                var g=new GameObject(large?"Hongo farol":"Hongo comestible");g.transform.SetParent(group);g.transform.position=p;g.transform.localScale=Vector3.one*size;
                var top=new GameObject("Sombrero luminoso");top.transform.SetParent(g.transform,false);top.AddComponent<MeshFilter>().sharedMesh=cap;top.AddComponent<MeshRenderer>().sharedMaterial=glows[(zone+i)%3];
                var trunk=new GameObject("Tallo");trunk.transform.SetParent(g.transform,false);trunk.AddComponent<MeshFilter>().sharedMesh=stalkMesh;trunk.AddComponent<MeshRenderer>().sharedMaterial=stem;
                if(!large)g.AddComponent<MushroomFood>();
                else
                {
                    var bulb=new GameObject("Luz bajo el sombrero");bulb.transform.SetParent(g.transform,false);bulb.transform.localPosition=Vector3.up*.5f;
                    var light=bulb.AddComponent<Light>();light.type=LightType.Point;light.color=glows[(zone+i)%3].GetColor("_BaseColor");light.intensity=2.3f;light.range=size*4;light.shadows=LightShadows.None;
                    g.AddComponent<MushroomGlow>().glow=light;
                }
            }
        }
        static GameObject Model(string path,Transform parent,float height)
        {
            var asset=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(!asset)throw new Exception("Missing model "+path);
            var go=(GameObject)PrefabUtility.InstantiatePrefab(asset);go.transform.SetParent(parent,false);go.transform.localPosition=Vector3.zero;go.transform.localRotation=Quaternion.identity;go.transform.localScale=Vector3.one;
            var renderers=go.GetComponentsInChildren<Renderer>();Bounds bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
            float ratio=height/Mathf.Max(.01f,bounds.size.y);go.transform.localScale=Vector3.one*ratio;
            go.transform.localPosition=new Vector3(-bounds.center.x*ratio,-bounds.min.y*ratio,-bounds.center.z*ratio);
            foreach(var a in go.GetComponentsInChildren<Animator>())a.applyRootMotion=false;
            return go;
        }
        static void SetLayer(GameObject go,int layer){foreach(var t in go.GetComponentsInChildren<Transform>(true))t.gameObject.layer=layer;}
        static void BuildActors()
        {
            var group=Group("Personajes y puntos de viaje");
            var player=new GameObject("Jugador · Militar y Comando");player.transform.SetParent(group);Vector3 p=Visits[0];p.y=Ground(p)+.1f;player.transform.position=p;
            var cc=player.AddComponent<CharacterController>();cc.height=2;cc.radius=.32f;cc.center=Vector3.up;cc.slopeLimit=45;cc.stepOffset=.3f;cc.minMoveDistance=0;
            var visual=new GameObject("Modelos estaticos · reemplazar por rig animado").transform;visual.SetParent(player.transform,false);visual.rotation=Quaternion.Euler(0,-35,0);
            // Normalize before moving into the scene: importer models are one metre tall.
            var male=Model("Assets/NexoterraWorld/Characters/Player_Male.prefab",null,1.9f);male.transform.SetParent(visual,false);
            var female=Model("Assets/NexoterraWorld/Characters/Player_Female.prefab",null,1.85f);female.transform.SetParent(visual,false);female.SetActive(false);
            var mount=new GameObject("Soporte del lanzador · ajustar con el rig").transform;mount.SetParent(visual,false);mount.localPosition=new Vector3(.48f,1.1f,.28f);
            var weapon=Model("Assets/03_Models/Blast/Blast.fbx",null,.34f);weapon.transform.SetParent(mount,false);
            var muzzle=new GameObject("Salida del disparo").transform;muzzle.SetParent(mount,false);muzzle.localPosition=new Vector3(0,.20f,.44f);
            SetLayer(player,2);
            var cameraObject=new GameObject("Main Camera");cameraObject.tag="MainCamera";cameraObject.transform.SetParent(group);var cam=cameraObject.AddComponent<Camera>();cam.fieldOfView=68;cam.nearClipPlane=.08f;cam.farClipPlane=550;
            cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.055f,.09f,.13f);cam.GetUniversalAdditionalCameraData().renderPostProcessing=true;cameraObject.AddComponent<AudioListener>();
            cam.transform.position=p+new Vector3(3,2.1f,-4);cam.transform.LookAt(p+new Vector3(-3,1.4f,5));
            var controller=player.AddComponent<NexoterraThirdPerson>();controller.view=cam;controller.visuals=visual;controller.male=male;controller.female=female;controller.muzzle=muzzle;controller.spawn=p;
            var line=new GameObject("Trazo de disparo de prueba").AddComponent<LineRenderer>();line.transform.SetParent(group);line.positionCount=2;line.startWidth=.025f;line.endWidth=.012f;line.sharedMaterial=glows[0];line.enabled=false;controller.tracer=line;
            controller.travelPoints=new Transform[6];
            for(int i=0;i<6;i++){var point=new GameObject(BiomeNames[i]).transform;point.SetParent(group);var pos=Visits[i];pos.y=Ground(pos)+.15f;point.position=pos;controller.travelPoints[i]=point;}
            var companion=Model("Assets/NexoterraWorld/Characters/Player_Female.prefab",null,1.85f);companion.name="Comando · companera en el refugio";companion.transform.SetParent(group);var npc=p+new Vector3(-4,0,0);npc.y=Ground(npc);companion.transform.position+=npc;companion.transform.rotation=Quaternion.Euler(0,140,0);
        }
        static GameObject Creature(string key,string path)
        {
            var holder=new GameObject(key);var model=Model(path,null,.65f);model.transform.SetParent(holder.transform,false);
            var slug=holder.AddComponent<WildSlug>();slug.species=key;
            // Use actual imported animation clips if available, without modifying the original FBX.
            string modelPath=path.EndsWith(".prefab")?AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromOriginalSource(AssetDatabase.LoadAssetAtPath<GameObject>(path))):path;
            var clips=AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__preview")).ToArray();
            if(clips.Length>0)
            {
                string ap=Folder+"/Creatures/"+key+".controller";var anim=AssetDatabase.LoadAssetAtPath<AnimatorController>(ap);
                if(!anim){anim=AnimatorController.CreateAnimatorControllerAtPath(ap);var idle=anim.layers[0].stateMachine.AddState("Idle");idle.motion=clips.FirstOrDefault(c=>c.name.ToLower().Contains("idle"))??clips[0];var walk=anim.layers[0].stateMachine.AddState("Walk");walk.motion=clips.FirstOrDefault(c=>c.name.ToLower().Contains("walk"))??clips[Mathf.Min(1,clips.Length-1)];}
                var animator=model.GetComponent<Animator>()??model.AddComponent<Animator>();animator.runtimeAnimatorController=anim;animator.applyRootMotion=false;
            }
            string outPath=Folder+"/Creatures/"+key+".prefab";var result=PrefabUtility.SaveAsPrefabAsset(holder,outPath);UnityEngine.Object.DestroyImmediate(holder);return result;
        }
        static void BuildSpawns()
        {
            var group=Group("Spawns de babosas por bioma");
            string[][] keys={new[]{"Gon_Doc","Armashelt","tazerling"},new[]{"ice","Elemental_Agua"},new[]{"Carnero","Arana","Elemental_Tierra"},new[]{"Infierno","Elemental_Fuego"},new[]{"Elemental_Agua","Elemental_Aire"},new[]{"Arana","Infierno","tazerling"}};
            var paths=new Dictionary<string,string>{{"Gon_Doc","Assets/02_Prefabs/Gon_Doc.fbx"},{"Armashelt","Assets/02_Prefabs/Armashelt.fbx"},{"tazerling","Assets/02_Prefabs/tazerling.fbx"},{"ice","Assets/02_Prefabs/ice.fbx"},{"Carnero","Assets/02_Prefabs/CARNERO.prefab"},{"Arana","Assets/NexoterraWorld/Biomes/Araña.prefab"},{"Infierno","Assets/02_Prefabs/Infierno.fbx"},{"Elemental_Agua","Assets/02_Prefabs/ELEMENTAL_AGUA.prefab"},{"Elemental_Aire","Assets/02_Prefabs/ELEMENTAL_AIRE.prefab"},{"Elemental_Tierra","Assets/02_Prefabs/ELEMENTAL_TIERRA.prefab"},{"Elemental_Fuego","Assets/02_Prefabs/ELEMENTAL_FUEGO.prefab"}};
            var prefabs=new Dictionary<string,GameObject>();foreach(var kv in paths)prefabs[kv.Key]=Creature(kv.Key,kv.Value);
            for(int zone=0;zone<6;zone++)for(int i=0;i<6;i++)
            {
                Vector3 p=Visits[zone]+new Vector3((i%3-1)*10,0,8+(i/3)*12);p.y=Ground(p);
                if(p.y<-.1f){p=Visits[zone]+new Vector3(i*2,0,-6);p.y=Ground(p);}
                var go=new GameObject("Spawn · "+BiomeNames[zone]+" · "+i);go.transform.SetParent(group);go.transform.position=p;
                var spawn=go.AddComponent<SlugSpawnPoint>();spawn.species=keys[zone][i%keys[zone].Length];spawn.biome=BiomeNames[zone];spawn.creaturePrefab=prefabs[spawn.species];spawn.population=spawn.species.StartsWith("Elemental")?1:2;
                var preview=(GameObject)PrefabUtility.InstantiatePrefab(spawn.creaturePrefab);preview.name="Vista previa · se sustituye al jugar";preview.transform.SetParent(go.transform,false);spawn.editorPreview=preview.transform;
            }
        }
        static void Lighting()
        {
            RenderSettings.skybox=null;RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.26f,.32f,.38f);RenderSettings.ambientEquatorColor=new Color(.18f,.24f,.28f);RenderSettings.ambientGroundColor=new Color(.12f,.15f,.18f);
            RenderSettings.fog=true;RenderSettings.fogMode=FogMode.ExponentialSquared;RenderSettings.fogDensity=.005f;RenderSettings.fogColor=new Color(.065f,.11f,.15f);
            var sun=new GameObject("Luz mineral");sun.transform.SetParent(world);sun.transform.rotation=Quaternion.Euler(50,-35,0);var l=sun.AddComponent<Light>();l.type=LightType.Directional;l.intensity=1;l.color=new Color(.9f,.92f,1);l.shadows=LightShadows.Soft;RenderSettings.sun=l;
            var v=new GameObject("Atmosfera").AddComponent<Volume>();v.transform.SetParent(world);v.isGlobal=true;v.sharedProfile=AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/NexoterraWorld/CavernAtmosphere.asset");
        }
        [MenuItem("Nexoterra/Mundo/Validar cuatro nodos")]
        public static void Validate()
        {
            Physics.SyncTransforms();var failures=new List<string>();var player=UnityEngine.Object.FindFirstObjectByType<NexoterraThirdPerson>();
            if(!player||!player.male||!player.female||!player.muzzle)failures.Add("Personajes o arma faltantes");
            int nodes=SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>()).Count(t=>t.name.StartsWith("Nodo "));
            if(nodes!=4)failures.Add("Se esperaban cuatro nodos");
            var spawns=UnityEngine.Object.FindObjectsByType<SlugSpawnPoint>();if(spawns.Length!=36)failures.Add("Cantidad incorrecta de spawns: "+spawns.Length);
            foreach(var s in spawns)if(!s.creaturePrefab)failures.Add("Spawn sin prefab: "+s.name);
            foreach(var r in UnityEngine.Object.FindObjectsByType<Renderer>())foreach(var m in r.sharedMaterials)if(!m||!m.shader||ShaderUtil.ShaderHasError(m.shader))failures.Add("Material incorrecto: "+r.name);
            var report=new StringBuilder("NEXOTERRA CUATRO NODOS\n");report.AppendLine(failures.Count==0?"PASS":"FAIL");report.AppendLine("4 nodos de 320 x 320 m; mundo de 640 x 640 m; 6 biomas");report.AppendLine("Spawns: "+spawns.Length);report.AppendLine("Hongos comestibles: "+UnityEngine.Object.FindObjectsByType<MushroomFood>().Length);report.AppendLine("Hongos con luz: "+UnityEngine.Object.FindObjectsByType<MushroomGlow>().Length);
            foreach(var f in failures)report.AppendLine(f);File.WriteAllText(Result,report.ToString());Debug.Log(report);
            if(failures.Count>0)throw new Exception(report.ToString());
        }
        [MenuItem("Nexoterra/Mundo/Capturar biomas")]
        public static void Capture()
        {
            Directory.CreateDirectory("Documentation/Nexoterra/Biomes");var camera=Camera.main;var original=camera.transform.position;var rotation=camera.transform.rotation;
            try
            {
                for(int i=0;i<6;i++)
                {
                    var p=Visits[i];p.y=Ground(p)+2.2f;camera.transform.position=p+new Vector3(4,0,-7);
                    var target=p+new Vector3(-8,-.5f,16);if(i==3)target=VolcanoCenter+Vector3.up*7;
                    camera.transform.LookAt(target);
                    var rt=new RenderTexture(1600,900,24);rt.Create();var previous=RenderTexture.active;
                    try{RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});RenderTexture.active=rt;var texture=new Texture2D(1600,900,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,1600,900),0,0);texture.Apply();File.WriteAllBytes("Documentation/Nexoterra/Biomes/"+i+"_"+BiomeNames[i]+".png",texture.EncodeToPNG());UnityEngine.Object.DestroyImmediate(texture);}
                    finally{RenderTexture.active=previous;rt.Release();UnityEngine.Object.DestroyImmediate(rt);}
                }
                File.WriteAllText(Result,"CAPTURE_OK");
            }
            finally{camera.transform.position=original;camera.transform.rotation=rotation;}
        }
    }
}
