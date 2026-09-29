using System;
using System.Collections.Generic;
using UnityEngine;
using DungeonTower.Core;

namespace DungeonTower.UI
{
    /// <summary>
    /// What number (if any) to print on a status icon.
    /// Auto picks from the status's StatusRule:
    ///   stack-decay statuses (Poison, Bleed, Regeneration) -> Stacks
    ///   magnitude-decay statuses (Burn)                    -> Magnitude (rounded)
    ///   duration statuses (Stun, Slow, Doom, ...)          -> Duration (turns left)
    ///   everything else (flag-style: Mark, Thorns, ...)    -> nothing
    /// Override per status in the asset if Auto guesses wrong.
    /// </summary>
    public enum StatusCountMode { Auto, None, Stacks, Duration, Magnitude }

    /// <summary>
    /// Presentation data for statuses, kept out of StatusRules on purpose
    /// (rules = gameplay, this = looks). One Entry per StatusEffectId.
    ///
    /// Placeholder phase: leave Sprite empty and the icon is a circle
    /// tinted with Color. Later, drop a sprite into Entry.Sprite and the
    /// icon shows the sprite untinted — no code changes.
    ///
    /// Create via Assets > Create > DungeonTower > Status Visuals, then use
    /// the component's context menu (three dots) > "Populate Missing Entries".
    /// </summary>
    [CreateAssetMenu(menuName = "DungeonTower/Status Visuals", fileName = "StatusVisuals")]
    public sealed class StatusVisualsSO : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public StatusEffectId Id;
            public Color Color = Color.gray;
            public Sprite Sprite;
            public StatusCountMode CountMode = StatusCountMode.Auto;
        }

        [Header("Border ring color by polarity")]
        [SerializeField] private Color _beneficialBorder = new Color(0.30f, 0.85f, 0.35f, 1f);
        [SerializeField] private Color _harmfulBorder = new Color(0.90f, 0.25f, 0.25f, 1f);

        [Header("One entry per status")]
        [SerializeField] private List<Entry> _entries = new List<Entry>();

        private Dictionary<StatusEffectId, Entry> _lookup;

        private void OnEnable() => _lookup = null;
        private void OnValidate() => _lookup = null;

        public Color BorderColor(StatusPolarity polarity)
            => polarity == StatusPolarity.Beneficial ? _beneficialBorder : _harmfulBorder;

        /// <summary>Never returns null — a missing status gets a gray fallback.</summary>
        public Entry Get(StatusEffectId id)
        {
            if (_lookup == null) BuildLookup();
            if (_lookup.TryGetValue(id, out var entry)) return entry;

            var fallback = new Entry { Id = id, Color = Color.gray, CountMode = StatusCountMode.Auto };
            _lookup[id] = fallback;
            return fallback;
        }

        private void BuildLookup()
        {
            _lookup = new Dictionary<StatusEffectId, Entry>();
            foreach (var e in _entries)
                if (e != null) _lookup[e.Id] = e;
        }

        [ContextMenu("Populate Missing Entries")]
        private void PopulateMissing()
        {
            var have = new HashSet<StatusEffectId>();
            foreach (var e in _entries)
                if (e != null) have.Add(e.Id);

            foreach (StatusEffectId id in Enum.GetValues(typeof(StatusEffectId)))
            {
                if (have.Contains(id)) continue;
                _entries.Add(new Entry { Id = id, Color = DefaultColor(id), CountMode = StatusCountMode.Auto });
            }

            _lookup = null;
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        private static Color DefaultColor(StatusEffectId id)
        {
            switch (id)
            {
                case StatusEffectId.Poison:       return new Color(0.45f, 0.80f, 0.20f);
                case StatusEffectId.Burn:         return new Color(1.00f, 0.55f, 0.10f);
                case StatusEffectId.Bleed:        return new Color(0.65f, 0.05f, 0.10f);
                case StatusEffectId.Regeneration: return new Color(0.55f, 0.95f, 0.55f);
                case StatusEffectId.Doom:         return new Color(0.45f, 0.15f, 0.60f);
                case StatusEffectId.Weaken:       return new Color(0.75f, 0.65f, 0.45f);
                case StatusEffectId.Sunder:       return new Color(0.55f, 0.35f, 0.20f);
                case StatusEffectId.Slow:         return new Color(0.45f, 0.55f, 0.75f);
                case StatusEffectId.Haste:        return new Color(1.00f, 0.90f, 0.25f);
                case StatusEffectId.Fortify:      return new Color(0.35f, 0.55f, 0.85f);
                case StatusEffectId.Root:         return new Color(0.40f, 0.30f, 0.15f);
                case StatusEffectId.Stun:         return new Color(1.00f, 1.00f, 0.60f);
                case StatusEffectId.Silence:      return new Color(0.60f, 0.60f, 0.65f);
                case StatusEffectId.Taunt:        return new Color(0.95f, 0.35f, 0.20f);
                case StatusEffectId.Mark:         return new Color(0.90f, 0.20f, 0.70f);
                case StatusEffectId.Thorns:       return new Color(0.50f, 0.60f, 0.20f);
                case StatusEffectId.Momentum:     return new Color(0.25f, 0.90f, 0.90f);
                case StatusEffectId.SecondWind:   return new Color(1.00f, 0.60f, 0.75f);
                case StatusEffectId.Ward:         return new Color(0.70f, 0.85f, 1.00f);
                default:                          return Color.gray;
            }
        }
    }
}
