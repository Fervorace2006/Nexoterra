using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Nexoterra.World
{
    [DefaultExecutionOrder(-1000)]
    public sealed class NexoMusic : MonoBehaviour
    {
        const string MenuTrack = "Nexoterra/Audio/music_menu";
        const string GameplayTrack = "Nexoterra/Audio/music_gameplay";
        const string LateTrack = "Nexoterra/Audio/music_late";

        static NexoMusic instance;
        AudioSource source;
        AudioClip menuClip, gameplayClip, lateClip;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void CreateForFirstScene()
        {
            if (instance) return;
            new GameObject("Música de fondo").AddComponent<NexoMusic>();
        }

        void Awake()
        {
            if (instance && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0;
            source.volume = GameplayOptions.MusicVolume;
            SceneManager.sceneLoaded += SceneLoaded;
            SetForScene(SceneManager.GetActiveScene());
        }

        void OnDestroy()
        {
            SceneManager.sceneLoaded -= SceneLoaded;
            if (instance == this) instance = null;
        }

        void Update()
        {
            if (source) source.volume = GameplayOptions.MusicVolume;
        }

        void SceneLoaded(Scene scene, LoadSceneMode mode) => SetForScene(scene);

        void SetForScene(Scene scene)
        {
            AudioClip next = TrackForScene(scene.name);
            if (!source || source.clip == next && source.isPlaying) return;

            source.Stop();
            source.clip = next;
            if (next) source.Play();
        }

        AudioClip TrackForScene(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) return null;

            if (sceneName == FrontEndMenu.MenuScene)
                return menuClip ? menuClip : menuClip = Resources.Load<AudioClip>(MenuTrack);

            if (sceneName == "Nexoterra_Arena")
                return lateClip ? lateClip : lateClip = Resources.Load<AudioClip>(LateTrack);

            int level = Array.IndexOf(CampaignLevel.Scenes, sceneName);
            if (level >= CampaignLevel.Scenes.Length - 2)
                return lateClip ? lateClip : lateClip = Resources.Load<AudioClip>(LateTrack);

            return gameplayClip ? gameplayClip : gameplayClip = Resources.Load<AudioClip>(GameplayTrack);
        }
    }
}
