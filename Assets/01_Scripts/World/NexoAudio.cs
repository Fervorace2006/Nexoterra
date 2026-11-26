using System.Collections.Generic;
using UnityEngine;
namespace Nexoterra.World
{
 // One persistent 2D audio source for UI and player feedback sounds (Resources/Nexoterra/Audio/*.wav).
 public static class NexoAudio
 {
  static AudioSource source;static readonly Dictionary<string,AudioClip> clips=new Dictionary<string,AudioClip>();
  static readonly Dictionary<string,float> lastPlayed=new Dictionary<string,float>();
  // 3D one-shot at a world position (enemy launches), scaled by the effects volume.
  public static void PlayAt(string name,Vector3 position,float volume=1f,float minDistance=4f,float maxDistance=45f,float spatialBlend=.85f)
  {
   if(!clips.TryGetValue(name,out var clip)||!clip){clip=Resources.Load<AudioClip>("Nexoterra/Audio/"+name);clips[name]=clip;}
   if(!clip)return;
   var go=new GameObject("Sonido "+name);go.transform.position=position;var s=go.AddComponent<AudioSource>();
   s.clip=clip;s.spatialBlend=Mathf.Clamp01(spatialBlend);s.minDistance=Mathf.Max(.01f,minDistance);s.maxDistance=Mathf.Max(s.minDistance,maxDistance);s.rolloffMode=AudioRolloffMode.Linear;s.volume=volume*GameplayOptions.SfxVolume;s.pitch=1+Random.Range(-.05f,.05f);s.Play();
   Object.Destroy(go,clip.length+.2f);
  }
  public static void Play(string name,float volume=1f,float pitchJitter=0f)
  {
   if(!source){var go=new GameObject("Sonidos de interfaz");Object.DontDestroyOnLoad(go);source=go.AddComponent<AudioSource>();source.playOnAwake=false;source.spatialBlend=0;source.ignoreListenerPause=true;}
   if(!clips.TryGetValue(name,out var clip)||!clip){clip=Resources.Load<AudioClip>("Nexoterra/Audio/"+name);clips[name]=clip;}
   if(!clip)return;
   // IMGUI runs several passes per frame: never play the same sound twice within 40 ms.
   float now=Time.unscaledTime;if(lastPlayed.TryGetValue(name,out var t)&&now-t<.04f)return;lastPlayed[name]=now;
   source.pitch=1+Random.Range(-pitchJitter,pitchJitter);source.PlayOneShot(clip,volume*GameplayOptions.SfxVolume);
  }
 }
}
