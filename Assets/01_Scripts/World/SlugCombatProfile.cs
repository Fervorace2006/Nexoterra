using System;
using UnityEngine;
namespace Nexoterra.World
{
 [Serializable]
 public sealed class SlugCombatProfile
 {
  [Min(0)] public float impactDamage=16;
  [Min(0)] public float burnDamagePerSecond,burnDuration;
  [Range(.05f,1)] public float slowMultiplier=1;
  [Min(0)] public float slowDuration,knockdownDuration,knockbackSpeed,stunDuration;
  public static SlugCombatProfile Default(string id)
  {
   var p=new SlugCombatProfile();
   switch(SlugCatalog.Canonical(id))
   {
    case "Infierno":p.impactDamage=22;p.burnDamagePerSecond=5;p.burnDuration=4;break;
    case "Elemental_Fuego":p.impactDamage=32;p.burnDamagePerSecond=7;p.burnDuration=5;break;
    case "Elemental_Agua":p.impactDamage=20;p.slowMultiplier=.55f;p.slowDuration=4;break;
    case "ice":p.impactDamage=18;p.slowMultiplier=1;p.slowDuration=0;break;
    case "Carnero":p.impactDamage=28;p.knockdownDuration=2.2f;p.knockbackSpeed=5;break;
    case "Armashelt":p.impactDamage=25;p.knockdownDuration=1.8f;p.knockbackSpeed=4;break;
    case "Elemental_Tierra":p.impactDamage=30;p.knockdownDuration=1.5f;p.knockbackSpeed=3;break;
    case "Elemental_Aire":p.impactDamage=20;p.knockbackSpeed=8;break;
    case "tazerling":p.impactDamage=18;p.stunDuration=0;break;
    case "Arana":p.impactDamage=12;p.slowMultiplier=1;p.slowDuration=0;break;
    case "Gon_Doc":p.impactDamage=4;break;
    case "Elemental_Energia":p.impactDamage=6;break;
    case "Tornado":p.impactDamage=16;p.knockbackSpeed=6;break;
   }
   return p;
  }
 }
}
