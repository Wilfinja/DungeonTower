namespace DungeonTower.Combat
{
    /// <summary>
    /// Moving arms the status; DamagePipeline spends it on the owner's
    /// next Damage-kind attack (Magnitude = bonus damage, in percent).
    /// Moving again re-arms it, so a move -> attack -> move -> attack
    /// rhythm keeps paying off until the status's duration runs out.
    /// </summary>
    public sealed class MomentumBehavior : StatusBehaviorBase
    {
        public override void OnMoved(CombatUnit owner, StatusEffectInstance instance, StatusTickResult result)
        {
            if (instance.Primed) return;
            instance.Primed = true;
            result.Log.Add($"{owner.DisplayName} builds momentum.");
        }
    }
}
