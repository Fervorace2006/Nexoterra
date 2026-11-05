using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Nexoterra.World;
using Random=UnityEngine.Random;
using Geo=Nexoterra.Editor.NexoterraCavernBuilder.Geometry;

namespace Nexoterra.Editor
{
    [InitializeOnLoad]
    public static class NexoterraHabitatUpgrade
    {
        const string Folder="Assets/NexoterraWorld/Habitats";
        static Transform root;
        static Material stone,dirt,wood,rope,foliage,stem;
        static Material[] tops,caps;
        static Mesh capMesh;
        static int serial;
        static bool repairModels=true;
        static readonly List<(Vector3 a,Vector3 b,float width)> corridors=new List<(Vector3,Vector3,float)>();
        static readonly string[] Names={"Jardines del Nexo","Cavernas de hielo","Tierras antiguas","Caldera volcanica","Galeria acuatica","Cienaga carmesi"};
        static readonly Vector3[] Camps={new Vector3(275,0,-270),new Vector3(-225,0,5),new Vector3(-210,0,195),new Vector3(190,0,170),new Vector3(105,0,-48),new Vector3(-230,0,-220)};
        static readonly Color[] Colors={new Color(.29f,.53f,.12f),new Color(.52f,.76f,.85f),new Color(.38f,.40f,.16f),new Color(.32f,.09f,.035f),new Color(.13f,.45f,.34f),new Color(.17f,.055f,.075f)};
        static Vector3[] first=new Vector3[6],second=new Vector3[6];
        static Vector3 waterShrine,fireShrine;
        static NexoterraHabitatUpgrade(){EditorApplication.update+=Poll;}
        static void Poll()
        {
            if(EditorApplication.isCompiling||EditorApplication.isUpdating||!File.Exists("Library/NexoterraHabitats.request"))return;
            if(EditorApplication.isPlayingOrWillChangePlaymode){EditorApplication.isPlaying=false;return;}
            string command=File.ReadAllText("Library/NexoterraHabitats.request").Trim();File.Delete("Library/NexoterraHabitats.request");
            try{if(command=="capture")Capture();else if(command=="validate")Validate();else {repairModels=command!="landscape";try{Build();}finally{repairModels=true;}}}
            catch(Exception e){File.WriteAllText("Library/NexoterraHabitats.result.txt","FAIL\n"+e);Debug.LogException(e);}
        }
        public static void BatchAudit()
        {
            EditorSceneManager.OpenScene(NexoterraBiomeBuilder.ScenePath);
            Func<Transform,string> path=null;path=t=>t.parent?path(t.parent)+"/"+t.name:t.name;
            var lines=UnityEngine.Object.FindObjectsByType<Renderer>().Where(r=>Vector3.Distance(r.bounds.center,Camps[0])<120).OrderByDescending(r=>r.bounds.size.sqrMagnitude).Take(70).Select(r=>path(r.transform)+" | "+r.bounds+" | "+string.Join(",",r.sharedMaterials.Select(m=>m?m.name:"NULL"))).ToList();
            foreach(var l in UnityEngine.Object.FindObjectsByType<Light>())lines.Add("LIGHT "+path(l.transform)+" "+l.type+" intensity="+l.intensity+" range="+l.range+" color="+l.color);
            File.WriteAllLines("Library/NexoterraHabitatAudit.txt",lines);
        }
        public static void BatchLandscape(){EditorSceneManager.OpenScene(NexoterraBiomeBuilder.ScenePath);repairModels=false;try{Build();}finally{repairModels=true;}Capture();}
        public static void BatchBuild(){EditorSceneManager.OpenScene(NexoterraBiomeBuilder.ScenePath);Build();Capture();}
        [MenuItem("Nexoterra/Mundo/Mejorar habitats y reparar especies")]
        public static void Build()
        {
            if(EditorApplication.isPlaying)throw new Exception("Salir de Play primero.");
            var scene=SceneManager.GetActiveScene();if(scene.path!=NexoterraBiomeBuilder.ScenePath)throw new Exception("Abrir CuatroNodos.");
            EditorSceneManager.SaveScene(scene);File.Copy(scene.path,"Library/NexoterraAntesHabitats_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".unity");
            var world=scene.GetRootGameObjects().First(g=>g.name.StartsWith("NEXOTERRA ·"));
            // Remove only the generated population; this also removes missing prefab previews.
            foreach(var spawn in world.GetComponentsInChildren<SlugSpawnPoint>(true))UnityEngine.Object.DestroyImmediate(spawn.gameObject);
            var old=root=world.transform.Find("Senderos y santuarios");if(old)UnityEngine.Object.DestroyImmediate(old.gameObject);
            var oldMushrooms=world.transform.Find("Hongos · comida y bioluminiscencia");if(oldMushrooms)UnityEngine.Object.DestroyImmediate(oldMushrooms.gameObject);
            if(repairModels)NexoterraSlugsSetup.Install();
            root=new GameObject("Senderos y santuarios").transform;root.SetParent(world.transform,false);
            Directory.CreateDirectory(Folder+"/Meshes");Directory.CreateDirectory(Folder+"/Materials");AssetDatabase.Refresh();Random.InitState(314159);serial=0;
            Materials();capMesh=Save(Cap(),"Sombrero_organico");
            corridors.Clear();AssetDatabase.StartAssetEditing();
            try{for(int zone=0;zone<6;zone++)BuildZone(zone);BuildShrines();}
            finally{AssetDatabase.StopAssetEditing();}
            BuildPopulation(world.transform);BuildPracticeTargets(world.transform);NexoterraVfxSetup.Ensure();
            AssetDatabase.SaveAssets();Physics.SyncTransforms();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            Validate();
        }
        static Material Mat(string name,Color color,float strata=0,float emission=0,float spots=0)
        {
            string path=Folder+"/Materials/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!m){m=new Material(Shader.Find("Nexoterra/Cavern Surface"));AssetDatabase.CreateAsset(m,path);}
            m.SetColor("_BaseColor",color);m.SetFloat("_Strata",strata);m.SetFloat("_Detail",.8f);m.SetFloat("_Emission",emission);m.SetFloat("_Spots",spots);EditorUtility.SetDirty(m);return m;
        }
        static void Materials()
        {
            stone=Mat("Roca_estratificada",new Color(.42f,.33f,.25f),1);dirt=Mat("Sendero_ocre",new Color(.37f,.21f,.085f),.15f);
            wood=Mat("Tablones",new Color(.26f,.14f,.06f),.75f);rope=Mat("Cuerdas",new Color(.37f,.34f,.19f));
            foliage=Mat("Helechos",new Color(.15f,.39f,.09f));foliage.SetFloat("_Wind",.5f);stem=Mat("Tallos_curvos",new Color(.16f,.14f,.24f),.3f);
            tops=Colors.Select((c,i)=>Mat("Superficie_"+i,c,.3f)).ToArray();
            caps=new[]{Mat("Hongo_azul",new Color(.08f,.24f,.56f),.1f,.3f,1),Mat("Hongo_celeste",new Color(.045f,.46f,.48f),.1f,.28f,1),Mat("Hongo_violeta",new Color(.32f,.09f,.39f),.1f,.35f,1)};
        }
        static Mesh Save(Mesh mesh,string name)
        {
            mesh.name=name;string path=Folder+"/Meshes/"+name+".asset";var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(saved){saved.Clear();saved.indexFormat=mesh.indexFormat;saved.vertices=mesh.vertices;saved.triangles=mesh.triangles;saved.normals=mesh.normals;saved.colors=mesh.colors;saved.uv=mesh.uv;saved.RecalculateBounds();UnityEngine.Object.DestroyImmediate(mesh);EditorUtility.SetDirty(saved);return saved;}
            AssetDatabase.CreateAsset(mesh,path);return mesh;
        }
        static GameObject Object(string name,Mesh mesh,Material mat,bool collision=false,Transform parent=null)
        {
            var go=new GameObject(name);go.transform.SetParent(parent?parent:root,false);go.AddComponent<MeshFilter>().sharedMesh=Save(mesh,(serial++)+"_"+name);
            go.AddComponent<MeshRenderer>().sharedMaterial=mat;if(collision)go.AddComponent<MeshCollider>().sharedMesh=go.GetComponent<MeshFilter>().sharedMesh;return go;
        }
        static float Ground(Vector3 p)
        {
            float y=-3;
            foreach(var h in Physics.RaycastAll(new Vector3(p.x,60,p.z),Vector3.down,80,1,QueryTriggerInteraction.Ignore))
            {
                string name=h.collider.name;if(name.StartsWith("Techo_")||name.StartsWith("Estalactitas_")||name=="Union_bovedas"||name.Contains("Hongo"))continue;
                if(h.normal.y>.6f)y=Mathf.Max(y,h.point.y);
            }
            return y;
        }
        static Vector3 OnGround(Vector3 p,float offset=.04f){p.y=Ground(p)+offset;return p;}
        static Vector3 Terrace(string name,Vector3 center,float radius,float rise,int zone)
        {
            float ground=Ground(center);center.y=ground+rise;var wall=new Geo();var top=new Geo();const int sides=40;
            Vector3[] rim=new Vector3[sides];
            for(int i=0;i<sides;i++){float a=i*Mathf.PI*2/sides;float r=radius*(1+.045f*Mathf.Sin(a*5));rim[i]=center+new Vector3(Mathf.Cos(a)*r,0,Mathf.Sin(a)*r);}
            for(int i=0;i<sides;i++)
            {
                int j=(i+1)%sides;top.Add(center,Color.white,Vector2.zero);top.Add(rim[j],Color.white,Vector2.one);top.Add(rim[i],Color.white,Vector2.zero);top.Tri(i*3,i*3+1,i*3+2);
                for(int band=0;band<5;band++)
                {
                    float lower=ground-.4f+(rise+.4f)*band/5,upper=ground-.4f+(rise+.4f)*(band+1)/5;
                    Vector3 a=rim[i],b=rim[j],c=rim[j],d=rim[i];a.y=b.y=lower;c.y=d.y=upper;
                    float scale=1+(band%2==0?.015f:-.02f);a.x=center.x+(a.x-center.x)*scale;a.z=center.z+(a.z-center.z)*scale;b.x=center.x+(b.x-center.x)*scale;b.z=center.z+(b.z-center.z)*scale;
                    wall.Quad(a,b,c,d,Color.Lerp(Color.white,Colors[zone],.27f)*(band%2==0?.86f:1.06f));
                }
            }
            Object(name+"_acantilado",wall.Mesh(),stone,true);Object(name+"_cima",top.Mesh(),tops[zone],true);Physics.SyncTransforms();return center;
        }
        static void Tube(Geo g,Vector3 a,Vector3 b,float radius,Color color,int sides=7)
        {
            Vector3 forward=(b-a).normalized,right=Vector3.Cross(forward,Vector3.forward).normalized;if(right.sqrMagnitude<.01f)right=Vector3.right;
            Vector3 up=Vector3.Cross(forward,right).normalized;
            for(int i=0;i<sides;i++){float x=i*Mathf.PI*2/sides,y=(i+1)*Mathf.PI*2/sides;Vector3 r=(right*Mathf.Cos(x)+up*Mathf.Sin(x))*radius,s=(right*Mathf.Cos(y)+up*Mathf.Sin(y))*radius;g.Quad(a+s,a+r,b+r,b+s,color);}
        }
        static void Walkway(string name,Vector3[] points,float width,bool bridge,int zone)
        {
            if(bridge)
            {
                var smooth=new List<Vector3>{points[0]};
                for(int i=1;i<points.Length-1;i++)
                {
                    Vector3 before=points[i]-points[i-1],after=points[i+1]-points[i];
                    float trim=Mathf.Min(10,Mathf.Min(before.magnitude,after.magnitude)*.45f);
                    Vector3 a=points[i]-before.normalized*trim,b=points[i]+after.normalized*trim;
                    for(int j=0;j<=8;j++){float t=j/8f;smooth.Add((1-t)*(1-t)*a+2*(1-t)*t*points[i]+t*t*b);}
                }
                smooth.Add(points[points.Length-1]);points=smooth.ToArray();
                float length=0;var distances=new float[points.Length];
                for(int i=1;i<points.Length;i++){length+=Vector2.Distance(new Vector2(points[i].x,points[i].z),new Vector2(points[i-1].x,points[i-1].z));distances[i]=length;}
                float startHeight=points[0].y,endHeight=points[points.Length-1].y;
                for(int i=0;i<points.Length;i++)points[i].y=Mathf.Lerp(startHeight,endHeight,Mathf.InverseLerp(6.5f,length-5.5f,distances[i]));
            }
            for(int i=0;i<points.Length-1;i++)corridors.Add((points[i],points[i+1],width));
            var deck=new Geo();var rails=new Geo();var supports=new Geo();
            for(int n=0;n<points.Length-1;n++)
            {
                Vector3 from=points[n],to=points[n+1],direction=to-from;Vector3 right=Vector3.Cross(Vector3.up,direction).normalized;
                Vector3 r0=n==0?right:(right+Vector3.Cross(Vector3.up,points[n]-points[n-1]).normalized).normalized;
                Vector3 r1=n==points.Length-2?right:(right+Vector3.Cross(Vector3.up,points[n+2]-points[n+1]).normalized).normalized;
                r0/=Mathf.Max(.35f,Vector3.Dot(r0,right));r1/=Mathf.Max(.35f,Vector3.Dot(r1,right));
                int count=Mathf.Max(1,Mathf.CeilToInt(direction.magnitude/(bridge?.65f:1.5f)));
                for(int i=0;i<count;i++)
                {
                    Vector3 a=Vector3.Lerp(from,to,i/(float)count),b=Vector3.Lerp(from,to,(i+1)/(float)count);
                    Vector3 ra=Vector3.Lerp(r0,r1,i/(float)count),rb=Vector3.Lerp(r0,r1,(i+1)/(float)count);
                    deck.Quad(a-ra*width*.5f,a+ra*width*.5f,b+rb*width*.5f,b-rb*width*.5f,Color.white*(i%2==0?1:.9f));
                    if(bridge||i%3==0)
                    {
                        for(int side=-1;side<=1;side+=2)
                        {
                            Vector3 edge=a+ra*(width*.5f+.1f)*side;
                            if(i%3==0)Tube(rails,edge,edge+Vector3.up*1.05f,.08f,Color.white);
                            if(bridge)Tube(rails,edge+Vector3.up*.75f,b+rb*(width*.5f+.1f)*side+Vector3.up*.75f,.04f,Color.white);
                        }
                    }
                    if(!bridge)
                    {
                        Vector3 lowA=a-right*width*.5f,lowB=b-right*width*.5f;lowA.y=Ground(lowA);lowB.y=Ground(lowB);
                        supports.Quad(lowA,lowB,b-right*width*.5f,a-right*width*.5f,Color.white);
                        lowA=a+right*width*.5f;lowB=b+right*width*.5f;lowA.y=Ground(lowA);lowB.y=Ground(lowB);
                        supports.Quad(lowB,lowA,a+right*width*.5f,b+right*width*.5f,Color.white);
                    }
                }
            }
            Object(name,deck.Mesh(),bridge?wood:zone==1?tops[1]:dirt,true);Object(name+"_barandas",rails.Mesh(),rope,bridge);
            if(!bridge)Object(name+"_base",supports.Mesh(),stone,true);Physics.SyncTransforms();
        }
        static bool OnPath(Vector3 p)
        {
            Vector2 point=new Vector2(p.x,p.z);
            foreach(var segment in corridors)
            {
                Vector2 a=new Vector2(segment.a.x,segment.a.z),b=new Vector2(segment.b.x,segment.b.z),d=b-a;
                float t=Mathf.Clamp01(Vector2.Dot(point-a,d)/Mathf.Max(.001f,d.sqrMagnitude));
                if(Vector2.Distance(point,a+d*t)<segment.width*.5f+1.3f)return true;
            }
            return false;
        }
        static void BuildZone(int zone)
        {
            Vector3 camp=OnGround(Camps[zone]);first[zone]=Terrace("Terraza_"+zone,camp+new Vector3(-12,0,24),13,3.2f,zone);
            second[zone]=Terrace("Cornisa_"+zone,camp+new Vector3(-35,0,50),11,6.2f,zone);
            Vector3 foot=OnGround(camp+new Vector3(7,0,6));Vector3 approach=foot-first[zone];approach.y=0;approach.Normalize();
            Walkway("Subida_"+zone,new[]{foot,first[zone]+approach*14.5f+Vector3.up*.08f,first[zone]+approach*5+Vector3.up*.08f},5,false,zone);
            Vector3 delta=second[zone]-first[zone];delta.y=0;delta.Normalize();
            Vector3 middle=(first[zone]+second[zone])*.5f+Vector3.Cross(Vector3.up,delta)*17;
            Walkway("Puente_"+zone,new[]{first[zone]+delta*8+Vector3.up*.09f,first[zone]+delta*14.5f+Vector3.up*.09f,middle+Vector3.up*.09f,second[zone]-delta*12.5f+Vector3.up*.09f,second[zone]-delta*7+Vector3.up*.09f},3.5f,true,zone);
            Walkway("Sendero_entrada_"+zone,new[]{OnGround(camp),OnGround(camp+new Vector3(9,0,3)),OnGround(camp+new Vector3(7,0,6))},4,false,zone);
            for(int i=0;i<7;i++)
            {
                float angle=i*2.4f;Vector3 p=i<3?first[zone]+new Vector3(Mathf.Cos(angle)*8,0,Mathf.Sin(angle)*8):camp+new Vector3(Mathf.Cos(angle)*Random.Range(15,30),0,Mathf.Sin(angle)*Random.Range(15,30));
                p=OnGround(p);
                for(int attempt=0;attempt<8&&OnPath(p);attempt++){float a=attempt*.9f;p=OnGround(p+new Vector3(Mathf.Cos(a)*5,0,Mathf.Sin(a)*5));}
                if(!OnPath(p))Mushroom(p,Random.Range(3.2f,6.5f),zone,i,true);
            }
            for(int i=0;i<12;i++)
            {
                Vector3 p=OnGround(camp+new Vector3(Random.Range(-12,12),0,Random.Range(-9,15)));
                if(p.y<0)continue;Mushroom(p,Random.Range(.4f,.8f),zone,i,false);
            }
            var stones=new Geo();var plants=new Geo();
            for(int i=0;i<30;i++)
            {
                Vector3 p=OnGround(camp+new Vector3(Random.Range(-35,15),0,Random.Range(8,58)));
                // Keep the center of the ramps and bridges clear.
                if(Vector2.Distance(new Vector2(p.x,p.z),new Vector2(first[zone].x,first[zone].z))<6)continue;
                if(OnPath(p))continue;
                if(i%3==0)stones.Rock(p,new Vector3(Random.Range(.7f,1.4f),Random.Range(1,2.1f),Random.Range(.6f,1.3f)),Color.Lerp(Color.white,Colors[zone],.2f),9);
                for(int leaf=0;leaf<9;leaf++)
                {
                    float a=leaf*2.399f;Vector3 d=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));Vector3 side=Vector3.Cross(Vector3.up,d)*.22f;
                    plants.Blade(p-side,p+side,p+d*Random.Range(1.0f,2)+Vector3.up*Random.Range(.7f,1.4f),Color.Lerp(Color.white,Colors[zone],.4f));
                }
            }
            Object("Rocas_sendero_"+zone,stones.Mesh(),stone,true);Object("Helechos_sendero_"+zone,plants.Mesh(),zone==1?tops[1]:foliage);
        }
        static Mesh Cap()
        {
            var g=new Geo();const int sides=36,rings=12;
            for(int ring=0;ring<=rings;ring++)for(int i=0;i<=sides;i++)
            {
                float t=ring/(float)rings,a=i*Mathf.PI*2/sides;float radius=Mathf.Sin(t*Mathf.PI*.5f),height=Mathf.Cos(t*Mathf.PI*.5f)*.65f;
                var p=new Vector3(Mathf.Cos(a)*radius,height,Mathf.Sin(a)*radius);
                g.Add(p,Color.Lerp(new Color(.45f,.50f,.65f),Color.white,1-t),new Vector2(i/(float)sides,t));
            }
            for(int r=0;r<rings;r++)for(int i=0;i<sides;i++){int a=r*(sides+1)+i;g.Tri(a,a+1,a+sides+1);g.Tri(a+1,a+sides+2,a+sides+1);}
            for(int i=0;i<sides;i++){float a=i*Mathf.PI*2/sides,b=(i+1)*Mathf.PI*2/sides;g.Blade(new Vector3(Mathf.Cos(a),0,Mathf.Sin(a)),new Vector3(Mathf.Cos(b),0,Mathf.Sin(b)),Vector3.down*.12f,new Color(.45f,.75f,.8f));}
            return g.Mesh();
        }
        static void Mushroom(Vector3 p,float height,int zone,int index,bool large)
        {
            var parent=new GameObject(large?"Hongo arboreo":"Hongo comestible").transform;parent.SetParent(root,false);parent.position=p;
            var g=new Geo();Vector3 previous=Vector3.zero;float bend=Random.Range(-.25f,.25f)*height;
            for(int i=1;i<=6;i++){float t=i/6f;Vector3 next=new Vector3(bend*t*t,height*t,Mathf.Sin(t*2)*height*.08f);Tube(g,previous,next,height*(.055f-.025f*t),Color.white);previous=next;}
            Object("Hongo_tallo_"+zone+"_"+index+"_"+large,g.Mesh(),stem,large,parent);
            int branches=large?3:1;
            for(int i=0;i<branches;i++)
            {
                var cap=new GameObject("Hongo_sombrero");cap.transform.SetParent(parent,false);cap.transform.localPosition=previous*(1-i*.22f)+new Vector3(i==1?height*.28f:i==2?-height*.23f:0,0,i*.1f);
                cap.transform.localScale=Vector3.one*height*(i==0?.37f:.23f);cap.transform.localRotation=Quaternion.Euler(i*6,0,i==1?-12:6);
                cap.AddComponent<MeshFilter>().sharedMesh=capMesh;cap.AddComponent<MeshRenderer>().sharedMaterial=caps[(zone+index+i)%3];
                if(i>0){var branch=new Geo();Tube(branch,previous*(1-i*.22f),cap.transform.localPosition,height*.025f,Color.white);Object("Hongo_rama",branch.Mesh(),stem,false,parent);}
            }
            if(!large)parent.gameObject.AddComponent<MushroomFood>();
            else
            {
                var go=new GameObject("Luz del hongo");go.transform.SetParent(parent,false);go.transform.localPosition=previous-Vector3.up*.2f;
                var light=go.AddComponent<Light>();light.type=LightType.Point;light.color=(zone+index)%3==0?new Color(.2f,.35f,1):new Color(.15f,.8f,.8f);light.intensity=1.1f;light.range=height*2;light.shadows=LightShadows.None;
                parent.gameObject.AddComponent<MushroomGlow>().glow=light;
            }
        }
        static void BuildShrines()
        {
            Vector3 pond=Camps[4]+new Vector3(28,0,30);
            pond.y=Ground(pond)+.12f;
            // Water follows the basin floor, not the raised shrine platform.
            waterShrine=Terrace("Islote_elemental_agua",pond,5.5f,3,4);
            var bank=new Geo();const int sides=80;
            for(int i=0;i<sides;i++)
            {
                float a=i*Mathf.PI*2/sides,b=(i+1)*Mathf.PI*2/sides;
                Vector3 innerA=pond+new Vector3(Mathf.Cos(a)*18.8f,.28f,Mathf.Sin(a)*14.8f),innerB=pond+new Vector3(Mathf.Cos(b)*18.8f,.28f,Mathf.Sin(b)*14.8f);
                Vector3 outerA=pond+new Vector3(Mathf.Cos(a)*22,0,Mathf.Sin(a)*18),outerB=pond+new Vector3(Mathf.Cos(b)*22,0,Mathf.Sin(b)*18);
                outerA.y=Ground(outerA)+.025f;outerB.y=Ground(outerB)+.025f;
                bank.Quad(outerA,innerA,innerB,outerB,Color.white);
                Vector3 bottomA=innerA,bottomB=innerB;bottomA.y=bottomB.y=pond.y-.18f;
                bank.Quad(bottomA,bottomB,innerB,innerA,Color.white);
            }
            Object("Orillas_laguna_santuario",bank.Mesh(),tops[4],true);
            var g=new Geo();
            for(int i=0;i<sides;i++){float a=i*Mathf.PI*2/sides,b=(i+1)*Mathf.PI*2/sides;g.Add(pond,Color.white,Vector2.zero);g.Add(pond+new Vector3(Mathf.Cos(b)*19,0,Mathf.Sin(b)*15),Color.white,Vector2.one);g.Add(pond+new Vector3(Mathf.Cos(a)*19,0,Mathf.Sin(a)*15),Color.white,Vector2.one);g.Tri(i*3,i*3+1,i*3+2);}
            Object("Laguna_del_santuario",g.Mesh(),AssetDatabase.LoadAssetAtPath<Material>("Assets/NexoterraWorld/Biomes/Materials/Agua_azul.mat"));
            Vector3 foot=OnGround(Camps[4]+new Vector3(7,0,7));Vector3 approach=foot-waterShrine;approach.y=0;approach.Normalize();
            Walkway("Acceso_al_islote",new[]{foot,waterShrine+approach*6.5f+Vector3.up*.08f,waterShrine+approach*2+Vector3.up*.08f},3,false,4);
            fireShrine=OnGround(new Vector3(190,0,239),.10f);
            var path=new List<Vector3>();for(int i=0;i<=48;i++){float t=i/48f,a=Mathf.Lerp(-Mathf.PI*.5f,Mathf.PI*.5f,t),r=Mathf.Lerp(48,9,t);path.Add(OnGround(new Vector3(190+Mathf.Cos(a)*r,0,230+Mathf.Sin(a)*r),.065f));}
            // Lift the approach before abrupt rock ledges; never bury the trail in them.
            for(int pass=0;pass<2;pass++)
            {
                for(int i=path.Count-2;i>=0;i--){Vector3 p=path[i];float run=Vector2.Distance(new Vector2(p.x,p.z),new Vector2(path[i+1].x,path[i+1].z));p.y=Mathf.Max(p.y,path[i+1].y-run*.55f);path[i]=p;}
                for(int i=1;i<path.Count;i++){Vector3 p=path[i];float run=Vector2.Distance(new Vector2(p.x,p.z),new Vector2(path[i-1].x,path[i-1].z));p.y=Mathf.Max(p.y,path[i-1].y-run*.55f);path[i]=p;}
            }
            Walkway("Sendero_al_crater",path.ToArray(),2.8f,false,3);
            foreach(var info in new[]{(second[1],1),(second[2],2),(waterShrine,4),(fireShrine,3)})
            {
                var shrine=new Geo();for(int i=0;i<6;i++){float a=i*Mathf.PI/3;Vector3 p=info.Item1+new Vector3(Mathf.Cos(a)*2.4f,0,Mathf.Sin(a)*2.4f);shrine.Spire(p,.25f,info.Item2==3?.7f:1.3f,Colors[info.Item2],false);}
                Object("Santuario_"+info.Item2,shrine.Mesh(),tops[info.Item2]);
            }
        }
        static GameObject Creature(SlugDefinition definition)
        {
            string folder="Assets/NexoterraWorld/Biomes/Creatures/";
            if(definition.id=="Infierno"&&File.Exists(folder+"Burpy.prefab")&&!File.Exists(folder+"Infierno.prefab"))AssetDatabase.MoveAsset(folder+"Burpy.prefab",folder+"Infierno.prefab");
            var holder=new GameObject(definition.id);var model=UnityEngine.Object.Instantiate(definition.normal,holder.transform);model.transform.localPosition=Vector3.up*.325f;model.transform.localScale=Vector3.one*.65f;
            var slug=holder.AddComponent<WildSlug>();slug.species=definition.id;slug.roamRadius=definition.unique?.65f:2;
            var result=PrefabUtility.SaveAsPrefabAsset(holder,folder+definition.id+".prefab");UnityEngine.Object.DestroyImmediate(holder);return result;
        }
        static void BuildPopulation(Transform world)
        {
            var old=world.Find("Spawns de babosas por bioma");if(old)UnityEngine.Object.DestroyImmediate(old.gameObject);
            var group=new GameObject("Spawns de babosas por bioma").transform;group.SetParent(world,false);
            string[][] ids={new[]{"tazerling","Gon_Doc","Armashelt","Carnero","Arana"},new[]{"ice","Armashelt","Carnero","Gon_Doc","Elemental_Aire"},new[]{"Carnero","Arana","Armashelt","Gon_Doc","Elemental_Tierra"},new[]{"Infierno","tazerling","Armashelt","Carnero","Elemental_Fuego"},new[]{"Gon_Doc","ice","Arana","Armashelt","Elemental_Agua"},new[]{"Infierno","Arana","tazerling","Carnero"}};
            var catalog=AssetDatabase.LoadAssetAtPath<SlugCatalog>("Assets/NexoterraWorld/Slugs/Catalog.asset");var prefabs=catalog.species.ToDictionary(d=>d.id,Creature);
            for(int zone=0;zone<6;zone++)for(int i=0;i<ids[zone].Length;i++)
            {
                string id=ids[zone][i];Vector3 pos=i==0?Camps[zone]+new Vector3(-4,0,7):i==1?first[zone]+new Vector3(5,0,3):i==2?Camps[zone]+new Vector3(12,0,17):second[zone]+new Vector3(-3,0,2);
                if(id=="Elemental_Aire")pos=second[1];if(id=="Elemental_Tierra")pos=second[2];if(id=="Elemental_Agua")pos=waterShrine;if(id=="Elemental_Fuego")pos=fireShrine;
                pos=OnGround(pos,.03f);var go=new GameObject("Spawn · "+Names[zone]+" · "+id);go.transform.SetParent(group,false);go.transform.position=pos;
                var spawn=go.AddComponent<SlugSpawnPoint>();spawn.species=id;spawn.biome=Names[zone];spawn.population=1;spawn.unique=catalog.Find(id).unique;spawn.activeDistance=110;spawn.creaturePrefab=prefabs[id];
                var preview=(GameObject)PrefabUtility.InstantiatePrefab(prefabs[id]);preview.transform.SetParent(go.transform,false);preview.name="Vista previa · se sustituye al jugar";spawn.editorPreview=preview.transform;
            }
        }
        static void BuildPracticeTargets(Transform world)
        {
            if(world.Find("Objetivos de practica"))return;
            var group=new GameObject("Objetivos de practica").transform;group.SetParent(world,false);
            var armor=Mat("Armadura_objetivos",new Color(.32f,.14f,.11f),.2f);
            var eye=Mat("Visor_objetivos",new Color(.08f,.8f,1),0,1);
            for(int i=0;i<3;i++)
            {
                var go=new GameObject("Objetivo de practica "+(i+1));go.transform.SetParent(group,false);go.transform.position=OnGround(new Vector3(i==1?305:295,0,-255+i*10),.03f);
                var collider=go.AddComponent<CapsuleCollider>();collider.height=2.2f;collider.radius=.42f;collider.center=Vector3.up*1.1f;
                var visual=new GameObject("Cuerpo").transform;visual.SetParent(go.transform,false);
                foreach(var part in new[]{("Torso",PrimitiveType.Capsule,new Vector3(0,1,0),new Vector3(.8f,.65f,.65f)),("Cabeza",PrimitiveType.Sphere,new Vector3(0,1.9f,0),Vector3.one*.55f),("Visor",PrimitiveType.Cube,new Vector3(0,1.95f,-.26f),new Vector3(.38f,.1f,.05f)),("Brazos",PrimitiveType.Cube,new Vector3(0,1.3f,0),new Vector3(1.2f,.18f,.22f))})
                {
                    var mesh=GameObject.CreatePrimitive(part.Item2);mesh.name=part.Item1;mesh.transform.SetParent(visual,false);mesh.transform.localPosition=part.Item3;mesh.transform.localScale=part.Item4;UnityEngine.Object.DestroyImmediate(mesh.GetComponent<Collider>());mesh.GetComponent<Renderer>().sharedMaterial=part.Item1=="Visor"?eye:armor;
                }
                var enemy=go.AddComponent<EnemyCombatant>();enemy.visualRoot=visual;enemy.displayName="Practica "+(i+1);enemy.maxHealth=160;
                go.AddComponent<EnemyPracticeTarget>();
            }
        }
        [MenuItem("Nexoterra/Mundo/Validar habitats")]
        public static void Validate()
        {
            var scene=SceneManager.GetActiveScene();var spawns=UnityEngine.Object.FindObjectsByType<SlugSpawnPoint>();
            var errors=new List<string>();if(spawns.Length!=29)errors.Add("Se esperaban 29 spawns");
            foreach(var group in spawns.GroupBy(s=>s.biome))if(group.Count()<4||group.Count()>5||group.Select(s=>s.species).Distinct().Count()!=group.Count()||group.Any(s=>s.population!=1))errors.Add("Poblacion incorrecta: "+group.Key);
            foreach(var group in spawns.Where(s=>s.unique).GroupBy(s=>s.species))if(group.Count()!=1)errors.Add("Elemental duplicada: "+group.Key);
            if(spawns.Count(s=>s.unique)!=4)errors.Add("Deben existir cuatro elementales unicas");
            foreach(var s in spawns)if(!s.creaturePrefab||!s.editorPreview||s.editorPreview.GetComponentsInChildren<Renderer>().Length==0)errors.Add("Spawn sin modelo: "+s.name);
            foreach(var t in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)))if(PrefabUtility.IsPrefabAssetMissing(t.gameObject))errors.Add("Prefab perdido: "+t.name);
            foreach(var r in UnityEngine.Object.FindObjectsByType<Renderer>())foreach(var m in r.sharedMaterials)if(!m||!m.shader||ShaderUtil.ShaderHasError(m.shader))errors.Add("Material incorrecto: "+r.name);
            string report=(errors.Count==0?"PASS":"FAIL")+"\n29 babosas: 5 en cada zona y 4 en Cienaga carmesi.\n4 elementales, una por especie.\n"+string.Join("\n",errors);
            File.WriteAllText("Library/NexoterraHabitats.result.txt",report);if(errors.Count>0)throw new Exception(report);
        }
        public static void Capture()
        {
            Directory.CreateDirectory("Documentation/Nexoterra/Habitats");var cam=Camera.main;var p=cam.transform.position;var rotation=cam.transform.rotation;float fov=cam.fieldOfView;
            try
            {
                for(int i=0;i<7;i++)
                {
                    Vector3 focus=i==6?new Vector3(133,2,-18):Camps[i]+new Vector3(-12,3,24);
                    cam.transform.position=i==6?new Vector3(160,12,-44):Camps[i]+new Vector3(28,14,-20);cam.transform.LookAt(focus);cam.fieldOfView=64;
                    var rt=new RenderTexture(1600,900,24);rt.Create();var previous=RenderTexture.active;
                    try{RenderPipeline.SubmitRenderRequest(cam,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});RenderPipeline.SubmitRenderRequest(cam,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});RenderTexture.active=rt;var tex=new Texture2D(1600,900,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1600,900),0,0);tex.Apply();File.WriteAllBytes("Documentation/Nexoterra/Habitats/"+i+"_"+(i==6?"Lago_elemental":Names[i])+".png",tex.EncodeToPNG());UnityEngine.Object.DestroyImmediate(tex);}
                    finally{RenderTexture.active=previous;rt.Release();UnityEngine.Object.DestroyImmediate(rt);}
                }
            }
            finally{cam.transform.position=p;cam.transform.rotation=rotation;cam.fieldOfView=fov;}
        }
    }
}
