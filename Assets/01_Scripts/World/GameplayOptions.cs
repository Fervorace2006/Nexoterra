using UnityEngine;
namespace Nexoterra.World
{
    public static class GameplayOptions
    {
        const string AutoReloadKey="Nexoterra.AutoReload";
        public static bool AutoReload
        {
            get=>PlayerPrefs.GetInt(AutoReloadKey,0)==1;
            set{PlayerPrefs.SetInt(AutoReloadKey,value?1:0);PlayerPrefs.Save();}
        }
        public static void DrawAutoReload(Rect rect)
        {
            if(NexoHud.Button(rect,"RECARGA AUTOMÁTICA · "+(AutoReload?"ACTIVADA":"DESACTIVADA")))AutoReload=!AutoReload;
        }
    }
}
