#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using UnityEngine;
using DungeonTower.Combat;
using DungeonTower.Core;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DungeonTower.UI
{
    // ---------------------------------------------------------------
    // Tiny read-only attribute so display-only fields are greyed out.
    // ---------------------------------------------------------------
    public sealed class DebugReadOnlyAttribute : PropertyAttribute { }

#if UNITY_EDITOR
    [CustomPropertyDrawer(typeof(DebugReadOnlyAttribute))]
    public sealed class DebugReadOnlyDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            bool prev = GUI.enabled;
            GUI.enabled = false;
            EditorGUI.PropertyField(position, property, label, true);
            GUI.enabled = prev;
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
            => EditorGUI.GetPropertyHeight(property, label, true);
    }
#endif

    [System.Serializable]
    public struct StatusRow
    {
        [DebugReadOnly] public StatusEffectId id;
        [Delayed] public int stacks;
        [Delayed] public int duration;
        [Delayed] public float magnitude;
        [DebugReadOnly] public string note;
    }

    /// <summary>
    /// Live Inspector view of one CombatUnit. CombatUnit is a plain C# class,
    /// so this component mirrors it: every frame it (1) pushes any edits you
    /// made in the Inspector into the unit, then (2) pulls the unit's real
    /// values back into the serialized fields.
    ///
    /// Lives on its own always-active GameObject (under BattleController >
    /// _UnitDebug) rather than on the UnitView, because UnitView deactivates
    /// itself when a unit is dead or fogged. Created by UnitDebugInspector.Attach.
    ///
    /// Only uses CombatUnit / StatusEffectTracker / UnitStats public API, so
    /// every edit goes through the game's own rules (stack caps, stat-bonus
    /// sync, Second Wind, etc.).
    /// </summary>
    public sealed class UnitDebugInspector : MonoBehaviour
    {
        private static int _nextId = 1;

        // ---------------- Identity (read-only) ----------------
        [Header("Identity (read-only)")]
        [DebugReadOnly, SerializeField] private string displayName;
        [DebugReadOnly, SerializeField] private string faction;
        [DebugReadOnly, SerializeField] private bool isAlive;
        [DebugReadOnly, SerializeField] private bool isAlerted;
        [DebugReadOnly, SerializeField] private Vector2Int gridPosition;
        [DebugReadOnly, SerializeField] private string equippedWeapon;
        [DebugReadOnly, SerializeField] private string equippedArmor;

        // ---------------- Vitals (editable) ----------------
        [Header("Vitals — edit live (HP is clamped to 1..Max so death handling isn't bypassed)")]
        [Delayed, SerializeField] private int currentHp;
        [Delayed, SerializeField] private int currentMp;
        [DebugReadOnly, SerializeField] private int maxHp;
        [DebugReadOnly, SerializeField] private int maxMp;

        // ---------------- Statuses ----------------
        [Header("Active statuses — edit Stacks/Duration/Magnitude; delete an element to remove that status")]
        [SerializeField] private List<StatusRow> statuses = new List<StatusRow>();

        [Header("Add a status — fill in, then tick 'Apply Status Now' (Duration 0 = status default)")]
        [SerializeField] private StatusEffectId addId;
        [SerializeField] private int addStacks = 1;
        [SerializeField] private int addDuration;
        [SerializeField] private float addMagnitude;
        [SerializeField] private DerivedStatBonus addStatBonus;
        [SerializeField] private bool applyStatusNow;
        [SerializeField] private bool clearAllStatusesNow;

        // ---------------- Permanent battle bonus ----------------
        [Header("Permanent-for-the-battle bonus (UnitStats.AddBonus) — additive; use negatives to undo")]
        [SerializeField] private DerivedStatBonus bonusToAdd;
        [SerializeField] private bool addBonusNow;

        // ---------------- Quick actions ----------------
        [Header("Quick actions (tick to fire)")]
        [SerializeField] private bool healToFullNow;
        [SerializeField] private bool refillMpNow;
        [SerializeField] private bool alertNow;

        // ---------------- Stats (read-only) ----------------
        [Header("Stats (read-only)")]
        [DebugReadOnly, SerializeField] private int level;
        [DebugReadOnly, SerializeField] private int unspentPoints;
        [DebugReadOnly, SerializeField] private int body;
        [DebugReadOnly, SerializeField] private int mind;
        [DebugReadOnly, SerializeField] private int spirit;
        [DebugReadOnly, SerializeField] private float physicalAttack;
        [DebugReadOnly, SerializeField] private float physicalDefense;
        [DebugReadOnly, SerializeField] private float magicAttack;
        [DebugReadOnly, SerializeField] private float magicDefense;
        [DebugReadOnly, SerializeField] private int initiative;
        [DebugReadOnly, SerializeField] private int moveRange;
        [DebugReadOnly, SerializeField] private float statusResist;
        [DebugReadOnly, SerializeField] private float critChance;

        // ---------------- AI (read-only) ----------------
        [Header("AI / senses (read-only)")]
        [DebugReadOnly, SerializeField] private int detectionRadius;
        [DebugReadOnly, SerializeField] private int alertRadius;
        [DebugReadOnly, SerializeField] private string targetingStrategy;
        [DebugReadOnly, SerializeField] private int targetingRange;
        [DebugReadOnly, SerializeField] private string supportTargetingStrategy;
        [DebugReadOnly, SerializeField] private float supportHealThreshold;

        [Header("Weapon ability cooldowns (read-only, turns remaining)")]
        [SerializeField] private List<string> cooldowns = new List<string>();

        // ---------------- Runtime state ----------------
        private CombatUnit _unit;
        private UnitView _owner;
        private int _shownHp, _shownMp;
        private readonly Dictionary<StatusEffectId, StatusRow> _snap = new Dictionary<StatusEffectId, StatusRow>();
        private readonly HashSet<StatusEffectId> _seen = new HashSet<StatusEffectId>();

        /// <summary>Called from UnitView.Bind. Creates the debug object under a shared container.</summary>
        public static void Attach(UnitView owner, CombatUnit unit)
        {
            Transform root = owner.transform.parent;
            Transform container = root != null ? root.Find("_UnitDebug") : null;
            if (container == null)
            {
                var c = new GameObject("_UnitDebug");
                if (root != null) c.transform.SetParent(root, false);
                container = c.transform;
            }

            var go = new GameObject();
            go.transform.SetParent(container, false);
            go.AddComponent<UnitDebugInspector>().Bind(owner, unit);
        }

        private void Bind(UnitView owner, CombatUnit unit)
        {
            _owner = owner;
            _unit = unit;
            gameObject.name = $"{unit.DisplayName} [{unit.Faction}] #{_nextId++}";
            Pull();
        }

        private void Update()
        {
            // The view was destroyed (e.g. the battle was rebuilt) — clean up.
            if (_owner == null || _unit == null) { Destroy(gameObject); return; }

            PushVitals();
            PushStatusEdits();
            PushActions();
            Pull();
        }

        // =========================================================
        // Inspector -> unit
        // =========================================================

        private void PushVitals()
        {
            if (!_unit.IsAlive) return;   // never resurrect via Heal

            if (currentHp != _shownHp)
            {
                int target = Mathf.Clamp(currentHp, 1, _unit.Stats.MaxHp);
                int delta = target - _unit.CurrentHp;
                if (delta > 0) _unit.Heal(delta);
                else if (delta < 0) _unit.ApplyDamage(-delta);   // target >= 1, so Second Wind never triggers
            }

            if (currentMp != _shownMp)
            {
                int target = Mathf.Clamp(currentMp, 0, _unit.Stats.MaxMp);
                int delta = target - _unit.CurrentMp;
                if (delta > 0) _unit.RestoreMp(delta);
                else if (delta < 0) _unit.SpendMp(-delta);
            }
        }

        private void PushStatusEdits()
        {
            if (_snap.Count == 0) return;

            _seen.Clear();
            foreach (var row in statuses)
            {
                // Rows for statuses the unit didn't have last frame (e.g. a
                // freshly clicked "+") are ignored — use the Add fields instead.
                if (!_snap.TryGetValue(row.id, out var old)) continue;
                _seen.Add(row.id);

                bool edited = row.stacks != old.stacks
                              || row.duration != old.duration
                              || !Mathf.Approximately(row.magnitude, old.magnitude);
                if (!edited) continue;

                var live = _unit.Status.Get(row.id);
                if (live != null) OverrideStatus(live, row);
            }

            // A row that disappeared from the list = the user deleted it.
            foreach (var kv in _snap)
            {
                if (_seen.Contains(kv.Key)) continue;
                _unit.Status.Cleanse(new List<StatusEffectId> { kv.Key });   // never pass an empty list: that means "all harmful"
            }
        }

        // Public API only exposes Apply/Cleanse for writing, so an edit is
        // "remove, then re-apply with the new numbers". That keeps the tracker's
        // own stacking caps and stat-bonus sync in charge. Side effect: the
        // Momentum "primed" flag (and similar internal flags) resets.
        private void OverrideStatus(StatusEffectInstance live, StatusRow row)
        {
            var app = new StatusApplication
            {
                Id = live.Id,
                Chance = 100f,
                Stacks = Mathf.Max(1, row.stacks),
                Duration = row.duration,        // 0 = status default
                Magnitude = row.magnitude,
                StatBonus = live.StatBonus
            };
            var source = live.Source ?? _unit;

            _unit.Status.Cleanse(new List<StatusEffectId> { live.Id });
            _unit.Status.Apply(app, source, null);   // null rng = deterministic, skips chance/resist rolls
        }

        private void PushActions()
        {
            if (applyStatusNow)
            {
                applyStatusNow = false;
                var app = new StatusApplication
                {
                    Id = addId,
                    Chance = 100f,
                    Stacks = addStacks,
                    Duration = addDuration,
                    Magnitude = addMagnitude,
                    StatBonus = addStatBonus
                };
                _unit.Status.Apply(app, _unit, null);
            }

            if (clearAllStatusesNow)
            {
                clearAllStatusesNow = false;
                var ids = new List<StatusEffectId>();
                foreach (var inst in _unit.Status.Active) ids.Add(inst.Id);
                if (ids.Count > 0) _unit.Status.Cleanse(ids);
            }

            if (addBonusNow)
            {
                addBonusNow = false;
                _unit.Stats.AddBonus(bonusToAdd);
                _unit.ClampResources();
                bonusToAdd = default;
            }

            if (healToFullNow)
            {
                healToFullNow = false;
                if (_unit.IsAlive) _unit.Heal(_unit.Stats.MaxHp);
            }

            if (refillMpNow)
            {
                refillMpNow = false;
                _unit.RestoreMp(_unit.Stats.MaxMp);
            }

            if (alertNow)
            {
                alertNow = false;
                _unit.Alert();
            }
        }

        // =========================================================
        // Unit -> Inspector
        // =========================================================

        private void Pull()
        {
            var s = _unit.Stats;

            displayName = _unit.DisplayName;
            faction = _unit.Faction.ToString();
            isAlive = _unit.IsAlive;
            isAlerted = _unit.IsAlerted;
            gridPosition = new Vector2Int(_unit.Position.X, _unit.Position.Y);
            equippedWeapon = NameOf(_unit.EquippedWeapon);
            equippedArmor = NameOf(s.EquippedArmor);

            currentHp = _shownHp = _unit.CurrentHp;
            currentMp = _shownMp = _unit.CurrentMp;
            maxHp = s.MaxHp;
            maxMp = s.MaxMp;

            level = s.Level;
            unspentPoints = s.UnspentPoints;
            body = s.Body;
            mind = s.Mind;
            spirit = s.Spirit;
            physicalAttack = s.PhysicalAttack;
            physicalDefense = s.PhysicalDefense;
            magicAttack = s.MagicAttack;
            magicDefense = s.MagicDefense;
            initiative = s.Initiative;
            moveRange = s.MoveRange;
            statusResist = s.StatusResist;
            critChance = s.CritChance;

            detectionRadius = _unit.DetectionRadius;
            alertRadius = _unit.AlertRadius;
            targetingStrategy = _unit.TargetingStrategy.ToString();
            targetingRange = _unit.TargetingRange;
            supportTargetingStrategy = _unit.SupportTargetingStrategy.ToString();
            supportHealThreshold = _unit.SupportHealThreshold;

            // Statuses
            statuses.Clear();
            _snap.Clear();
            foreach (var inst in _unit.Status.Active)
            {
                var row = new StatusRow
                {
                    id = inst.Id,
                    stacks = inst.Stacks,
                    duration = inst.Duration,
                    magnitude = inst.Magnitude,
                    note = $"{StatusRules.Get(inst.Id).Polarity} | from {(inst.Source != null ? inst.Source.DisplayName : "-")}{(inst.Primed ? " | primed" : "")}"
                };
                statuses.Add(row);
                _snap[inst.Id] = row;
            }

            // Cooldowns
            cooldowns.Clear();
            if (_unit.EquippedWeapon != null)
            {
                foreach (var ability in _unit.EquippedWeapon.Abilities)
                    cooldowns.Add($"{ability.Name}: {_unit.GetRemainingCooldown(ability)}");
            }
        }

        private static string NameOf(object o)
        {
            if (o == null) return "(none)";
            if (o is UnityEngine.Object uo) return uo.name;
            return o.ToString();
        }
    }
}
#endif
