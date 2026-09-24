namespace DungeonTower.Core
{
    /// <summary>
    /// One thing a weapon, scroll, or potion lets a unit do: EffectKind
    /// decides what actually happens (deal damage, heal, or grant a
    /// stat bonus) to whichever target(s) fall in its area; the rest of
    /// the fields are only meaningful for the EffectKind that uses them —
    /// Kind/DamageMultiplier for Damage, HealHp/HealMp for Heal, Bonus
    /// for Buff. Range/AreaShape/AreaRadius/CooldownTurns apply
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
    }
}
