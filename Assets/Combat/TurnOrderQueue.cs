using System.Collections.Generic;
using System.Linq;

namespace DungeonTower.Combat
{
    /// <summary>
    /// Individual-unit initiative order: every living unit acts once per
    /// round, highest Initiative first, players and enemies interleaved
    /// together rather than in separate phases. Rebuild at the start of
    /// each round rather than patching a stale ordering as units die.
    /// </summary>
    public sealed class TurnOrderQueue
    {
        private readonly Queue<CombatUnit> _order = new Queue<CombatUnit>();

        public int RoundNumber { get; private set; }
        public bool HasNext => _order.Count > 0;

        public void StartNewRound(IEnumerable<CombatUnit> allUnits)
        {
            RoundNumber++;
            _order.Clear();

            var living = allUnits
                .Where(u => u.IsAlive)
                .OrderByDescending(u => u.Stats.Initiative)
                .ThenBy(u => u.DisplayName); // stable tiebreak, avoids nondeterministic order

            foreach (var unit in living)
                _order.Enqueue(unit);
        }

        public CombatUnit TakeNext()
        {
            // Skip anyone who died earlier this round before their turn came up.
            while (_order.Count > 0)
            {
                var next = _order.Dequeue();
                if (next.IsAlive)
                    return next;
            }
            return null;
        }
    }
}
