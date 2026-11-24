using System.Collections.Generic;

namespace DungeonTower.Core
{

    public interface IPassive
    {
        string Name { get; }

        int passivempRegen { get; }
        int passivehpRegen { get; }
        int passiveRangeBonus {  get; }
        int passivePullRange {  get; }
        int passivePushRange { get; }
        int passivePushResist {  get; }
        int passivePullResist { get; }
        AttackKind passiveKind { get; }
        int passiveAuraDamage { get; }
        int passiveAuraRange { get; }

        float passiveThornsPercent { get; }

        int passiveAuraHeal { get; }
        int passiveAuraHealRange { get; }
        int passiveOnKillHeal { get; }
        int passiveOnKillMp { get; }
        IReadOnlyList<StatusApplication> passiveOnKillStatuses { get; }
        float passiveLastStandThreshold { get; }   // % of max HP
        float passiveLastStandReduction { get; }   // % less damage taken
        float passiveLastStandBonus { get; }       // % more damage dealt
        int passiveThornsReach { get; }

        IReadOnlyList<StatusEffectId> passiveImmunity { get; }
        IReadOnlyList<WeaponDamageBonus> passiveWeaponDamage { get; }

    }
}
