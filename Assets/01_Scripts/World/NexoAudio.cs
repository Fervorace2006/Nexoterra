using System.Collections.Generic;
using UnityEngine;
namespace Nexoterra.World
{
 // One persistent 2D audio source for UI and player feedback sounds (Resources/Nexoterra/Audio/*.wav).
 public static class NexoAudio
 {
  static AudioSource source;static readonly Dictionary<string,AudioClip> clips=new Dictionary<string,AudioClip>();
  static readonly Dictionary<string,float> lastPlayed=new Dictionary<string,float>();
  public static void Play(string name,float volume=1f,float pitchJitter=0f)
  {
   if(!source){var go=new GameObject("Sonidos de interfaz");Object.DontDestroyOnLoad(go);source=go.AddComponent<AudioSource>();source.playOnAwake=false;source.spatialBlend=0;source.ignoreListenerPause=true;}
   if(!clips.TryGetValue(name,out var clip)||!clip){clip=Resources.Load<AudioClip>("Nexoterra/Audio/"+name);clips[name]=clip;}
   if(!clip)return;
   // IMGUI runs several passes per frame: never play the same sound twice within 40 ms.
   float now=Time.unscaledTime;if(lastPlayed.TryGetValue(name,out var t)&&now-t<.04f)return;lastPlayed[name]=now;
   source.pitch=1+Random.Range(-pitchJitter,pitchJitter);source.PlayOneShot(clip,volume);
  }
 }
}
