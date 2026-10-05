using System;
using System.Collections.Generic;
using DungeonTower.Core;

namespace DungeonTower.Combat
{
    /// <summary>
    /// One unit as it exists on the battle grid this fight: its stats,
    /// position, and current HP/MP. Wraps a UnitStats rather than
    /// duplicating stat logic — Core owns what the numbers mean, this
    /// just tracks how much HP/MP is left right now.
    /// </summary>
    public sealed class CombatUnit
    {
        // Fixed for every unit for now — not something enemies use yet,
        // but harmless to give them one too rather than special-casing
        // by faction.
        private const int BeltSlotCount = 4;

        public string DisplayName { get; }
        public Faction Faction { get; }
        public UnitStats Stats { get; }
        public IWeapon EquippedWeapon { get; private set; }
        public int DetectionRadius { get; }
        public int AlertRadius { get; }
        public EnemyTargetingStrategy TargetingStrategy { get; }
        public int TargetingRange { get; }
        public SupportTargetingStrategy SupportTargetingStrategy { get; }
        public float SupportHealThreshold { get; }
        public Belt Belt { get; } = new Belt(BeltSlotCount);
        public StatusEffectTracker Status { get; }

        // What this unit (and its alerted pack — see
        // BattleController.RecordSighting) last actually saw of each
        // hostile. Present on every unit for the same reason
        // TargetingStrategy etc. are — harmless and simply unused on a
        // player-controlled one.
        public TargetMemory Memory { get; } = new TargetMemory();

        public GridPosition Position { get; set; }
        public int CurrentHp { get; private set; }
        public int CurrentMp { get; private set; }

        public bool IsAlive => CurrentHp > 0;

        // Once true, stays true for the rest of the fight — no de-aggro if
        // the player retreats out of range. Only meaningful for enemies;
        // harmless and unused for player units.
        public bool IsAlerted { get; private set; }

        // Tiles an idle, un-alerted unit is willing to wander into on its
        // own turn — a room's floor tiles for a room-roaming group, or a
        // local corridor patch for a corridor-roaming one. Null/empty
        // means "don't roam," same as before this existed. Only ever
        // consulted while idle — once alerted, a unit chases using its
        // full move range, unconstrained by this.
        public HashSet<GridPosition> RoamZone { get; private set; }

        public void SetRoamZone(IEnumerable<GridPosition> zone)
            => RoamZone = zone != null ? new HashSet<GridPosition>(zone) : null;

        // Remaining cooldown per ability this unit has actually used —
        // keyed by the ability reference itself rather than by weapon
        // slot index, so it works the same whether the ability came
        // from an equipped weapon or a cast scroll. An ability never
        // used yet, or one with no cooldown at all, simply has no entry
        // here — IsOnCooldown/GetRemainingCooldown both treat "absent"
        // as "ready."
        private readonly Dictionary<IAbility, int> _cooldowns = new Dictionary<IAbility, int>();

        public bool IsOnCooldown(IAbility ability) => GetRemainingCooldown(ability) > 0;

        public int GetRemainingCooldown(IAbility ability)
            => ability != null && _cooldowns.TryGetValue(ability, out var remaining) ? remaining : 0;

        // Called once, right when the ability is actually used.
        public void TriggerCooldown(IAbility ability)
        {
            if (ability != null && ability.CooldownTurns > 0)
                _cooldowns[ability] = ability.CooldownTurns;
        }

        // Called once at the start of this unit's own turn — ticks every
        // tracked ability down by one, dropping it once it reaches 0 so
        // the dictionary never accumulates stale zero-entries.
        public void TickCooldowns()
        {
            if (_cooldowns.Count == 0) return;

            var abilities = new List<IAbility>(_cooldowns.Keys);
            foreach (var ability in abilities)
            {
                int remaining = _cooldowns[ability] - 1;
                if (remaining <= 0) _cooldowns.Remove(ability);
                else _cooldowns[ability] = remaining;
            }
        }

        public CombatUnit(string displayName, Faction faction, UnitStats stats, GridPosition startPosition,
    int detectionRadius = 5, int alertRadius = 4,
    EnemyTargetingStrategy targetingStrategy = EnemyTargetingStrategy.Nearest,
    int targetingRange = 8,
    SupportTargetingStrategy supportTargetingStrategy = SupportTargetingStrategy.LowestHpPercent,
    float supportHealThreshold = 50f)
        {
            DisplayName = displayName;
            Faction = faction;
            Stats = stats;
            Position = startPosition;
            DetectionRadius = detectionRadius;
            AlertRadius = alertRadius;
            TargetingStrategy = targetingStrategy;
            TargetingRange = targetingRange;
            SupportTargetingStrategy = supportTargetingStrategy;
            SupportHealThreshold = supportHealThreshold;
            CurrentHp = stats.MaxHp;
            CurrentMp = stats.MaxMp;
            Status = new StatusEffectTracker(this);
        }

        public bool CanSense(GridPosition position, IWalkableMap map)
            => Position.ManhattanDistance(position) <= DetectionRadius && LineOfSight.HasClearPath(Position, position, map);

        public void Alert() => IsAlerted = true;

        // Only one weapon slot exists right now. Returns false without
        // changing anything if the unit's stats don't meet the weapon's
        // requirement.
        public bool TryEquip(IWeapon weapon) => TryEquip(weapon, out _);

        // Same as above, but also hands back whatever was equipped before
        // (null if nothing was) — needed so a caller swapping gear via the
        // party inventory can put the old item back in the pool instead of
        // it just vanishing.
        public bool TryEquip(IWeapon weapon, out IWeapon previouslyEquipped)
        {
            previouslyEquipped = null;
            if (weapon == null || !weapon.CanEquip(Stats.Current)) return false;
            previouslyEquipped = EquippedWeapon;
            EquippedWeapon = weapon;
            return true;
        }

        public IWeapon UnequipWeapon()
        {
            var previous = EquippedWeapon;
            EquippedWeapon = null;
            return previous;
        }

        // Fills HP and MP to their current maximums. Call once after a unit's
        // starting gear is equipped: the constructor sizes HP/MP from the
        // bare stats, so armor with an HP/MP bonus would otherwise leave
        // the unit short of its own max. NOT for mid-fight gear swaps —
        // those should never heal (see ClampVitals).
        public void RestoreToFull()
        {
            CurrentHp = Stats.MaxHp;
            CurrentMp = Stats.MaxMp;
        }

        // True if the unit has the MP to use this ability. Only weapon and
        // scroll abilities charge MP — callers never ask this for potions.
        public bool CanAfford(IAbility ability)
            => ability == null || ability.MpCost <= 0 || CurrentMp >= ability.MpCost;

        // Max HP/MP can drop when armor comes off or is swapped — keeps the
        // current values from sitting above the new max.
        public void ClampVitals()
        {
            if (CurrentHp > 0) CurrentHp = Math.Max(1, Math.Min(CurrentHp, Stats.MaxHp));
            CurrentMp = Math.Min(CurrentMp, Stats.MaxMp);
        }

        public void ApplyDamage(int amount) => ApplyDamage(amount, out _);

        public void ApplyDamage(int amount, out bool savedBySecondWind)
        {
            savedBySecondWind = false;
            amount = Math.Max(0, amount);

            if (amount >= CurrentHp && CurrentHp > 0 && Status.Has(StatusEffectId.SecondWind))
            {
                Status.Remove(StatusEffectId.SecondWind);
                CurrentHp = 1;
                savedBySecondWind = true;
                return;
            }

            CurrentHp = Math.Max(0, CurrentHp - amount);
        }

        public void SpendMp(int amount)
        {
            CurrentMp = Math.Max(0, CurrentMp - Math.Max(0, amount));
        }

        public void Heal(int amount)
        {
            CurrentHp = Math.Min(Stats.MaxHp, CurrentHp + Math.Max(0, amount));
        }

        public void ClampResources()
        {
            CurrentHp = Math.Min(CurrentHp, Stats.MaxHp);
            CurrentMp = Math.Min(CurrentMp, Stats.MaxMp);
        }

        // Mirrors Heal — added for potions/scrolls that restore MP
        // rather than (or in addition to) HP.
        public void RestoreMp(int amount)
        {
            CurrentMp = Math.Min(Stats.MaxMp, CurrentMp + Math.Max(0, amount));
        }
    }
}
