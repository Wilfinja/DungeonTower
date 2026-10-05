using System;
using UnityEngine;

namespace DungeonTower.Items
{
    [Serializable]
    public sealed class AbilityVisuals
    {
        [Header("Attacker motion")]
        [Tooltip("Pull back slowly, then snap toward the target.")]
        public bool Lunge = true;
        [Min(0f), Tooltip("Seconds for the slow pull-back.")]
        public float WindUpSeconds = 0.30f;

        [Header("Melee (optional) - author the prefab facing RIGHT (+X); it is rotated attacker -> target")]
        [Tooltip("A slash, pierce, or swing that appears as the attacker snaps forward.")]
        public GameObject MeleeEffectPrefab;
        [Min(0f), Tooltip("Distance from the attacker's center, in tiles. ~0.6 sits between adjacent tiles; raise it for spears and other reach weapons.")]
        public float MeleeEffectOffsetTiles = 0.6f;

        [Header("In flight (optional)")]
        public GameObject ProjectilePrefab;
        [Min(0.1f)] public float ProjectileTilesPerSecond = 14f;

        [Header("On impact (all optional)")]
        [Tooltip("Plays on every unit (or object) the ability hits.")]
        public GameObject ImpactEffectPrefab;
        [Tooltip("Rotate the impact effect to point from the attacker through the target (author it facing RIGHT).")]
        public bool OrientImpactToAttack;
        [Tooltip("Explosion-style effect for area abilities.")]
        public GameObject AreaEffectPrefab;
        [Tooltip("Off = once at the aimed tile. On = on every tile of the footprint (good for lines/cones).")]
        public bool AreaEffectOnEveryTile;
        [Min(0.1f), Tooltip("Spawned effects are destroyed after this many seconds.")]
        public float EffectLifetime = 2f;
        [Min(0f), Tooltip("Pause after impact so the effect is seen before the turn moves on.")]
        public float ImpactHoldSeconds = 0.35f;

        [Header("Hit feedback (all optional) - the 'crunch'")]
        [Tooltip("One is picked at random per use, with a little pitch variation. Leave empty for silence.")]
        public AudioClip[] HitSounds;
        [Range(0f, 1f)] public float HitVolume = 0.8f;
        [Min(0f), Tooltip("Freeze on impact, in seconds. Crits and kills get double. ~0.05 light, ~0.1 heavy, 0 = off.")]
        public float HitStopSeconds = 0.06f;
        [Min(0f), Tooltip("Camera shake strength in tiles. ~0.05 light, ~0.15 heavy, 0 = off.")]
        public float ShakeStrengthTiles = 0.06f;
        [Min(0.05f), Tooltip("How long the shake lasts, in seconds.")]
        public float ShakeSeconds = 0.18f;
        [Min(0f), Tooltip("How far a hit unit is knocked back before springing home, in tiles.")]
        public float RecoilTiles = 0.2f;

        [Header("Lingering (Summon abilities)")]
        [Tooltip("Shown on every tile of a summoned object until it expires. Hidden traps stay hidden.")]
        public GameObject HazardEffectPrefab;
    }
}
