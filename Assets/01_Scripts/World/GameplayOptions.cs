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
            get=>PlayerPrefs.GetInt(AutoReloadKey,0)==1;
            set{PlayerPrefs.SetInt(AutoReloadKey,value?1:0);PlayerPrefs.Save();}
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
        public static void DrawGraphics(Rect rect)
        {
            if(!initialized)InitializeGraphics();
            float y=rect.y;
            NexoUi.Section(new Rect(rect.x,y,rect.width,22),"PRESET DE CALIDAD");y+=32;
            int current=Mathf.Clamp(PlayerPrefs.GetInt(QualityPresetKey,1),0,presetNames.Length-1);
            int chosen=NexoUi.Segmented(new Rect(rect.x,y,rect.width,46),presetNames,current);if(chosen!=current)SetPreset(chosen);y+=52;
            NexoUi.Write(new Rect(rect.x,y,rect.width,20),presetHints[chosen],13,NexoUi.Muted);y+=36;
            NexoUi.Section(new Rect(rect.x,y,rect.width,22),"LÍMITE DE IMÁGENES POR SEGUNDO");y+=32;
            int fps=Mathf.Clamp(PlayerPrefs.GetInt(FrameRateKey,2),0,frameRates.Length-1);
            int fpsChosen=NexoUi.Segmented(new Rect(rect.x,y,rect.width,40),frameRateNames,fps);if(fpsChosen!=fps)SetFrameRate(fpsChosen);y+=54;
            bool vsync=PlayerPrefs.GetInt(VSyncKey,0)==1;
            bool v=NexoUi.Toggle(new Rect(rect.x,y,rect.width,58),"Sincronización vertical","Evita cortes en la imagen. Limita a la frecuencia del monitor.",vsync);if(v!=vsync)SetVSync(v);
        }
        // Full settings page: title, graphics, gameplay and a back button. Returns true when the player goes back.
        public static bool DrawSettingsPage(Rect rect)
        {
            NexoUi.Title(new Rect(rect.x,rect.y,rect.width,70),"CALIDAD GRÁFICA","Los cambios se aplican y guardan al instante.");
            DrawGraphics(new Rect(rect.x,rect.y+84,rect.width,300));
            float y=rect.y+84+300;
            NexoUi.Section(new Rect(rect.x,y,rect.width,22),"JUEGO");y+=32;
            bool auto=AutoReload,next=NexoUi.Toggle(new Rect(rect.x,y,rect.width,58),"Recarga automática","Carga la babosa seleccionada cuando esté disponible. Desactivada: recarga con R.",auto);
            if(next!=auto)AutoReload=next;
            bool back=NexoHud.Button(new Rect(rect.x,rect.yMax-50,rect.width,50),"VOLVER");
            return back||NexoUi.BackPressed;
        }
    }
}
