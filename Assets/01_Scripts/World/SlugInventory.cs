using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Nexoterra.World
{
    public sealed class SlugInventory : MonoBehaviour
    {
        public SlugCatalog catalog;
        public float transformationDistance=8,projectileSpeed=24;
        public bool persist=true;
        public List<string> owned=new List<string>();
        public int[] equipped={-1,-1,-1,-1,-1,-1};
        public int selected;
        public bool IsOpen { get; private set; }
        public bool PointerOverBar
        {
            get
            {
                var mouse=Mouse.current;if(mouse==null)return false;
                float scale=Mathf.Min(Screen.width/1280f,Screen.height/720f);
                Vector2 p=mouse.position.ReadValue();p.y=Screen.height-p.y;p-=new Vector2((Screen.width-1280*scale)*.5f,(Screen.height-720*scale)*.5f);p/=scale;
                return new Rect(380,588,524,116).Contains(p)||new Rect(1070,654,190,48).Contains(p);
            }
        }
        [Min(.5f)] public float docHealingRadius=6;
        public int darkWater;
        public int Count=>owned.Count;
        readonly Dictionary<string,SlugProjectile> travelling=new Dictionary<string,SlugProjectile>();
        public bool IsBusy(string species)=>(travelling.TryGetValue(species,out var slug)&&slug)||(GetComponent<CorruptionCompanion>()&&GetComponent<CorruptionCompanion>().IsLoose(species));
        public SlugProjectile Travelling(string species)=>travelling.TryGetValue(species,out var slug)?slug:null;
        public void Recover(string species,SlugProjectile slug)
        {
            if(travelling.TryGetValue(species,out var current)&&current==slug)
            {travelling.Remove(species);Status=(catalog.Find(species)?.displayName??species)+" ha regresado. Lista para lanzar.";}
        }
        public void ReleaseLost(string species,SlugProjectile slug)
        {
            // Scene unloading or external destruction must not leave a stale species lock.
            if(travelling.TryGetValue(species,out var current)&&current==slug)travelling.Remove(species);
        }
        public string Status { get; private set; }="Alimenta una babosa con F para incorporarla.";
        NexoterraThirdPerson player;
        Vector2 scroll;bool onlyElementals;int previewIndex=-1;
        GUIStyle text,small,heading,center;
        static readonly Color Ink=new Color(.025f,.065f,.085f,.97f),Teal=new Color(.02f,.35f,.40f),Gold=new Color(1,.62f,.13f);
        [Serializable] sealed class SaveData { public int version=1; public List<string> owned; public int[] equipped; public int selected; }
        string SavePath=>Path.Combine(Application.persistentDataPath,"nexoterra-slugs-v1.json");
        void Awake(){player=GetComponent<NexoterraThirdPerson>();if(persist&&!GetComponent<GameSession>())Load();}
        public void SetOpen(bool value)
        {
            IsOpen=value;
            Cursor.lockState=value?CursorLockMode.None:CursorLockMode.Locked;Cursor.visible=value;
        }
        void Update()
        {
            if(GameSession.Blocked)return;
            var k=Keyboard.current;var m=Mouse.current;if(k==null)return;
            if(k.bKey.wasPressedThisFrame||k.iKey.wasPressedThisFrame)SetOpen(!IsOpen);
            if(!GameSession.Current&&k.escapeKey.wasPressedThisFrame&&IsOpen){SetOpen(false);Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}
            var keys=new[]{k.digit1Key,k.digit2Key,k.digit3Key,k.digit4Key,k.digit5Key,k.digit6Key};
            for(int i=0;i<6;i++)if(keys[i].wasPressedThisFrame)selected=i;
            if(!IsOpen&&m!=null&&Mathf.Abs(m.scroll.ReadValue().y)>.01f)selected=(selected+(m.scroll.ReadValue().y>0?5:1))%6;
        }
        public bool HasSpecies(string species)=>owned.Exists(id=>SlugCatalog.BaseSpecies(id)==SlugCatalog.BaseSpecies(species));
        public bool Add(string species)
        {
            species=SlugCatalog.Canonical(species);
            var definition=catalog?catalog.Find(species):null;
            if(definition!=null&&definition.unique&&HasSpecies(species)){Status="Ya domesticaste esta criatura única.";return false;}
            if(catalog==null||catalog.Find(species)==null){Status="Esta especie aún no está registrada.";return false;}
            owned.Add(species);int index=owned.Count-1;
            int empty=Array.IndexOf(equipped,-1);if(empty>=0){equipped[empty]=index;selected=empty;}
            Status=catalog.Find(species).displayName+(empty>=0?" equipada.":" guardada en la mochila.");return true;
        }
        public bool Equip(int ownedIndex,int slot)
        {
            if(ownedIndex<0||ownedIndex>=owned.Count||slot<0||slot>=6)return false;
            int previous=Array.IndexOf(equipped,ownedIndex),displaced=equipped[slot];
            if(previous>=0)equipped[previous]=displaced;
            equipped[slot]=ownedIndex;selected=slot;Status="Equipamiento actualizado.";return true;
        }
        public SlugDefinition SelectedDefinition()
        {
            if(equipped==null||equipped.Length!=6||selected<0||selected>=6)return null;
            int index=equipped[selected];return index>=0&&index<owned.Count?catalog.Find(owned[index]):null;
        }
        public SlugProjectile Fire(Transform muzzle,Vector3 direction)
        {
            if(GameSession.Blocked||IsOpen||!muzzle)return null;
            var definition=SelectedDefinition();
            if(definition==null){Status="Selecciona una babosa equipada (1–6).";return null;}
            if(IsBusy(definition.id)){Status=definition.displayName+" todavía está fuera. Espera a que regrese.";return null;}
            if(!definition.normal||!definition.transformed){Status="Falta el modelo de esta especie.";return null;}
            var go=new GameObject("Disparo · "+definition.id);go.layer=2;go.transform.position=muzzle.position;
            var shot=go.AddComponent<SlugProjectile>();shot.speed=projectileSpeed;shot.transformDistance=transformationDistance;shot.docHealingRadius=docHealingRadius;
            if(definition.id.StartsWith("Malvada_")){var health=GetComponent<PlayerHealth>();if(health)health.CorruptionCost();}
            travelling[definition.id]=shot;shot.Launch(definition,direction,this);if(ArenaSession.Current)ArenaSession.Current.SubmitShot(definition,muzzle.position,direction,shot);var abilities=GetComponent<PlayerAbilities>();if(abilities)abilities.OnSlugLaunched(definition.id);return shot;
        }
        public void Save()
        {
            if(GameSession.Current){GameSession.Current.SaveGame();return;}
            if(!persist)return;
            try{File.WriteAllText(SavePath,JsonUtility.ToJson(new SaveData{owned=owned,equipped=equipped,selected=selected},true));}
            catch(Exception e){Debug.LogWarning("No se pudo guardar la mochila: "+e.Message);}
        }
        void Load()
        {
            if(!File.Exists(SavePath)||catalog==null)return;
            try
            {
                var data=JsonUtility.FromJson<SaveData>(File.ReadAllText(SavePath));
                if(data==null||data.version!=1||data.owned==null)return;
                owned=new List<string>();equipped=new[]{-1,-1,-1,-1,-1,-1};
                var remap=new Dictionary<int,int>();var uniques=new Dictionary<string,int>();
                for(int i=0;i<data.owned.Count;i++)
                {
                    string id=SlugCatalog.Canonical(data.owned[i]);var definition=catalog.Find(id);
                    if(definition!=null&&definition.unique&&uniques.TryGetValue(id,out int existing)){remap[i]=existing;continue;}
                    remap[i]=owned.Count;owned.Add(id);if(definition!=null&&definition.unique)uniques[id]=owned.Count-1;
                }
                var seen=new HashSet<int>();
                if(data.equipped!=null)for(int i=0;i<Mathf.Min(6,data.equipped.Length);i++)
                {if(remap.TryGetValue(data.equipped[i],out int index)&&catalog.Find(owned[index])!=null&&seen.Add(index))equipped[i]=index;}
                selected=Mathf.Clamp(data.selected,0,5);
            }
            catch(Exception e){Debug.LogWarning("No se pudo cargar la mochila: "+e.Message);}
        }
        
        static void Fill(Rect r,Color color){GUI.color=color;GUI.DrawTexture(r,Texture2D.whiteTexture);GUI.color=Color.white;}
        static void Frame(Rect r,Color color,float thickness=2)
        {Fill(new Rect(r.x,r.y,r.width,thickness),color);Fill(new Rect(r.x,r.yMax-thickness,r.width,thickness),color);Fill(new Rect(r.x,r.y,thickness,r.height),color);Fill(new Rect(r.xMax-thickness,r.y,thickness,r.height),color);}
        void Styles()
        {
            if(text!=null)return;
            text=new GUIStyle(GUI.skin.label){fontSize=16};text.normal.textColor=new Color(.85f,.96f,1);
            small=new GUIStyle(text){fontSize=12};heading=new GUIStyle(text){fontSize=26,fontStyle=FontStyle.Bold};
            center=new GUIStyle(small){alignment=TextAnchor.MiddleCenter,wordWrap=true};
        }
        void Slot(Rect r,SlugDefinition definition,bool active,string badge,string name)
        {
            var accent=NexoUi.Accent(definition);NexoUi.Card(r,active?Gold:accent,active);bool busy=definition!=null&&IsBusy(definition.id);
            var picture=new Rect(r.x+5,r.y+9,r.width-10,r.height-37);NexoUi.Portrait(picture,definition?.portrait,accent,busy?.45f:1);
            if(definition==null)NexoHud.Label(picture,"+",24,NexoUi.Muted,TextAnchor.MiddleCenter);
            NexoHud.Fill(new Rect(r.x+5,r.y+5,19,18),active?Gold:new Color(.06f,.13f,.17f));NexoHud.Label(new Rect(r.x+5,r.y+4,19,19),badge,12,active?Ink:NexoUi.Text,TextAnchor.MiddleCenter);
            if(definition!=null&&definition.IsElemental)NexoHud.Label(new Rect(r.xMax-25,r.y+4,20,20),"E",12,accent,TextAnchor.MiddleCenter);
            if(busy)DrawReturnRing(r,definition.id);
            NexoHud.Fill(new Rect(r.x+4,r.yMax-27,r.width-8,23),new Color(.02f,.04f,.055f,.9f));NexoHud.Label(new Rect(r.x+4,r.yMax-27,r.width-8,23),name,12,definition!=null?NexoUi.Text:NexoUi.Muted,TextAnchor.MiddleCenter);
            if(active)NexoHud.Fill(new Rect(r.center.x-12,r.yMax-2,24,2),Gold);
        }
        void DrawReturnRing(Rect r,string species)
        {
            var slug=Travelling(species);float radius=Mathf.Min(r.width-16,r.height-45)*.5f+1;
            Vector2 centerPoint=new Vector2(r.center.x,r.y+12+(r.height-45)*.5f);
            float progress=slug?(slug.IsReturning?slug.ReturnProgress:.18f):1;
            float rotation=slug&&!slug.IsReturning?Time.unscaledTime*120:-90;
            Color bright=slug?(slug.IsReturning?new Color(.1f,.9f,1):Gold):new Color(.1f,.65f,.55f,.65f);
            for(int i=0;i<64;i++)
            {
                float a=(rotation+i*360f/64)*Mathf.Deg2Rad,b=(rotation+(i+1)*360f/64)*Mathf.Deg2Rad;
                Vector2 from=centerPoint+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius,to=centerPoint+new Vector2(Mathf.Cos(b),Mathf.Sin(b))*radius;
                var previous=GUI.matrix;GUI.matrix=previous*Matrix4x4.TRS(new Vector3(from.x,from.y,0),Quaternion.Euler(0,0,Mathf.Atan2(to.y-from.y,to.x-from.x)*Mathf.Rad2Deg),Vector3.one);
                Fill(new Rect(0,-1.5f,Vector2.Distance(from,to)+1,3),i/64f<progress?bright:new Color(.12f,.22f,.25f));GUI.matrix=previous;
            }
        }
        void OnGUI()
        {
            if(GameSession.Blocked)return;Styles();Matrix4x4 before=GUI.matrix;NexoHud.Scale();
            if(IsOpen)DrawBackpack();
            if(!IsOpen)DrawTray();
            GUI.matrix=before;
        }
        // Bottom-right blaster tray (6 capsule slots + charge gauge) and bottom-left info tiles.
        void DrawTray()
        {
            var inner=NexoHud.Plate(new Rect(790,592,474,114),false);
            var anim=GetComponent<NativePlayerAnimation>();
            for(int i=0;i<6;i++)
            {
                int index=equipped[i];var def=index>=0&&index<owned.Count?catalog.Find(owned[index]):null;
                var r=new Rect(inner.x+4+i*70,inner.y+4,64,inner.height-8);
                bool active=i==selected;var accent=NexoUi.Accent(def);bool busy=def!=null&&IsBusy(def.id);
                NexoHud.Fill(r,active?NexoHud.Orange:new Color(.02f,.04f,.05f));
                NexoHud.Gradient(new Rect(r.x+2,r.y+2,r.width-4,r.height-4),new Color(.07f,.42f,.48f),new Color(.01f,.11f,.14f));
                for(float y=r.y+8;y<r.yMax-6;y+=6)NexoHud.Fill(new Rect(r.x+4,y,r.width-8,1),new Color(.5f,1,1,.05f));
                if(def!=null)NexoUi.Portrait(new Rect(r.x+4,r.y+8,r.width-8,r.height-30),def.portrait,accent,busy?.4f:1);
                else NexoHud.Label(new Rect(r.x,r.y,r.width,r.height-20),"+",22,new Color(.4f,.6f,.65f),TextAnchor.MiddleCenter);
                if(busy)DrawReturnRing(r,def.id);
                // Number badge as a round socket at the bottom, like a chamber.
                var badge=new Rect(r.center.x-10,r.yMax-22,20,18);NexoHud.Fill(badge,active?NexoHud.Orange:new Color(.1f,.16f,.19f));NexoHud.Fill(new Rect(badge.x+2,badge.y+2,badge.width-4,badge.height-4),active?new Color(1,.75f,.3f):new Color(.04f,.08f,.1f));
                NexoHud.Label(badge,(i+1).ToString(),11,active?Ink:NexoUi.Text,TextAnchor.MiddleCenter);
                if(def!=null&&def.IsElemental)NexoHud.Label(new Rect(r.xMax-16,r.y+3,14,14),"E",10,accent,TextAnchor.MiddleCenter);
                if(active)NexoHud.Brackets(r,NexoHud.Orange,10,2);
                if(Cursor.lockState!=CursorLockMode.Locked&&GUI.Button(r,GUIContent.none,GUIStyle.none))selected=i;
            }
            // Charge gauge: full when the selected slug is loaded, fills during reload.
            var definition=SelectedDefinition();var current=definition==null?null:Travelling(definition.id);
            var gauge=new Rect(inner.xMax-38,inner.y+4,34,inner.height-8);NexoHud.Fill(gauge,new Color(.01f,.02f,.03f));
            float charge=anim==null?1:anim.IsLoaded?1:anim.IsReloading?.5f+.5f*Mathf.PingPong(Time.time*2,1):0;
            int cells=10;for(int c=0;c<cells;c++){var cell=new Rect(gauge.x+5,gauge.yMax-5-(c+1)*(gauge.height-10)/cells,gauge.width-10,(gauge.height-10)/cells-2);NexoHud.Fill(cell,(c+.5f)/cells<=charge?new Color(.2f,.85f,1):new Color(.05f,.1f,.12f));}
            string state=definition==null?"ESPACIO "+(selected+1)+" VACÍO":current?(current.IsReturning?"REGRESANDO":"EN VUELO"):definition.displayName.ToUpperInvariant()+(anim!=null&&!anim.IsLoaded?" · R RECARGAR":" · LISTA");
            var caption=NexoHud.Plate(new Rect(790,562,474,28),false,new Color(.7f,.35f,.08f));
            NexoHud.Label(caption,state,12,current?Gold:Color.white,TextAnchor.MiddleCenter);
            // Info tiles (bottom-left).
            Tile(new Rect(16,616,84,90),(player?player.food:0).ToString(),"HONGOS",new Color(.85f,.55f,1f),false);
            if(Tile(new Rect(106,616,84,90),owned.Count.ToString(),"MOCHILA [B]",Gold,IsOpen))SetOpen(!IsOpen);
            var abilities=GetComponent<PlayerAbilities>();float cd=abilities?abilities.DodgeCooldown:0;
            Tile(new Rect(196,616,84,90),cd>0?cd.ToString("0.0"):"OK","ESQUIVA",new Color(.3f,.85f,1f),false);
            if(abilities&&abilities.BoostRemaining>0)Tile(new Rect(286,616,84,90),Mathf.CeilToInt(abilities.BoostRemaining).ToString(),"VIENTO",new Color(.85f,.9f,.95f),true);
            if(owned.Count==0)NexoHud.Shadowed(new Rect(790,536,474,22),"Alimenta babosas con F para formar tu equipo",12,NexoUi.Muted,TextAnchor.MiddleCenter);
            else NexoHud.Shadowed(new Rect(790,536,474,22),"1–6 / RUEDA  seleccionar     CLIC  lanzar",11,new Color(.75f,.85f,.88f),TextAnchor.MiddleCenter);
        }
        bool Tile(Rect r,string value,string caption,Color accent,bool active)
        {
            var inner=NexoHud.Plate(r,true,active?NexoHud.Orange:new Color(.75f,.4f,.08f));
            NexoHud.Shadowed(new Rect(inner.x,inner.y+4,inner.width,34),value,24,accent,TextAnchor.MiddleCenter);
            NexoHud.Fill(new Rect(inner.x+8,inner.y+44,inner.width-16,1),accent*new Color(1,1,1,.5f));
            NexoHud.Label(new Rect(inner.x,inner.y+48,inner.width,26),caption,10,NexoUi.Text,TextAnchor.MiddleCenter);
            return GUI.Button(r,GUIContent.none,GUIStyle.none)&&Cursor.lockState!=CursorLockMode.Locked;
        }
        void DrawBackpack()
        {
            float scale=Mathf.Min(Screen.width/1280f,Screen.height/720f);
            Fill(new Rect(-(Screen.width/scale-1280)*.5f,-(Screen.height/scale-720)*.5f,Screen.width/scale,Screen.height/scale),new Color(.005f,.012f,.02f,.93f));
            NexoUi.Card(new Rect(170,34,940,510),Teal);
            NexoHud.Label(new Rect(194,53,640,38),"COLECCIÓN DE BABOSAS",27,NexoUi.Text);
            NexoHud.Label(new Rect(194,92,690,22),owned.Count+" en tu mochila  /  6 espacios de equipo",14,NexoUi.Muted);
            if(NexoUi.Button(new Rect(1014,54,72,40),"CERRAR"))SetOpen(false);
            if(NexoUi.Button(new Rect(194,128,108,34),"TODAS",!onlyElementals)){onlyElementals=false;scroll=Vector2.zero;}
            if(NexoUi.Button(new Rect(312,128,148,34),"ELEMENTALES",onlyElementals)){onlyElementals=true;scroll=Vector2.zero;}
            var visible=new List<int>();for(int i=0;i<owned.Count;i++)if(!onlyElementals||(catalog.Find(owned[i])?.IsElemental??false))visible.Add(i);
            if(!visible.Contains(previewIndex))previewIndex=visible.Count>0?visible[0]:-1;
            if(visible.Count==0)NexoHud.Label(new Rect(218,236,490,105),owned.Count==0?"TU COLECCIÓN EMPIEZA AQUÍ\nRecoge hongos con E y alimenta babosas con F.":"AÚN NO TIENES ELEMENTALES\nExplora los lugares especiales de cada bioma.",18,NexoUi.Muted,TextAnchor.MiddleCenter);
            scroll=GUI.BeginScrollView(new Rect(192,178,588,300),scroll,new Rect(0,0,564,Mathf.Max(298,Mathf.Ceil(visible.Count/5f)*144)));
            for(int k=0;k<visible.Count;k++)
            {
                int i=visible[k];var d=catalog.Find(owned[i]);int slot=Array.IndexOf(equipped,i);var r=new Rect(k%5*112,k/5*144,104,132);
                Slot(r,d,slot==selected&&slot>=0,slot>=0?(slot+1).ToString():"·",d?.displayName??"No disponible");
                if(r.Contains(Event.current.mousePosition))previewIndex=i;
                if(GUI.Button(r,GUIContent.none,GUIStyle.none)&&d!=null){Equip(i,selected);previewIndex=i;}
            }
            GUI.EndScrollView();
            var focus=previewIndex>=0&&previewIndex<owned.Count?catalog.Find(owned[previewIndex]):SelectedDefinition();
            var accent=NexoUi.Accent(focus);NexoUi.Card(new Rect(798,128,288,350),accent);
            NexoHud.Label(new Rect(814,140,256,24),focus?.IsElemental==true?"ELEMENTAL · ÚNICA":focus?.unique==true?"CRIATURA · ÚNICA":"FICHA DE CRIATURA",12,accent,TextAnchor.MiddleCenter);
            NexoUi.Portrait(new Rect(838,168,208,172),focus?.portrait,accent);
            NexoHud.Label(new Rect(814,340,256,29),focus?.displayName??"Tu próxima compañera",21,NexoUi.Text,TextAnchor.MiddleCenter);
            NexoHud.Label(new Rect(820,379,246,78),NexoUi.Ability(focus),14,NexoUi.Muted,TextAnchor.UpperCenter);
            NexoHud.Label(new Rect(194,490,560,38),"Espacio "+(selected+1)+" seleccionado. Pulsa una criatura para equiparla.",14,NexoUi.Text);
            GUI.enabled=equipped[selected]>=0;if(NexoUi.Button(new Rect(798,491,288,34),"RETIRAR DEL ESPACIO "+(selected+1))){equipped[selected]=-1;Status="Babosa guardada en la mochila.";}GUI.enabled=true;
        }
    }
}
