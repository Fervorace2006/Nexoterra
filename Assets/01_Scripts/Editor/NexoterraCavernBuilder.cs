using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Nexoterra.World;
using Random = UnityEngine.Random;

namespace Nexoterra.Editor
{
    /// <summary>Deterministic, editor-only world authoring. All geometry is saved as normal assets.</summary>
    public static class NexoterraCavernBuilder
    {
        public const string ScenePath="Assets/00_Scenes/Nexoterra_CuencaDelNexo.unity";
        const string Root="Assets/NexoterraWorld";
        const string Request="Library/NexoterraWorld.request";
        const string Result="Library/NexoterraWorld.result.txt";
        const float Extent=160;
        const float DefaultHorizontalScale=2f;
        static Material stone, terrain, foliage, crystal, amber, cyanWater, redWater;
        static Transform world;
        static double nextPoll;
        static readonly Vector4[] Hills={new Vector4(22,2,28,5.6f),new Vector4(-46,76,27,5.2f),new Vector4(74,-68,29,5.8f),
            new Vector4(-110,-83,25,4.8f),new Vector4(60,111,25,5.1f),new Vector4(4,78,22,4.3f)};
        static readonly Vector4[] Lakes={new Vector4(-55,-22,30,45),new Vector4(-95,64,23,28),
            new Vector4(83,34,28,35),new Vector4(105,94,18,23)};

        [InitializeOnLoadMethod]
        static void Install(){EditorApplication.update-=Poll;EditorApplication.update+=Poll;}
        static void Poll()
        {
            if(EditorApplication.timeSinceStartup<nextPoll || EditorApplication.isCompiling || EditorApplication.isUpdating)return;
            nextPoll=EditorApplication.timeSinceStartup+1;
            if(!File.Exists(Request))return;
            string command=File.ReadAllText(Request).Trim();File.Delete(Request);
            try
            {
                if(command=="build")Build();
                else if(command=="capture")Capture();
                else if(command=="validate")Validate();
                else if(command=="double")DoubleSize();
                else if(command=="play")EditorApplication.isPlaying=true;
                else if(command=="stop")EditorApplication.isPlaying=false;
                else throw new ArgumentException("Unknown Nexoterra command");
            }
            catch(Exception e){File.WriteAllText(Result,"FAILED\n"+e);Debug.LogException(e);}
        }

        public static void BuildAndCapture(){Build();Capture();}

        [MenuItem("Nexoterra/Mapa/Generar cuenca y recursos")]
        public static void Build()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play mode before authoring.");
            if(File.Exists(ScenePath) && !Application.isBatchMode)
            {
                // Existing work is never silently overwritten, including edits to a generated scene.
                string stamp=DateTime.Now.ToString("yyyyMMdd_HHmmss");
                File.Copy(ScenePath,"Library/Nexoterra_scene_backup_"+stamp+".unity");
            }
            var old=SceneManager.GetActiveScene();
            if(old.isDirty)throw new InvalidOperationException("Save the current scene before generating the map. No scene was changed.");
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            Random.State state=Random.state;Random.InitState(73129);
            try
            {
                Directory.CreateDirectory(Root+"/Meshes");Directory.CreateDirectory(Root+"/Materials");
                AssetDatabase.Refresh();
                MakeMaterials();
                world=new GameObject("NEXOTERRA · Cuenca del Nexo · 320 x 320 m").transform;
                var ground=Group("01 · Suelo y mesetas transitables");
                var roof=Group("02 · Boveda y estalactitas");
                var waters=Group("03 · Aguas celestes y carmesi");
                var props=Group("04 · Rocas cristales y vegetacion");
                var landmarks=Group("05 · Hitos y caminos");
                for(int z=0;z<8;z++)for(int x=0;x<8;x++)
                {
                    var mesh=Grid(x*40-160,z*40-160,40,32,false);
                    PlaceMesh("Suelo_"+x+"_"+z,mesh,terrain,ground,true);
                    PlaceMesh("Techo_"+x+"_"+z,Grid(x*40-160,z*40-160,40,8,true),stone,roof,true);
                }
                BuildBoundary(roof);
                for(int i=0;i<Lakes.Length;i++)BuildLake(i,waters);
                BuildHangingRocks(roof);
                BuildDecoration(props);
                BuildLandmarks(landmarks);
                Lighting();
                Explorer();
                ApplyHorizontalScale(DefaultHorizontalScale);
                AssetDatabase.SaveAssets();
                EditorSceneManager.SaveScene(scene,ScenePath);
                if(SceneView.lastActiveSceneView!=null)
                    SceneView.lastActiveSceneView.LookAt(new Vector3(-14,2,-20),Quaternion.Euler(8,325,0),32);
                Selection.activeGameObject=world.gameObject;
                Validate();
                File.AppendAllText(Result,"\nScene saved: "+ScenePath);
                Debug.Log("NEXOTERRA_BUILD_OK "+ScenePath);
            }
            finally {Random.state=state;EditorUtility.ClearProgressBar();}
        }

        static Transform Group(string name){var t=new GameObject(name).transform;t.SetParent(world);return t;}
        static Transform FindWorld()
        {
            foreach(var root in SceneManager.GetActiveScene().GetRootGameObjects())
                if(root.name.StartsWith("NEXOTERRA ·"))return root.transform;
            throw new InvalidOperationException("Open the Nexoterra cavern scene first.");
        }
        static void ApplyHorizontalScale(float scale)
        {
            var root=FindWorld();float ratio=scale/root.localScale.x;
            root.localScale=new Vector3(scale,root.localScale.y,scale);
            root.name="NEXOTERRA · Cuenca del Nexo · "+(320*scale).ToString("0")+" x "+(320*scale).ToString("0")+" m";
            RenderSettings.fogDensity/=ratio;
            if(Camera.main)Camera.main.farClipPlane*=ratio;
            foreach(var explorer in UnityEngine.Object.FindObjectsByType<CavernExplorer>())
            {
                var cc=explorer.GetComponent<CharacterController>();bool wasEnabled=cc.enabled;cc.enabled=false;
                Vector3 p=explorer.transform.position;p.x*=ratio;p.z*=ratio;explorer.transform.position=p;
                explorer.spawn=p;cc.enabled=wasEnabled;
            }
            Physics.SyncTransforms();
        }
        [MenuItem("Nexoterra/Mapa/Duplicar extension horizontal")]
        public static void DoubleSize()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play mode before resizing the map.");
            ApplyHorizontalScale(FindWorld().localScale.x*2);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            Validate();
            Debug.Log("NEXOTERRA_RESIZE_OK");
        }
        static Material Mat(string name,string shader,Color tint,float strata=0,float emission=0,float wind=0)
        {
            var sh=Shader.Find(shader);if(sh==null)throw new Exception("Shader missing: "+shader);
            string path=Root+"/Materials/"+name+".mat";
            var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!m){m=new Material(sh);AssetDatabase.CreateAsset(m,path);}else m.shader=sh;
            m.SetColor("_BaseColor",tint);
            if(m.HasProperty("_Strata")){m.SetFloat("_Strata",strata);m.SetFloat("_Emission",emission);m.SetFloat("_Wind",wind);}
            m.enableInstancing=true;EditorUtility.SetDirty(m);return m;
        }
        static void MakeMaterials()
        {
            terrain=Mat("Suelo_musgo_y_estratos","Nexoterra/Cavern Surface",Color.white,.6f);
            stone=Mat("Roca_ocre_estratificada","Nexoterra/Cavern Surface",Color.white,.9f);
            foliage=Mat("Flora_con_brisa","Nexoterra/Cavern Surface",Color.white,0,.08f,.65f);
            crystal=Mat("Cristal_turquesa","Nexoterra/Cavern Surface",new Color(.14f,.8f,.76f),.15f,1.25f);
            amber=Mat("Cristal_ambar","Nexoterra/Cavern Surface",new Color(.95f,.32f,.12f),.1f,.8f);
            cyanWater=WaterMat("Agua_celeste",new Color(.12f,.72f,.81f),new Color(.025f,.27f,.38f),new Color(.55f,.92f,.88f),.8f);
            redWater=WaterMat("Agua_carmesi_oscura",new Color(.34f,.035f,.06f),new Color(.075f,.009f,.025f),new Color(.64f,.13f,.14f),.3f);
        }
        static Material WaterMat(string name,Color shallow,Color deep,Color foam,float speed)
        {
            var m=Mat(name,"Nexoterra/Cavern Water",Color.white);
            m.SetColor("_ShallowColor",shallow);m.SetColor("_DeepColor",deep);m.SetColor("_FoamColor",foam);m.SetFloat("_FlowSpeed",speed);return m;
        }
        static float Smooth(float a,float b,float x){return Mathf.SmoothStep(0,1,Mathf.InverseLerp(a,b,x));}
        static float Noise(float x,float z){return Mathf.PerlinNoise(x+321.32f,z+91.7f);}
        static float LakeRadius(float a,int i){return 1+Mathf.Sin(a*3+i)*.075f+Mathf.Cos(a*5-i)*.045f+Mathf.Sin(a*9)*.018f;}
        static float LakeDistance(float x,float z,int i)
        {
            var l=Lakes[i];float dx=(x-l.x)/l.z,dz=(z-l.y)/l.w;
            return Mathf.Sqrt(dx*dx+dz*dz)/LakeRadius(Mathf.Atan2(dz,dx),i);
        }
        public static float Height(float x,float z)
        {
            float h=.06f+(Noise(x*.04f,z*.04f)-.5f)*.18f;
            foreach(var hill in Hills)
            {
                float r=Vector2.Distance(new Vector2(x,z),new Vector2(hill.x,hill.y))/hill.z;
                float height=hill.w*(1-Smooth(.43f,1,r));
                if(r<1)h=Mathf.Max(h,height);
            }
            for(int i=0;i<Lakes.Length;i++)
            {
                float r=LakeDistance(x,z,i);
                if(r<1.08f) h=Mathf.Lerp(-2.2f,h,Smooth(.60f,1.08f,r));
            }
            return h;
        }
        static float Ceiling(float x,float z)
        {return Mathf.Max(12.2f+Noise(x*.045f,z*.045f)*2.1f,Height(x,z)+8.2f);}
        static Color GroundColor(float x,float z)
        {
            float h=Height(x,z);
            float slope=new Vector2(Height(x+.5f,z)-Height(x-.5f,z),Height(x,z+.5f)-Height(x,z-.5f)).magnitude;
            float n=Noise(x*.10f,z*.10f);
            Color grass=Color.Lerp(new Color(.18f,.31f,.17f),new Color(.43f,.55f,.24f),n);
            if(h>2 && slope<.06f)grass=Color.Lerp(new Color(.24f,.43f,.18f),new Color(.48f,.62f,.28f),n);
            Color rock=Color.Lerp(new Color(.30f,.25f,.22f),new Color(.51f,.39f,.27f),n);
            Color c=Color.Lerp(grass,rock,Smooth(.16f,.40f,slope));
            // Broad winding travel corridors through the otherwise flat basin.
            float path=Mathf.Min(Mathf.Abs(x-(Mathf.Sin(z*.024f)*13)),Mathf.Abs(z+49-Mathf.Sin(x*.025f)*9));
            c=Color.Lerp(c,new Color(.52f,.45f,.32f),1-Smooth(2.4f,5.4f,path));
            for(int i=0;i<Lakes.Length;i++)
            {
                float r=LakeDistance(x,z,i);
                if(r<1.13f)c=Color.Lerp(c,i<2?new Color(.48f,.49f,.35f):new Color(.28f,.16f,.18f),1-Smooth(1.03f,1.13f,r));
            }
            return c;
        }
        static Mesh Grid(float startX,float startZ,float size,int steps,bool ceiling)
        {
            var b=new Geometry();float step=size/steps;
            for(int z=0;z<=steps;z++)for(int x=0;x<=steps;x++)
            {
                float wx=startX+x*step,wz=startZ+z*step;
                b.Add(new Vector3(wx,ceiling?Ceiling(wx,wz):Height(wx,wz),wz),
                    ceiling?Color.Lerp(new Color(.23f,.19f,.17f),new Color(.43f,.34f,.25f),Noise(wx*.14f,wz*.14f)):GroundColor(wx,wz),Vector2.zero);
            }
            for(int z=0;z<steps;z++)for(int x=0;x<steps;x++)
            {
                int a=z*(steps+1)+x,c=a+steps+1;
                if(ceiling){b.Tri(a,a+1,c);b.Tri(a+1,c+1,c);}
                else{b.Tri(a,c,a+1);b.Tri(a+1,c,c+1);}
            }
            return b.Mesh();
        }
        static GameObject PlaceMesh(string name,Mesh mesh,Material mat,Transform parent,bool collision)
        {
            string path=Root+"/Meshes/"+name+".asset";
            var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(saved){EditorUtility.CopySerialized(mesh,saved);UnityEngine.Object.DestroyImmediate(mesh);mesh=saved;}
            else{mesh.name=name;AssetDatabase.CreateAsset(mesh,path);}
            var go=new GameObject(name);go.transform.SetParent(parent);
            go.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=mat;
            renderer.shadowCastingMode=ShadowCastingMode.On;
            if(collision)go.AddComponent<MeshCollider>().sharedMesh=mesh;
            go.isStatic=mat!=foliage;
            return go;
        }
        static void BuildLake(int index,Transform parent)
        {
            var b=new Geometry();var l=Lakes[index];const int rings=18,sides=128;
            for(int r=0;r<=rings;r++)for(int a=0;a<=sides;a++)
            {
                float angle=a*Mathf.PI*2/sides;
                // Intersect the actual terrain contour so the animated shoreline touches land.
                float lo=.8f,hi=1.09f;
                for(int k=0;k<16;k++)
                {
                    float mid=(lo+hi)*.5f;
                    float sx=l.x+Mathf.Cos(angle)*l.z*LakeRadius(angle,index)*mid;
                    float sz=l.y+Mathf.Sin(angle)*l.w*LakeRadius(angle,index)*mid;
                    if(Height(sx,sz)<-.24f)lo=mid;else hi=mid;
                }
                float radius=r/(float)rings*(lo+hi)*.5f*LakeRadius(angle,index);
                b.Add(new Vector3(l.x+Mathf.Cos(angle)*l.z*radius,-.23f,l.y+Mathf.Sin(angle)*l.w*radius),Color.white,new Vector2(r/(float)rings,a/(float)sides));
            }
            for(int r=0;r<rings;r++)for(int a=0;a<sides;a++)
            {int v=r*(sides+1)+a;b.Tri(v,v+1,v+sides+1);b.Tri(v+1,v+sides+2,v+sides+1);}
            var go=PlaceMesh(index<2?"Lago_celeste_"+index:"Lago_carmesi_"+index,b.Mesh(),index<2?cyanWater:redWater,parent,false);
            go.GetComponent<MeshRenderer>().shadowCastingMode=ShadowCastingMode.Off;
            // Trigger volumes are integration markers; swimming/combat are separate features.
            var volume=new GameObject("Volumen de agua · referencia futura");volume.transform.SetParent(go.transform);
            volume.transform.position=new Vector3(l.x,-1,l.y);
            var trigger=volume.AddComponent<BoxCollider>();trigger.isTrigger=true;trigger.size=new Vector3(l.z*1.4f,2,l.w*1.4f);
        }
        static void BuildBoundary(Transform parent)
        {
            var b=new Geometry();
            for(int side=0;side<4;side++)for(int i=0;i<80;i++)
            {
                Vector3 a,bp;
                float v=-160+i*4,w=v+4;
                if(side==0){a=new Vector3(v,0,-160);bp=new Vector3(w,0,-160);}
                else if(side==1){a=new Vector3(160,0,v);bp=new Vector3(160,0,w);}
                else if(side==2){a=new Vector3(-v,0,160);bp=new Vector3(-w,0,160);}
                else{a=new Vector3(-160,0,-v);bp=new Vector3(-160,0,-w);}
                a.y=-3;bp.y=-3;
                var topA=new Vector3(a.x,Ceiling(a.x,a.z)+1,a.z);var topB=new Vector3(bp.x,Ceiling(bp.x,bp.z)+1,bp.z);
                Color c=Color.Lerp(new Color(.22f,.21f,.19f),new Color(.39f,.32f,.25f),Random.value);
                b.Quad(a,bp,topB,topA,c);
                Vector3 p=(a+bp)*.5f;p.y=0;
                b.Rock(p,new Vector3(Random.Range(3.5f,6.5f),Ceiling(p.x,p.z)*Random.Range(1.45f,1.85f),Random.Range(3.5f,6.5f)),c,7);
            }
            PlaceMesh("Paredes_exteriores",b.Mesh(),stone,parent,true);
        }
        static void BuildHangingRocks(Transform parent)
        {
            for(int quadrant=0;quadrant<16;quadrant++)
            {
                var b=new Geometry();int qx=quadrant%4,qz=quadrant/4;
                for(int i=0;i<25;i++)
                {
                    float x=-155+qx*80+Random.value*70,z=-155+qz*80+Random.value*70;
                    float length=Random.Range(1.3f,2.8f),radius=Random.Range(.65f,1.35f);
                    Color c=Color.Lerp(new Color(.29f,.24f,.20f),new Color(.46f,.36f,.26f),Random.value);
                    b.Spire(new Vector3(x,Ceiling(x,z)+.6f,z),radius,length+.4f,c,true);
                    if(i%4==0)b.Spire(new Vector3(x+1.3f,Ceiling(x+1.3f,z)+.6f,z+.5f),radius*.55f,length*.7f+.5f,c,true);
                }
                PlaceMesh("Estalactitas_"+quadrant,b.Mesh(),stone,parent,true);
            }
        }
        static void BuildDecoration(Transform parent)
        {
            for(int chunk=0;chunk<16;chunk++)
            {
                int qx=chunk%4,qz=chunk/4;var rocks=new Geometry();var plants=new Geometry();var teal=new Geometry();var orange=new Geometry();
                for(int i=0;i<100;i++)
                {
                    float x=-150+qx*80+Random.value*60,z=-150+qz*80+Random.value*60;
                    float h=Height(x,z);if(h<-.1f || Mathf.Abs(x)<7 || Mathf.Abs(z+49-Mathf.Sin(x*.025f)*9)<4)continue;
                    Color c=Color.Lerp(new Color(.30f,.31f,.25f),new Color(.48f,.41f,.31f),Random.value);
                    if(i%3==0)
                    {
                        float s=Random.Range(.45f,1.5f);
                        rocks.Rock(new Vector3(x,h-.1f,z),new Vector3(s,Random.Range(.5f,1.7f),s*.8f),c,7);
                    }
                    if(i%8==0)
                    {
                        bool red=x>55&&z>0;var batch=red?orange:teal;
                        for(int j=0;j<3;j++)batch.Spire(new Vector3(x+j*.45f,h,z+Mathf.Sin(j)*.5f),Random.Range(.22f,.45f),Random.Range(.9f,2.4f),Color.Lerp(Color.white,new Color(.55f,.75f,.7f),Random.value),false);
                    }
                    for(int j=0;j<10;j++)
                    {
                        float px=x+Random.Range(-2,2),pz=z+Random.Range(-2,2),py=Height(px,pz);
                        if(py<-.1f)continue;
                        Color grass=Color.Lerp(new Color(.19f,.36f,.19f),new Color(.55f,.64f,.28f),Random.value);
                        float ht=Random.Range(.25f,.65f),width=Random.Range(.08f,.18f);
                        for(int blade=0;blade<3;blade++)
                        {
                            float angle=blade*2.1f+Random.value;Vector3 side=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*width;
                            Vector3 p=new Vector3(px,py,pz);
                            plants.Blade(p-side,p+side,p+Vector3.up*ht+side*1.4f,grass);
                        }
                    }
                    if(i%14==0 && h<1)
                    {
                        // Mushroom-like subterranean flora, keeping the low roof legible.
                        float ht=Random.Range(1.2f,2.5f);
                        rocks.Spire(new Vector3(x,h,z),.16f,ht,new Color(.4f,.41f,.31f),false);
                        plants.Rock(new Vector3(x,h+ht*.65f,z),new Vector3(1.6f,.45f,1.3f),new Color(.18f,.48f,.39f),9);
                    }
                }
                PlaceMesh("Rocas_"+chunk,rocks.Mesh(),stone,parent,true);
                PlaceMesh("Flora_"+chunk,plants.Mesh(),foliage,parent,false);
                PlaceMesh("Cristales_celestes_"+chunk,teal.Mesh(),crystal,parent,false);
                PlaceMesh("Cristales_ambar_"+chunk,orange.Mesh(),amber,parent,false);
            }
            // Rock framing along each shore, rather than random props inside lakes.
            for(int i=0;i<Lakes.Length;i++)
            {
                var b=new Geometry();var l=Lakes[i];
                for(int j=0;j<32;j++)
                {
                    float a=j*Mathf.PI*2/32,r=LakeRadius(a,i)*1.065f;
                    float x=l.x+Mathf.Cos(a)*l.z*r,z=l.y+Mathf.Sin(a)*l.w*r;
                    if(j%4==0)continue;
                    float s=Random.Range(.55f,1.3f);
                    b.Rock(new Vector3(x,Height(x,z)-.05f,z),new Vector3(s,Random.Range(.4f,1.1f),s),i<2?new Color(.40f,.43f,.34f):new Color(.31f,.22f,.24f),7);
                }
                PlaceMesh("Orilla_"+i,b.Mesh(),stone,parent,true);
            }
        }
        static void BuildLandmarks(Transform parent)
        {
            var masonry=new Geometry();var glow=new Geometry();
            // Ancient segmented arch on the central flat summit: a visible navigation target.
            Vector3 center=new Vector3(22,5.6f,2);
            for(int i=0;i<13;i++)
            {
                float a=Mathf.PI*i/12;
                Vector3 p=center+new Vector3(Mathf.Cos(a)*3.3f,Mathf.Sin(a)*3.3f+.6f,0);
                masonry.Rock(p,new Vector3(.65f,.78f,.75f),new Color(.49f,.49f,.36f),6);
            }
            glow.Spire(center+new Vector3(0,0,.3f),.5f,2.3f,Color.white,false);
            // Wayfinding lanterns mark the main walkable spine.
            for(int i=0;i<14;i++)
            {
                float z=-125+i*18,x=Mathf.Sin(z*.024f)*13+5;
                float y=Height(x,z);
                masonry.Rock(new Vector3(x,y,z),new Vector3(.42f,.6f,.42f),new Color(.38f,.37f,.29f),6);
                glow.Spire(new Vector3(x,y+.6f,z),.18f,.65f,Color.white,false);
            }
            // Overlook and low stepping stones at the southern aquifer edge.
            for(int i=0;i<8;i++)
            {
                float x=-24-i*3.2f,z=-49;
                masonry.Rock(new Vector3(x,Height(x,z)-.1f,z),new Vector3(1.35f,.28f,1.5f),new Color(.56f,.53f,.40f),8);
            }
            PlaceMesh("Ruinas_y_balizas",masonry.Mesh(),stone,parent,true);
            PlaceMesh("Nexo_y_luces_de_ruta",glow.Mesh(),crystal,parent,false);
            var markers=new GameObject("Puntos de interes").transform;markers.SetParent(parent);
            Marker("Inicio · explanada central",new Vector3(0,0,-55),markers);
            Marker("Acuifero celeste",new Vector3(-55,0,-22),markers);
            Marker("Cuenca carmesi",new Vector3(83,0,34),markers);
            for(int i=0;i<Hills.Length;i++)Marker("Cima plana escalable "+(i+1),new Vector3(Hills[i].x,Hills[i].w,Hills[i].y),markers);
        }
        static void Marker(string name,Vector3 p,Transform parent){var g=new GameObject(name);g.transform.SetParent(parent);g.transform.position=p;}
        static void Lighting()
        {
            RenderSettings.skybox=null;RenderSettings.ambientMode=AmbientMode.Trilight;
            RenderSettings.ambientSkyColor=new Color(.29f,.38f,.41f);
            RenderSettings.ambientEquatorColor=new Color(.23f,.30f,.31f);
            RenderSettings.ambientGroundColor=new Color(.13f,.16f,.15f);
            RenderSettings.fog=true;RenderSettings.fogMode=FogMode.ExponentialSquared;
            RenderSettings.fogDensity=.009f;RenderSettings.fogColor=new Color(.10f,.20f,.22f);
            var go=new GameObject("Luz mineral difusa");go.transform.SetParent(world);
            go.transform.rotation=Quaternion.Euler(48,-32,0);
            var light=go.AddComponent<Light>();light.type=LightType.Directional;light.color=new Color(1,.88f,.70f);light.intensity=1.25f;
            light.shadows=LightShadows.Soft;RenderSettings.sun=light;
            // The roof does not shadow the whole cavern; local rocks still cast soft shadows.
            foreach(var r in world.GetChild(1).GetComponentsInChildren<MeshRenderer>())r.shadowCastingMode=ShadowCastingMode.Off;
            var volume=new GameObject("Ambiente cinematografico URP");volume.transform.SetParent(world);
            var v=volume.AddComponent<Volume>();v.isGlobal=true;
            string path=Root+"/CavernAtmosphere.asset";
            var profile=AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if(!profile){profile=ScriptableObject.CreateInstance<VolumeProfile>();AssetDatabase.CreateAsset(profile,path);}
            if(!profile.TryGet<Bloom>(out var bloom)){bloom=profile.Add<Bloom>(true);AssetDatabase.AddObjectToAsset(bloom,profile);}
            bloom.intensity.Override(.32f);bloom.threshold.Override(1.05f);bloom.scatter.Override(.65f);
            if(!profile.TryGet<Tonemapping>(out var tone)){tone=profile.Add<Tonemapping>(true);AssetDatabase.AddObjectToAsset(tone,profile);}
            tone.mode.Override(TonemappingMode.ACES);
            if(!profile.TryGet<ColorAdjustments>(out var color)){color=profile.Add<ColorAdjustments>(true);AssetDatabase.AddObjectToAsset(color,profile);}
            color.postExposure.Override(.35f);color.contrast.Override(7);color.saturation.Override(5);
            v.sharedProfile=profile;EditorUtility.SetDirty(profile);
        }
        static void Explorer()
        {
            var player=new GameObject("Explorador · 2 metros · WASD");
            player.transform.position=new Vector3(-9,Height(-9,-52)+.15f,-52);player.transform.rotation=Quaternion.Euler(0,-43,0);
            var cc=player.AddComponent<CharacterController>();cc.height=2;cc.center=new Vector3(0,1,0);cc.radius=.34f;cc.stepOffset=.3f;cc.slopeLimit=45;
            var c=new GameObject("Main Camera");c.tag="MainCamera";c.transform.SetParent(player.transform);c.transform.localPosition=new Vector3(0,1.78f,0);
            c.transform.localRotation=Quaternion.Euler(-1,0,0);
            var cam=c.AddComponent<Camera>();cam.fieldOfView=73;cam.nearClipPlane=.08f;cam.farClipPlane=250;
            cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=RenderSettings.fogColor;
            cam.GetUniversalAdditionalCameraData().renderPostProcessing=true;
            c.AddComponent<AudioListener>();player.AddComponent<CavernExplorer>().view=cam;
        }

        [MenuItem("Nexoterra/Mapa/Validar cuenca")]
        public static void Validate()
        {
            Physics.SyncTransforms();
            var failures=new List<string>();
            float horizontalScale=FindWorld().localScale.x;
            foreach(var r in UnityEngine.Object.FindObjectsByType<MeshRenderer>())
            {if(!r.sharedMaterial || !r.sharedMaterial.shader || ShaderUtil.ShaderHasError(r.sharedMaterial.shader))failures.Add("Invalid material: "+r.name);}
            float steepest=0,clearance=100;
            foreach(var h in Hills)
            {
                for(int a=0;a<32;a++)for(int r=0;r<60;r++)
                {
                    float ang=a*Mathf.PI/16,dist=h.z*r/60;
                    float x=h.x+Mathf.Cos(ang)*dist,z=h.y+Mathf.Sin(ang)*dist;
                    float slope=new Vector2(Height(x+.25f,z)-Height(x-.25f,z),Height(x,z+.25f)-Height(x,z-.25f)).magnitude/.5f;
                    steepest=Mathf.Max(steepest,Mathf.Atan(slope/horizontalScale)*Mathf.Rad2Deg);
                    clearance=Mathf.Min(clearance,Ceiling(x,z)-2.8f-Height(x,z));
                }
                if(Mathf.Abs(Height(h.x,h.y)-h.w)>.02f)failures.Add("Summit not flat: "+h);
            }
            if(steepest>45)failures.Add("Slope exceeds explorer limit: "+steepest);
            if(clearance<2.3f)failures.Add("Insufficient roof clearance");
            if(UnityEngine.Object.FindObjectsByType<CavernExplorer>().Length!=1)failures.Add("Expected one explorer");
            int reachable=VerifyTraversal(failures);
            string report="NEXOTERRA VALIDATION\n"+(failures.Count==0?"PASS":"FAIL")+"\nExtent: "+(320*horizontalScale)+" x "+(320*horizontalScale)+" m\nPlateaus: 6\nLakes: 2 cyan + 2 dark red\nMaximum sampled hill slope: "+steepest.ToString("F2")+" degrees\nMinimum conservative clearance above hills: "+clearance.ToString("F2")+" m\nSummits reached with a 2 m CharacterController: "+reachable+" / 6\n"+string.Join("\n",failures);
            File.WriteAllText(Result,report);Debug.Log(report);
            if(failures.Count>0)throw new Exception(report);
        }

        static int VerifyTraversal(List<string> failures)
        {
            int reached=0;
            float scale=FindWorld().localScale.x;
            var probe=new GameObject("Temporary traversal probe"){hideFlags=HideFlags.HideAndDontSave};
            var cc=probe.AddComponent<CharacterController>();cc.height=2;cc.radius=.34f;cc.center=Vector3.up;
            cc.stepOffset=.3f;cc.slopeLimit=45;
            try
            {
                foreach(var hill in Hills)
                {
                    bool success=false;
                    for(int direction=0;direction<12 && !success;direction++)
                    {
                        float angle=direction*Mathf.PI/6;
                        Vector3 dir=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));
                        Vector3 start=new Vector3(hill.x,0,hill.y)+dir*hill.z*1.06f;
                        start.y=Height(start.x,start.z)+.1f;start.x*=scale;start.z*=scale;
                        cc.enabled=false;probe.transform.position=start;cc.enabled=true;Physics.SyncTransforms();
                        cc.Move(Vector3.down*.4f);
                        int steps=Mathf.CeilToInt((hill.z*scale*.67f)/.12f);
                        for(int j=0;j<steps;j++)cc.Move(-dir*.12f+Vector3.down*.06f);
                        float distance=Vector2.Distance(new Vector2(probe.transform.position.x,probe.transform.position.z),new Vector2(hill.x*scale,hill.y*scale));
                        success=distance<hill.z*scale*.43f && Mathf.Abs(probe.transform.position.y-hill.w)<.25f;
                    }
                    if(success)reached++;else failures.Add("No tested walking route reached summit "+hill);
                }
            }
            finally{UnityEngine.Object.DestroyImmediate(probe);}
            return reached;
        }

        [MenuItem("Nexoterra/Mapa/Capturar vistas")]
        public static void Capture()
        {
            Directory.CreateDirectory("Documentation/Nexoterra/Previews");
            var cam=Camera.main;if(!cam)throw new Exception("No main camera");
            Vector3 pos=cam.transform.position;Quaternion rot=cam.transform.rotation;float fov=cam.fieldOfView;
            var points=new[]{new Vector3(-9,2,-52),new Vector3(-29,3,-60),new Vector3(45,7,2),new Vector3(55,3,9),new Vector3(2,7,70)};
            var targets=new[]{new Vector3(-50,2,-17),new Vector3(-56,0,-18),new Vector3(19,6,4),new Vector3(84,-.2f,37),new Vector3(-42,4,78)};
            var names=new[]{"01_entrada","02_acuifero","03_meseta","04_carmesi","05_boveda"};
            try
            {
                for(int i=0;i<points.Length;i++)
                {
                    float scale=FindWorld().localScale.x;
                    cam.transform.position=Vector3.Scale(points[i],new Vector3(scale,1,scale));cam.transform.LookAt(Vector3.Scale(targets[i],new Vector3(scale,1,scale)));cam.fieldOfView=78;
                    var rt=new RenderTexture(1600,900,24,RenderTextureFormat.ARGB32);rt.Create();
                    var previous=RenderTexture.active;
                    try
                    {
                        var request=new UniversalRenderPipeline.SingleCameraRequest{destination=rt};
                        RenderPipeline.SubmitRenderRequest(cam,request);
                        RenderTexture.active=rt;var tex=new Texture2D(1600,900,TextureFormat.RGB24,false);
                        tex.ReadPixels(new Rect(0,0,1600,900),0,0);tex.Apply();
                        File.WriteAllBytes("Documentation/Nexoterra/Previews/"+names[i]+".png",tex.EncodeToPNG());UnityEngine.Object.DestroyImmediate(tex);
                    }
                    finally{RenderTexture.active=previous;rt.Release();UnityEngine.Object.DestroyImmediate(rt);}
                }
                File.WriteAllText(Result,"CAPTURE_OK");
            }
            finally{cam.transform.position=pos;cam.transform.rotation=rot;cam.fieldOfView=fov;}
        }

        internal sealed class Geometry
        {
            readonly List<Vector3> vertices=new List<Vector3>();readonly List<int> indices=new List<int>();
            readonly List<Color> colors=new List<Color>();readonly List<Vector2> uvs=new List<Vector2>();
            public void Add(Vector3 p,Color c,Vector2 uv){vertices.Add(p);colors.Add(c);uvs.Add(uv);}
            public void Tri(int a,int b,int c){indices.Add(a);indices.Add(b);indices.Add(c);}
            public void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Color color)
            {int n=vertices.Count;Add(a,color,Vector2.zero);Add(b,color,Vector2.zero);Add(c,color,Vector2.up);Add(d,color,Vector2.up);Tri(n,n+2,n+1);Tri(n,n+3,n+2);}
            public void Blade(Vector3 a,Vector3 b,Vector3 tip,Color c)
            {int n=vertices.Count;Add(a,c*.65f,Vector2.zero);Add(b,c*.65f,Vector2.zero);Add(tip,c,Vector2.up);Tri(n,n+2,n+1);}
            public void Spire(Vector3 p,float radius,float height,Color color,bool down)
            {
                int sides=7;float sign=down?-1:1;float phase=Random.value*6;
                for(int i=0;i<sides;i++)
                {
                    float a=phase+i*Mathf.PI*2/sides,b=phase+(i+1)*Mathf.PI*2/sides;
                    Vector3 baseA=p+new Vector3(Mathf.Cos(a)*radius,0,Mathf.Sin(a)*radius),baseB=p+new Vector3(Mathf.Cos(b)*radius,0,Mathf.Sin(b)*radius);
                    Vector3 midA=p+new Vector3(Mathf.Cos(a)*radius*.63f,sign*height*.55f,Mathf.Sin(a)*radius*.63f);
                    Vector3 midB=p+new Vector3(Mathf.Cos(b)*radius*.63f,sign*height*.55f,Mathf.Sin(b)*radius*.63f);
                    Color shade=color*Random.Range(.78f,1.12f);shade.a=1;
                    if(down)Quad(baseB,baseA,midA,midB,shade);else Quad(baseA,baseB,midB,midA,shade);
                    int n=vertices.Count;Add(midA,shade,Vector2.zero);Add(midB,shade,Vector2.zero);Add(p+new Vector3(radius*.12f,sign*height,0),shade,Vector2.up);
                    if(down)Tri(n,n+1,n+2);else Tri(n,n+2,n+1);
                }
            }
            public void Rock(Vector3 p,Vector3 scale,Color color,int sides)
            {
                float phase=Random.value*6.28f;
                for(int i=0;i<sides;i++)
                {
                    float a=phase+i*Mathf.PI*2/sides,b=phase+(i+1)*Mathf.PI*2/sides;
                    Vector3 a0=p+Vector3.Scale(new Vector3(Mathf.Cos(a),0,Mathf.Sin(a)),scale);
                    Vector3 b0=p+Vector3.Scale(new Vector3(Mathf.Cos(b),0,Mathf.Sin(b)),scale);
                    Vector3 a1=p+Vector3.Scale(new Vector3(Mathf.Cos(a)*.75f,.7f,Mathf.Sin(a)*.75f),scale);
                    Vector3 b1=p+Vector3.Scale(new Vector3(Mathf.Cos(b)*.75f,.7f,Mathf.Sin(b)*.75f),scale);
                    Color shade=color*Random.Range(.85f,1.1f);shade.a=1;Quad(a0,b0,b1,a1,shade);
                    int n=vertices.Count;Add(a1,shade,Vector2.zero);Add(b1,shade,Vector2.zero);Add(p+Vector3.up*scale.y,shade,Vector2.zero);Tri(n,n+2,n+1);
                }
            }
            public Mesh Mesh()
            {
                var m=new Mesh();m.indexFormat=IndexFormat.UInt32;m.SetVertices(vertices);m.SetColors(colors);m.SetUVs(0,uvs);m.SetTriangles(indices,0);m.RecalculateNormals();m.RecalculateBounds();return m;
            }
        }
    }
}
