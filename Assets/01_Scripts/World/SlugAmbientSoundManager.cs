using System.Collections.Generic;
using UnityEngine;

namespace Nexoterra.World
{
    [DefaultExecutionOrder(-950)]
    public sealed class SlugAmbientSoundManager : MonoBehaviour
    {
        const float AudibleRadius = 9f;
        static readonly HashSet<Transform> Sources = new HashSet<Transform>();
        static SlugAmbientSoundManager instance;

        readonly List<Transform> nearby = new List<Transform>();
        NexoterraThirdPerson player;
        float nextAmbientAt;
        int lastClip;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Bootstrap()
        {
            if (!instance) new GameObject("Voces de babosas").AddComponent<SlugAmbientSoundManager>();
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
            nextAmbientAt = Time.unscaledTime + Random.Range(5f, 9f);
        }

        void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        public static void Register(Transform slug)
        {
            if (slug) Sources.Add(slug);
        }

        public static void Unregister(Transform slug)
        {
            if (slug) Sources.Remove(slug);
        }

        public static void PlayOnRelease(Transform slug)
        {
            if (!slug || !instance) return;
            instance.PlayRandomVoice(slug.position);
            instance.nextAmbientAt = Time.unscaledTime + Random.Range(5f, 9f);
        }

        void Update()
        {
            if (Time.unscaledTime < nextAmbientAt) return;
            nextAmbientAt = Time.unscaledTime + Random.Range(7f, 14f);
            if (GameSession.Blocked) return;

            if (!player) player = FindAnyObjectByType<NexoterraThirdPerson>();
            if (!player) return;

            nearby.Clear();
            foreach (var source in Sources)
            {
                if (!source || !source.gameObject.activeInHierarchy) continue;
                if ((source.position - player.transform.position).sqrMagnitude <= AudibleRadius * AudibleRadius)
                    nearby.Add(source);
            }

            if (nearby.Count > 0)
                PlayRandomVoice(nearby[Random.Range(0, nearby.Count)].position);
        }

        void PlayRandomVoice(Vector3 position)
        {
            int clip = Random.Range(1, 12);
            if (clip == lastClip) clip = clip % 11 + 1;
            lastClip = clip;
            NexoAudio.PlayAt("SlugVoices/" + clip, position, .28f, 1.2f, AudibleRadius, 1f);
        }
    }
}
