using System.Collections.Generic;
using System.Linq;

namespace DungeonTower.Combat
{
    /// <summary>
    /// When one unit becomes alerted, nearby allies within its AlertRadius
    /// wake up too — and so on transitively through a cluster (a newly
    /// alerted unit can wake its own neighbors in turn), so a tightly
    /// grouped patrol reacts together instead of letting the player pick
    /// them off one at a time in silence. Distant or already-scattered
    /// groups are unaffected.
    /// </summary>
    public static class AlertPropagation
    {
        public static void PropagateFrom(CombatUnit source, IEnumerable<CombatUnit> allUnits)
        {
            var units = allUnits as IList<CombatUnit> ?? allUnits.ToList();
            var frontier = new Queue<CombatUnit>();
            frontier.Enqueue(source);

            while (frontier.Count > 0)
            {
                var current = frontier.Dequeue();

                foreach (var other in units)
                {
                    if (other == current || !other.IsAlive) continue;
                    if (other.Faction != current.Faction) continue;
                    if (other.IsAlerted) continue;
                    if (current.Position.ManhattanDistance(other.Position) > current.AlertRadius) continue;

                    other.Alert();
                    frontier.Enqueue(other);
                }
            }
        }
    }
}
