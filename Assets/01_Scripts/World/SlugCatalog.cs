using System;
using UnityEngine;

namespace Nexoterra.World
{
    [Serializable]
    public sealed class SlugDefinition
    {
        public string id, displayName;
        public GameObject normal, transformed;
        public Texture2D portrait;
        public Color accent=Color.cyan;
        public bool unique;
        public SlugCombatProfile combat;
        public bool combatConfigured;
        public SlugCombatProfile Combat=>combatConfigured&&combat!=null?combat:SlugCombatProfile.Default(id);
    }

    [CreateAssetMenu(menuName="Nexoterra/Catalogo de babosas")]
    public sealed class SlugCatalog : ScriptableObject
    {
        public SlugDefinition[] species=Array.Empty<SlugDefinition>();
        public static string Canonical(string id)=>id=="Burpy"?"Infierno":id;
        public SlugDefinition Find(string id)=>Array.Find(species,s=>s.id==Canonical(id));
    }
}
