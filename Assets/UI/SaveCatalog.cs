using System.Collections.Generic;
using DungeonTower.Core;
using DungeonTower.Items;
using UnityEngine;

namespace DungeonTower.UI
{
    /// <summary>
    /// Finds items, enemies and abilities again from their asset names when
    /// a save is loaded. It's built from everything the game can hand out:
    /// the themes (enemy gear and drops, chest loot), the starter kit, and
    /// whatever starting items BattleController lists. An item that isn't
    /// reachable from any of those can't be loaded back, so SaveCatalog
    /// warns when the game tries to save one (see NameOf).
    ///
    /// Asset names are the key, so two items of one kind must not share a
    /// file name. Renaming an asset breaks saves that mention it.
    /// </summary>
    public sealed class SaveCatalog
    {
        private readonly Dictionary<string, IWeapon> _weapons = new Dictionary<string, IWeapon>();
        private readonly Dictionary<string, IArmor> _armors = new Dictionary<string, IArmor>();
        private readonly Dictionary<string, IPotion> _potions = new Dictionary<string, IPotion>();
        private readonly Dictionary<string, IScroll> _scrolls = new Dictionary<string, IScroll>();
        private readonly Dictionary<string, EnemySO> _enemies = new Dictionary<string, EnemySO>();
        private readonly Dictionary<string, IAbility> _abilities = new Dictionary<string, IAbility>();

        public static SaveCatalog Build(ThemeRegistry themes, StarterKitSO kit, IEnumerable<Object> extras)
        {
            var catalog = new SaveCatalog();

            if (themes != null)
            {
                foreach (var theme in themes.Themes)
                {
                    if (theme == null) continue;
                    foreach (var entry in theme.Enemies) catalog.AddEnemy(entry.Enemy);
                    foreach (var loot in theme.ChestLoot)
                    {
                        catalog.Add(loot.Weapon);
                        catalog.Add(loot.Armor);
                        catalog.Add(loot.Potion);
                        catalog.Add(loot.Scroll);
                    }
                }
            }

            if (kit != null)
            {
                foreach (var e in kit.Weapons) catalog.Add(e.Item);
                foreach (var e in kit.Armors) catalog.Add(e.Item);
                foreach (var e in kit.Potions) catalog.Add(e.Item);
                foreach (var e in kit.Scrolls) catalog.Add(e.Item);
            }

            if (extras != null)
                foreach (var extra in extras) catalog.Add(extra);

            return catalog;
        }

        // ---- Building -----------------------------------------------------

        private void Add(object item)
        {
            // An unassigned or destroyed asset reference is "fake null" in Unity: not == null to C#.
            if (item == null || (item is Object asset && asset == null)) return;

            switch (item)
            {
                case IWeapon weapon:
                    Put(_weapons, weapon);
                    foreach (var ability in weapon.Abilities) AddAbility(ability);
                    break;
                case IArmor armor:
                    Put(_armors, armor);
                    break;
                case IPotion potion:
                    Put(_potions, potion);
                    AddAbility(potion.Ability);
                    break;
                case IScroll scroll:
                    Put(_scrolls, scroll);
                    AddAbility(scroll.Ability);
                    break;
            }
        }

        private void AddEnemy(EnemySO enemy)
        {
            if (enemy == null) return;
            _enemies[enemy.name] = enemy;
            Add(enemy.Weapon);
            Add(enemy.OffHand);
            Add(enemy.Armor);
            foreach (var drop in enemy.Drops)
            {
                Add(drop.Potion);
                Add(drop.Scroll);
            }
        }

        private void AddAbility(IAbility ability)
        {
            if (ability == null || string.IsNullOrEmpty(ability.Name)) return;
            if (!_abilities.ContainsKey(ability.Name)) _abilities[ability.Name] = ability;
        }

        private static void Put<T>(Dictionary<string, T> into, T item)
        {
            var key = KeyOf(item);
            if (key != null) into[key] = item;
        }

        private static string KeyOf(object item) => item is Object asset ? asset.name : null;

        // ---- Saving ---------------------------------------------------------

        /// <summary>The name to store for an item ("" for none). Warns if the catalog couldn't find it again.</summary>
        public string NameOf(object item)
        {
            if (item == null) return string.Empty;

            var key = KeyOf(item);
            if (key == null)
            {
                Debug.LogWarning($"SaveCatalog: '{item}' isn't an asset, so it can't be saved.");
                return string.Empty;
            }

            bool known = item is IWeapon ? _weapons.ContainsKey(key)
                : item is IArmor ? _armors.ContainsKey(key)
                : item is IPotion ? _potions.ContainsKey(key)
                : item is IScroll ? _scrolls.ContainsKey(key)
                : true;
            if (!known)
                Debug.LogWarning($"SaveCatalog: '{key}' isn't in any theme's loot or enemy gear, the starter kit or BattleController's starting lists, " +
                                 "so loading this save will drop it. Add it to one of those.");
            return key;
        }

        public SaveItemStack StackOf(object item, int count)
        {
            if (item == null || count <= 0) return default;
            var kind = item is IWeapon ? SaveItemKind.Weapon
                : item is IArmor ? SaveItemKind.Armor
                : item is IPotion ? SaveItemKind.Potion
                : item is IScroll ? SaveItemKind.Scroll
                : SaveItemKind.None;
            if (kind == SaveItemKind.None) return default;
            return new SaveItemStack { kind = kind, name = NameOf(item), count = count };
        }

        // ---- Loading --------------------------------------------------------

        public IWeapon Weapon(string name) => Find(_weapons, name, "weapon");
        public IArmor Armor(string name) => Find(_armors, name, "armor");
        public IPotion Potion(string name) => Find(_potions, name, "potion");
        public IScroll Scroll(string name) => Find(_scrolls, name, "scroll");
        public EnemySO Enemy(string name) => Find(_enemies, name, "enemy");

        public IConsumable Consumable(SaveItemStack stack)
            => stack.kind == SaveItemKind.Potion ? (IConsumable)Potion(stack.name)
             : stack.kind == SaveItemKind.Scroll ? Scroll(stack.name)
             : null;

        /// <summary>The weapon, armor, potion or scroll a stack refers to, or null if it can't be found.</summary>
        public object Item(SaveItemStack stack)
        {
            switch (stack.kind)
            {
                case SaveItemKind.Weapon: return Weapon(stack.name);
                case SaveItemKind.Armor: return Armor(stack.name);
                case SaveItemKind.Potion: return Potion(stack.name);
                case SaveItemKind.Scroll: return Scroll(stack.name);
                default: return null;
            }
        }

        public IAbility AbilityByName(string name)
            => !string.IsNullOrEmpty(name) && _abilities.TryGetValue(name, out var ability) ? ability : null;

        // Null (not a warning) for the empty string, since "no item" is normal.
        private static T Find<T>(Dictionary<string, T> from, string name, string label) where T : class
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (from.TryGetValue(name, out var found)) return found;
            Debug.LogWarning($"SaveCatalog: the saved {label} '{name}' can't be found, so it was left out.");
            return null;
        }
    }
}
