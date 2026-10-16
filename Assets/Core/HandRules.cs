using System;

namespace DungeonTower.Core
{
    /// <summary>How a weapon is held.</summary>
    public enum HandUse
    {
        OneHanded,   // main hand or off hand; two of them = dual wielding
        OffHand,     // shields, focuses, tomes: off hand only
        TwoHanded    // fills both hands
    }

    public enum HandSlot { Main, Off }

    /// <summary>One "needs Body 4" term. Plain public fields so the Inspector can edit a list of them.</summary>
    [Serializable]
    public struct StatRequirement
    {
        public PrimaryStat Stat;
        public int Value;

        public StatRequirement(PrimaryStat stat, int value)
        {
            Stat = stat;
            Value = value;
        }
    }

    /// <summary>The slot rules, in one place so combat, inventory and creation agree.</summary>
    public static class HandRules
    {
        // Across both hands.
        public const int MaxAbilities = 4;

        // Max abilities a single weapon may carry.
        public static int MaxAbilitiesFor(HandUse hands) => hands == HandUse.TwoHanded ? 3 : 2;

        public static string Describe(HandUse hands)
        {
            switch (hands)
            {
                case HandUse.OffHand: return "Off-hand";
                case HandUse.TwoHanded: return "Two-handed";
                default: return "One-handed";
            }
        }

        // Slot-shape rules only (not stat requirements). otherHand is what
        // the OTHER hand holds right now (null if empty).
        public static bool Fits(IWeapon weapon, HandSlot slot, IWeapon otherHand, out string reason)
        {
            reason = null;
            if (weapon == null) return true;

            if (slot == HandSlot.Main)
            {
                if (weapon.Hands == HandUse.OffHand)
                {
                    reason = "it can only be held in the off hand";
                    return false;
                }
                if (weapon.Hands == HandUse.TwoHanded && otherHand != null)
                {
                    reason = "it needs both hands - empty the off hand first";
                    return false;
                }
                return true;
            }

            if (weapon.Hands == HandUse.TwoHanded)
            {
                reason = "it needs both hands - put it in the main hand";
                return false;
            }
            if (otherHand != null && otherHand.Hands == HandUse.TwoHanded)
            {
                reason = "the main hand holds a two-handed weapon";
                return false;
            }
            return true;
        }

        public static bool MeetsRequirements(IWeapon weapon, StatBlock stats)
        {
            if (weapon == null) return false;
            var reqs = weapon.Requirements;
            for (int i = 0; i < reqs.Count; i++)
                if (stats.Get(reqs[i].Stat) < reqs[i].Value) return false;
            return true;
        }
    }
}
