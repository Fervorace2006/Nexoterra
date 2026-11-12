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
        public bool IsElemental=>SlugCatalog.BaseSpecies(id).StartsWith("Elemental_");
        public SlugCombatProfile combat;
        public bool combatConfigured;
        public bool usePrefabCombat=true;
        public SlugCombatProfile Combat {get {var stats=usePrefabCombat&&normal?normal.GetComponent<SlugPrefabStats>():null;var profile=stats&&stats.combat!=null?stats.combat:combatConfigured&&combat!=null?combat:SlugCombatProfile.Default(id);return id.StartsWith("Malvada_")?profile.WithDamageMultiplier(1.05f):profile;}}
    }

    [CreateAssetMenu(menuName="Nexoterra/Catalogo de babosas")]
    public sealed class SlugCatalog : ScriptableObject
    {
        public SlugDefinition[] species=Array.Empty<SlugDefinition>();
        public static string BaseSpecies(string id)=>Canonical(id.Replace("Malvada_",""));
        public static string Canonical(string id)=>id=="Burpy"?"Infierno":id=="Malvada_Elemental_Energia"?"Elemental_Energia":id;
        public SlugDefinition Find(string id)=>Array.Find(species,s=>s.id==Canonical(id));
    }
}
