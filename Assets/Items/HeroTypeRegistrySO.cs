using System.Collections.Generic;
using UnityEngine;

namespace DungeonTower.Items
{
    /// <summary>
    /// The list of every hero type in the game. The town (tavern) and the
    /// dungeon both point at this one asset: the tavern shows these types,
    /// and the dungeon's save system uses it to find a hero's starting gear
    /// again when a save is loaded.
    /// </summary>
    [CreateAssetMenu(menuName = "Dungeon Tower/Hero Type Registry", fileName = "HeroTypeRegistry")]
    public sealed class HeroTypeRegistrySO : ScriptableObject
    {
        [SerializeField] private List<HeroTypeSO> _types = new List<HeroTypeSO>();

        public IReadOnlyList<HeroTypeSO> Types => _types;

        public HeroTypeSO Find(string id)
        {
            foreach (var type in _types)
                if (type != null && type.Id == id) return type;
            return null;
        }
    }
}
