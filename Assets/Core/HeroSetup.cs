using System.Collections.Generic;

namespace DungeonTower.Core
{
    /// <summary>
    /// One hero as chosen on the character creation screen: a name, where
    /// their creation points went (on top of the 3/3/3 start), and the
    /// gear they start with. Plain data using Core's item interfaces, so
    /// the creation scene can hand it to the game scene without either
    /// side knowing about the other.
    /// </summary>
    public sealed class HeroSetup
    {
        public string Name { get; set; } = "Hero";
        public int Body { get; set; }
        public int Mind { get; set; }
        public int Spirit { get; set; }
        public IWeapon Weapon { get; set; }
        public IArmor Armor { get; set; }

        // Each entry fills one belt slot's worth, in order; the same item
        // repeated stacks into one slot (see BattleController.LoadBeltFromLists).
        public List<IPotion> BeltPotions { get; } = new List<IPotion>();
        public List<IScroll> BeltScrolls { get; } = new List<IScroll>();
    }

    /// <summary>
    /// The party chosen on the creation screen, parked here so it survives
    /// the scene change into the game. The title scene clears it on load,
    /// so a fresh run always starts from creation; if the game scene is
    /// played on its own, HasParty is false and BattleController falls
    /// back to its Default Party.
    /// </summary>
    public static class PartySetup
    {
        private static readonly List<HeroSetup> Chosen = new List<HeroSetup>();

        public static IReadOnlyList<HeroSetup> Heroes => Chosen;
        public static bool HasParty => Chosen.Count > 0;

        public static void Set(IEnumerable<HeroSetup> heroes)
        {
            Chosen.Clear();
            if (heroes != null) Chosen.AddRange(heroes);
        }

        public static void Clear() => Chosen.Clear();
    }
}
