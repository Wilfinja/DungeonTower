using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DungeonTower.Items
{
    /// <summary>
    /// Scene-level lookup from a floor number to whichever
    /// DungeonThemeSO covers it. Populate the list in the Inspector; if
    /// two themes' ranges overlap for the same floor, the first one
    /// listed wins and a warning fires so the overlap doesn't go
    /// unnoticed.
    /// </summary>
    public sealed class ThemeRegistry : MonoBehaviour
    {
        [SerializeField] private List<DungeonThemeSO> _themes = new List<DungeonThemeSO>();

        public DungeonThemeSO GetThemeForFloor(int floor)
        {
            var matches = _themes.Where(t => t != null && t.CoversFloor(floor)).ToList();

            if (matches.Count == 0)
            {
                Debug.LogError($"ThemeRegistry: no DungeonThemeSO covers floor {floor}. Check the registry's Themes list.");
                return null;
            }

            if (matches.Count > 1)
                Debug.LogWarning(
                    $"ThemeRegistry: floor {floor} is covered by {matches.Count} themes " +
                    $"({string.Join(", ", matches.Select(t => t.DisplayName))}) — using the first one listed.");

            return matches[0];
        }
    }
}
