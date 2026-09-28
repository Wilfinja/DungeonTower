namespace DungeonTower.Combat
{
    /// <summary>
    /// The one way a status deals its own damage (Poison, Burn, Bleed,
    /// Doom). Goes straight to CombatUnit.ApplyDamage — deliberately
    /// bypassing DamagePipeline, so Ward, Mark and Thorns don't touch
    /// damage-over-time — but still honors Second Wind, which lives in
    /// ApplyDamage itself.
    /// </summary>
    internal static class DotDamage
    {
        public static void Deal(CombatUnit owner, int amount, string message, StatusTickResult result)
        {
            if (amount <= 0) return;

            owner.ApplyDamage(amount, out bool saved);
            result.Log.Add(message);
            if (saved)
                result.Log.Add($"{owner.DisplayName} refuses to fall — Second Wind leaves them at 1 HP!");
        }
    }
}
