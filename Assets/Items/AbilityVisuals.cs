using System;
using UnityEngine;

namespace DungeonTower.Items
{
    /// <summary>How the caster winds up before the ability lands.</summary>
    public enum WindUpStyle
    {
        [InspectorName("Default (as before)")] Default,   // follows the old hidden Lunge tick-box
        None,            // fires instantly
        Lunge,           // slow pull-back, snap forward (the old behaviour)
        Gather,          // plays the gather effect in place, no lunge (healers, buffs)
        [InspectorName("Gather + Lunge")] GatherAndLunge   // gather, brief coil, snap forward (fireball)
    }

    [Serializable]
    public sealed class AbilityVisuals
    {
        [Header("Attacker motion")]
        [Tooltip("Default keeps whatever the old Lunge tick-box said, so existing abilities don't change.")]
        public WindUpStyle WindUp = WindUpStyle.Default;
        [HideInInspector] public bool Lunge = true;   // legacy; only read while WindUp is Default
        [Min(0f), Tooltip("Lunge: seconds for the slow pull-back. Gather with no particle system on the prefab: how long to wait.")]
        public float WindUpSeconds = 0.30f;

        [Header("Gather (optional) - plays on the caster while charging")]
        [Tooltip("The charge lasts as long as the longest particle system's Duration (plus Start Delay). When it ends, emission stops and leftover particles fade out while the ability fires.")]
        public GameObject GatherEffectPrefab;
        [Tooltip("Tiles from the caster toward the target to spawn it. 0 = on the caster; ~0.6 puts a fireball orb in front of them.")]
        public float GatherOffsetTiles = 0f;

        [Header("Melee (optional) - author the prefab facing RIGHT (+X); it is rotated attacker -> target")]
        [Tooltip("A slash, pierce, or swing that appears as the attacker snaps forward.")]
        public GameObject MeleeEffectPrefab;
        [Min(0f), Tooltip("Distance from the attacker's center, in tiles. ~0.6 sits between adjacent tiles; raise it for spears and other reach weapons.")]
        public float MeleeEffectOffsetTiles = 0.6f;

        [Header("In flight (optional)")]
        public GameObject ProjectilePrefab;
        [Min(0.1f)] public float ProjectileTilesPerSecond = 14f;
        [Min(0f), Tooltip("Seconds the projectile's trail/particles keep fading after it lands. 0 = vanish instantly.")]
        public float ProjectileFadeSeconds = 1f;

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

        // Default resolves through the legacy Lunge tick-box.
        public WindUpStyle ResolvedWindUp
            => WindUp != WindUpStyle.Default ? WindUp : (Lunge ? WindUpStyle.Lunge : WindUpStyle.None);

        public bool Gathers => ResolvedWindUp == WindUpStyle.Gather || ResolvedWindUp == WindUpStyle.GatherAndLunge;
        public bool Lunges => ResolvedWindUp == WindUpStyle.Lunge || ResolvedWindUp == WindUpStyle.GatherAndLunge;

        // Charge time read straight off the prefab's particle systems.
        public float GatherSeconds
        {
            get
            {
                if (GatherEffectPrefab == null) return WindUpSeconds;
                float longest = 0f;
                foreach (var ps in GatherEffectPrefab.GetComponentsInChildren<ParticleSystem>(true))
                {
                    var main = ps.main;
                    longest = Mathf.Max(longest, main.startDelay.constantMax + main.duration);
                }
                return longest > 0f ? longest : WindUpSeconds;
            }
        }

        // How long leftover particles need to fade after emission stops.
        public float GatherTailSeconds
        {
            get
            {
                if (GatherEffectPrefab == null) return 0f;
                float tail = 0f;
                foreach (var ps in GatherEffectPrefab.GetComponentsInChildren<ParticleSystem>(true))
                    tail = Mathf.Max(tail, ps.main.startLifetime.constantMax);
                return Mathf.Min(tail, 5f);
            }
        }
    }
}
