using System;

namespace DungeonTower.Combat
{
    /// <summary>
    /// At the owner's turn start, deals Magnitude percent of the owner's
    /// MAX HP (at least 1); the tracker then lowers the percentage by 5
    /// points (see StatusRules), so it burns hottest first and cools
    /// off until it hits zero.
    /// </summary>
    public sealed class BurnBehavior : StatusBehaviorBase
    {
        public override void OnTurnStart(CombatUnit owner, StatusEffectInstance instance, StatusTickResult result)
        {
            int damage = Math.Max(1,
                (int)Math.Round(owner.Stats.MaxHp * instance.Magnitude / 100.0, MidpointRounding.AwayFromZero));
            DotDamage.Deal(owner, damage,
                $"{owner.DisplayName} burns for {damage} ({instance.Magnitude:0}% of max HP).", result);
        }
    }
}
