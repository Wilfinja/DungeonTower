namespace DungeonTower.Combat
{
    /// <summary>
    /// At the owner's turn start, deals damage equal to the current stack
    /// count; the tracker then drops one stack (see StatusRules). Each
    /// further Poison application adds stacks, so 3 stacks = 3 damage this
    /// turn, 2 next turn, then 1.
    /// </summary>
    public sealed class PoisonBehavior : StatusBehaviorBase
    {
        public override void OnTurnStart(CombatUnit owner, StatusEffectInstance instance, StatusTickResult result)
            => DotDamage.Deal(owner, instance.Stacks,
                $"{owner.DisplayName} takes {instance.Stacks} poison damage.", result);
    }
}
