using System.Collections.Generic;

namespace DungeonTower.Core
{
    /// <summary>
    /// One thing a weapon, scroll, or potion lets a unit do: EffectKind
    /// decides what actually happens (deal damage, heal, grant a
    /// permanent stat bonus, or just apply statuses) to whichever
    /// target(s) fall in its area; the rest of the fields are only
    /// meaningful for the EffectKind that uses them —
    /// Kind/DamageMultiplier for Damage, HealHp/HealMp for Heal, Bonus
    /// for Buff. Range/AreaShape/AreaRadius/CooldownTurns/Statuses apply
    /// regardless of EffectKind. Damage + AreaShape.Single is a normal
    /// single-target hit — every ability authored before EffectKind or
    /// Cooldown existed keeps behaving exactly as it did, since both
    /// default to "off." Implemented by AbilitySO (DungeonTower.Items) —
    /// Core never references ScriptableObject directly, so this stays
    /// engine-free.
    /// </summary>
    public interface IAbility
    {
        string Name { get; }
        EffectKind EffectKind { get; }

        // Damage-only.
        AttackKind Kind { get; }
        float DamageMultiplier { get; }

        // Heal-only.
        int HealHp { get; }
        int HealMp { get; }

        // Buff-only — applied as a permanent-for-the-battle addition,
        // same DerivedStatBonus shape ArmorSO already grants passively.
        // (Timed stat modifiers are Statuses, not this.)
        DerivedStatBonus Bonus { get; }

        // Apply to every EffectKind.
        int Range { get; }
        AttackShape AreaShape { get; }
        int AreaRadius { get; }

        // How many of the wielder's own turns must pass after using this
        // before it's usable again — 0 means no cooldown. Remaining
        // cooldown is per-unit runtime state (tracked on CombatUnit),
        // not stored here — this is just "how long," not "how long left."
        int CooldownTurns { get; }

        // Statuses applied to each target this ability lands on: the
        // whole payload for EffectKind.Status, on-hit procs for every
        // other kind. Empty (never null) when none.
        IReadOnlyList<StatusApplication> Statuses { get; }

        // True if the Silence status stops a unit from using this when it
        // comes from an equipped weapon. (Scrolls are always blocked by
        // Silence and potions never are — see SilenceRules — so this flag
        // only matters on weapon abilities.) Defaults to false, so every
        // existing ability keeps working while silenced.
        bool Silenceable { get; }

        // Cleanse-only. Which statuses to strip from each target: empty
        // means "every Harmful status" (a general cleanse); otherwise
        // exactly the listed ones, whatever their polarity (a Bandage
        // lists just Bleed). Empty (never null) when unused.
        IReadOnlyList<StatusEffectId> Cleanses { get; }

        // Applies to any EffectKind, on every target hit: knocks the
        // target away from the attacker this many tiles (negative pulls
        // it toward the attacker instead), stopping early at the first
        // wall or occupied tile. 0 = no push. Ignored on the attacker's
        // own tile (a self-hit never pushes itself).
        int PushDistance { get; }

        // Applies to any EffectKind: instantly swaps the attacker's and
        // the target's positions instead of (or in addition to, though
        // authoring both is nonsensical — pick one) pushing. False for
        // every ability that isn't specifically a swap/blink effect.
        bool SwapWithCaster { get; }

        // --- Summon-only (EffectKind = Summon) ---
        // What placing this object actually creates. AreaShape/AreaRadius
        // (above) still decide its FOOTPRINT — Single for a totem or
        // trap sitting on one tile, Line for a wall, Blast for a cloud —
        // exactly like they decide a Damage ability's footprint.

        // How this object's Statuses actually get applied: None = it's
        // just a blocker/obscurer with no effect of its own; OnRoundTick
        // = applied every round to qualifying units within AuraRadius of
        // its footprint (Totem, Aura); OnEntry = applied once to a unit
        // the moment it steps onto one of its footprint tiles (Trap, a
        // damaging hazard zone). OnEntry never fires on a tile that also
        // has SummonBlocksMovement on, since nothing can ever step onto
        // a tile that blocks movement in the first place.
        SummonTriggerMode SummonTriggerMode { get; }

        // Reach, in tiles, beyond the object's own footprint that
        // OnRoundTick still affects — 0 means only the footprint tiles
        // themselves. Unused by OnEntry/None.
        int AuraRadius { get; }

        // Who OnRoundTick/OnEntry Statuses apply to, relative to the
        // object's owner (the caster): true = the owner's allies
        // (a beneficial totem/aura), false = everyone else (a hostile
        // totem, or a trap that can hurt anyone including enemies of
        // its placer).
        bool SummonAffectsAllies { get; }

        // OnEntry only: skip triggering on a unit that shares the
        // owner's faction — the normal case for a trap (so its placer's
        // own side doesn't set it off), but leave off for a hazard
        // that's genuinely indiscriminate.
        bool SummonIgnoreOwnerFaction { get; }

        // OnEntry only: true removes the object after it fires once
        // (Trap); false lets it keep firing on every entry for as long
        // as it exists (a damaging hazard zone, which needs
        // SummonBlocksMovement off — see SummonTriggerMode above).
        bool SummonConsumedAfterTrigger { get; }

        // True while hidden, this object has no visible marker until it
        // triggers (Trap). No partial detection/search mechanic exists
        // yet — it's invisible until it goes off, or forever if it never does.
        bool SummonIsHidden { get; }

        // How many ROUNDS (not turns) this object lasts before it's
        // removed on its own, ticking down once per round regardless of
        // whose turn it is. 0 or less = no duration cap — it only ends
        // by being destroyed (SummonMaxHp), consumed (a triggered Trap),
        // or its owner moving (EndsIfOwnerMoves).
        int SummonDuration { get; }

        // 0 or less = indestructible by damage. Otherwise this object
        // can be targeted and destroyed like a unit, at this much HP —
        // see BattleController.DamageBattlefieldObject for how damage
        // against it is currently calculated (a simplification worth
        // reading before relying on it).
        int SummonMaxHp { get; }

        // True ties this object to whichever unit cast it (Aura) — it's
        // removed the instant that unit moves, dies, or its own
        // SummonDuration runs out, whichever comes first. False (Totem,
        // Trap, Wall, Cloud) means it's independent of any unit.
        bool EndsIfOwnerMoves { get; }

        // True blocks movement onto this object's footprint tiles
        // (impassable terrain, a solid Wall) — see the OnEntry note
        // above for why this and OnEntry rarely combine.
        bool SummonBlocksMovement { get; }

        // True blocks line of sight through this object's footprint
        // tiles without blocking movement (an obscuring cloud); a solid
        // Wall typically wants this ON too, alongside SummonBlocksMovement.
        bool SummonBlocksLineOfSight { get; }
    }
}
