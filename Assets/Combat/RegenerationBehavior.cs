namespace DungeonTower.Combat
{
    /// <summary>
    /// Reverse Poison: at the owner's turn start, heals HP equal to the
    /// current stack count (the tracker then drops one stack). Heal()
    /// already clamps to max HP, so overheal is simply wasted.
    /// </summary>
    public sealed class RegenerationBehavior : StatusBehaviorBase
    {
        public override void OnTurnStart(CombatUnit owner, StatusEffectInstance instance, StatusTickResult result)
        {
            int before = owner.CurrentHp;
            owner.Heal(instance.Stacks);
            int healed = owner.CurrentHp - before;
            if (healed > 0)
                result.Log.Add($"{owner.DisplayName} regenerates {healed} HP.");
        }
    }
}
