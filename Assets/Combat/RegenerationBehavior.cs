namespace DungeonTower.Combat
{
    /// <summary>
    /// Reverse Poison: at the owner's turn start, heals HP equal to the
    /// current stack count, boosted by the owner's healing-received bonus
    /// (the tracker then drops one stack). Overheal is simply wasted.
    /// </summary>
    public sealed class RegenerationBehavior : StatusBehaviorBase
    {
        public override void OnTurnStart(CombatUnit owner, StatusEffectInstance instance, StatusTickResult result)
        {
            int healed = owner.ReceiveHeal(instance.Stacks);
            if (healed > 0)
                result.Log.Add($"{owner.DisplayName} regenerates {healed} HP.");
        }
    }
}
