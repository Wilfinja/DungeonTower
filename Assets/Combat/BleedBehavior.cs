namespace DungeonTower.Combat
{
    /// <summary>
    /// Bleeding hurts when the owner MOVES, not on a timer: damage equal to
    /// the current stack count, every move. It still fades on its own (2
    /// stacks per turn at turn start, see StatusRules), and a Cleanse
    /// aimed at Bleed — a bandage — stops it outright. Standing still
    /// (attacking, waiting) costs nothing.
    /// </summary>
    public sealed class BleedBehavior : StatusBehaviorBase
    {
        public override void OnMoved(CombatUnit owner, StatusEffectInstance instance, StatusTickResult result)
            => DotDamage.Deal(owner, instance.Stacks,
                $"{owner.DisplayName} bleeds for {instance.Stacks} as they move.", result);
    }
}
