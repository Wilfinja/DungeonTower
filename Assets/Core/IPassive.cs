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

        IReadOnlyList<StatusEffectId> passiveImmunity { get; }
        IReadOnlyList<WeaponDamageBonus> passiveWeaponDamage { get; }

    }
}
