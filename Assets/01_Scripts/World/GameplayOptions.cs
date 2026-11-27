using UnityEngine;
using UnityEngine.Rendering;
using System.Reflection;
namespace Nexoterra.World
{
    public static class GameplayOptions
    {
        const string AutoReloadKey="Nexoterra.AutoReload";
        const string QualityPresetKey="Nexoterra.GraphicsPreset";
        const string FrameRateKey="Nexoterra.FrameRate";
        const string VSyncKey="Nexoterra.VSync";
        static bool initialized;
        static readonly string[] presetNames={"RENDIMIENTO","EQUILIBRADO","CALIDAD"};
        static readonly string[] qualityNames={"Mobile","PC","High","Ultra"};
        static readonly int[] frameRates={30,60,90,120,0};
        static readonly string[] frameRateNames={"30","60","90","120","SIN LÍMITE"};
        public static bool AutoReload
        {
            get=>Application.isMobilePlatform||PlayerPrefs.GetInt(AutoReloadKey,0)==1;
            set{if(Application.isMobilePlatform)value=true;PlayerPrefs.SetInt(AutoReloadKey,value?1:0);PlayerPrefs.Save();}
        }
        public static void DrawAutoReload(Rect rect)
        {
            bool value=NexoUi.Toggle(rect,"Recarga automática",null,AutoReload);if(value!=AutoReload)AutoReload=value;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void InitializeGraphics()
        {
            if(initialized)return;
            initialized=true;
            if(!PlayerPrefs.HasKey(QualityPresetKey))
            {
                int memory=SystemInfo.systemMemorySize;
                int cores=SystemInfo.processorCount;
                int preset=memory<6000||cores<6?0:memory<12000||cores<8?1:2;
                PlayerPrefs.SetInt(QualityPresetKey,preset);
                if(!PlayerPrefs.HasKey(FrameRateKey))PlayerPrefs.SetInt(FrameRateKey,preset==0?1:2);
                PlayerPrefs.Save();
            }
            ApplyGraphics();
        }

        static void ApplyGraphics()
        {
            int preset=Mathf.Clamp(PlayerPrefs.GetInt(QualityPresetKey,1),0,presetNames.Length-1);
            string wanted=preset==0?qualityNames[0]:preset==1?qualityNames[1]:qualityNames[2];
            var names=QualitySettings.names;
            int quality=0;
            for(int i=0;i<names.Length;i++)if(names[i]==wanted){quality=i;break;}
            quality=Mathf.Clamp(quality,0,Mathf.Max(0,names.Length-1));
            QualitySettings.SetQualityLevel(quality,true);
            QualitySettings.vSyncCount=PlayerPrefs.GetInt(VSyncKey,0);
            Application.targetFrameRate=frameRates[Mathf.Clamp(PlayerPrefs.GetInt(FrameRateKey,preset==0?1:2),0,frameRates.Length-1)];
            var pipeline=GraphicsSettings.currentRenderPipeline;
            var renderScale=pipeline?pipeline.GetType().GetProperty("renderScale",BindingFlags.Instance|BindingFlags.Public):null;
            if(renderScale!=null&&renderScale.CanWrite)renderScale.SetValue(pipeline,preset==0?.72f:preset==1?.88f:1f);
        }

        static void SetPreset(int preset)
        {
            PlayerPrefs.SetInt(QualityPresetKey,Mathf.Clamp(preset,0,presetNames.Length-1));
            PlayerPrefs.Save();
            ApplyGraphics();
        }

        static void SetFrameRate(int index)
        {
            PlayerPrefs.SetInt(FrameRateKey,Mathf.Clamp(index,0,frameRates.Length-1));
            PlayerPrefs.Save();
            ApplyGraphics();
        }

        static void SetVSync(bool enabled)
        {
            PlayerPrefs.SetInt(VSyncKey,enabled?1:0);
            PlayerPrefs.Save();
            ApplyGraphics();
        }

        static readonly string[] presetHints={
            "Resolución interna al 72 % y efectos reducidos. Para equipos modestos.",
            "Resolución interna al 88 %. Buen balance entre fluidez y detalle.",
            "Resolución completa y máximo detalle. Recomendado para PCs potentes."};

        // Graphics controls only (preset, frame-rate cap, v-sync). Needs ~300 px of height.
        // ---- Audio volumes (0..1), saved in PlayerPrefs ----
        const string MusicKey="Nexoterra.MusicVolume",SfxKey="Nexoterra.SfxVolume";
        public static float MusicVolume{get=>PlayerPrefs.GetFloat(MusicKey,.7f);set{PlayerPrefs.SetFloat(MusicKey,Mathf.Clamp01(value));}}
        public static float SfxVolume{get=>PlayerPrefs.GetFloat(SfxKey,.4f);set{PlayerPrefs.SetFloat(SfxKey,Mathf.Clamp01(value));}}

        // Graphics controls (preset, frame-rate cap, v-sync). Uses ~232 px of height.
        public static void DrawGraphics(Rect rect)
        {
            if(!initialized)InitializeGraphics();
            float y=rect.y;
            NexoUi.Section(new Rect(rect.x,y,rect.width,22),"PRESET DE CALIDAD");y+=28;
            int current=Mathf.Clamp(PlayerPrefs.GetInt(QualityPresetKey,1),0,presetNames.Length-1);
            int chosen=NexoUi.Segmented(new Rect(rect.x,y,rect.width,40),presetNames,current);if(chosen!=current)SetPreset(chosen);y+=44;
            NexoUi.Write(new Rect(rect.x,y,rect.width,18),presetHints[chosen],12,NexoUi.Muted);y+=26;
            NexoUi.Section(new Rect(rect.x,y,rect.width,22),"LÍMITE DE IMÁGENES POR SEGUNDO");y+=28;
            int fps=Mathf.Clamp(PlayerPrefs.GetInt(FrameRateKey,2),0,frameRates.Length-1);
            int fpsChosen=NexoUi.Segmented(new Rect(rect.x,y,rect.width,36),frameRateNames,fps);if(fpsChosen!=fps)SetFrameRate(fpsChosen);y+=44;
            bool vsync=PlayerPrefs.GetInt(VSyncKey,0)==1;
            bool v=NexoUi.Toggle(new Rect(rect.x,y,rect.width,50),"Sincronización vertical","Evita cortes en la imagen.",vsync);if(v!=vsync)SetVSync(v);
        }
        // Full settings page: title, graphics, audio, gameplay and a back button. Returns true when the player goes back.
        static bool padPage;
        public static bool DrawSettingsPage(Rect rect)
        {
            if(padPage){if(NexoInput.DrawPage(rect)){padPage=false;NexoUi.ResetFocus();}return false;}
            NexoUi.Write(new Rect(rect.x,rect.y,rect.width,18),"NOMBRE EN MULTIJUGADOR",12,NexoUi.Teal,TextAnchor.MiddleLeft,true);
            string playerName=NexoHud.Field(new Rect(rect.x,rect.y+23,rect.width,40),ArenaSession.PlayerName,16);
            if(playerName!=ArenaSession.PlayerName)ArenaSession.PlayerName=playerName;
            float y=rect.y+76;
            DrawGraphics(new Rect(rect.x,y,rect.width,232));y+=232;
            NexoUi.Section(new Rect(rect.x,y,rect.width,22),"AUDIO");y+=30;
            float half=(rect.width-16)/2;
            float m=MusicVolume,mn=NexoUi.Slider(new Rect(rect.x,y,half,44),"Música",m);if(!Mathf.Approximately(m,mn)){MusicVolume=mn;PlayerPrefs.Save();}
            float f=SfxVolume,fn=NexoUi.Slider(new Rect(rect.x+half+16,y,half,44),"Efectos",f);if(!Mathf.Approximately(f,fn)){SfxVolume=fn;PlayerPrefs.Save();}
            y+=54;
            if(Application.isMobilePlatform)
                NexoUi.Write(new Rect(rect.x,y,rect.width,50),"RECARGA AUTOMÁTICA · ACTIVA EN MÓVIL",13,NexoUi.Teal,TextAnchor.MiddleLeft,true);
            else
            {
                bool auto=AutoReload,next=NexoUi.Toggle(new Rect(rect.x,y,rect.width,50),"Recarga automática","Carga la babosa seleccionada cuando esté disponible.",auto);
                if(next!=auto)AutoReload=next;
            }
            float bw=(rect.width-16)/2;
            if(NexoUi.Button(new Rect(rect.x,rect.yMax-50,bw,50),"MANDO")){padPage=true;NexoUi.ResetFocus();}
            bool back=NexoHud.Button(new Rect(rect.x+bw+16,rect.yMax-50,bw,50),"VOLVER");
            return back||NexoUi.BackPressed;
        }
    }
}
