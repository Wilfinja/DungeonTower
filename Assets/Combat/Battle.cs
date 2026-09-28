using System;
using System.Collections.Generic;
using System.Linq;

namespace DungeonTower.Combat
{
    /// <summary>
    /// One entry in a turn-order forecast. RoundOffset 0 is the current
    /// round, 1 is the next round, and so on.
    /// </summary>
    public readonly struct TurnSlot
    {
        public CombatUnit Unit { get; }
        public int RoundOffset { get; }

        public TurnSlot(CombatUnit unit, int roundOffset)
        {
            Unit = unit;
            RoundOffset = roundOffset;
        }
    }

    /// <summary>
    /// Owns one fight from start to outcome: the roster, the initiative
    /// queue, and whose turn it currently is. Call EndCurrentTurn() after
    /// a unit finishes acting — it advances to the next living unit,
    /// starting a new round when the queue empties, and re-checks the
    /// outcome every time since any single action can end the fight.
    /// </summary>
    public sealed class Battle
    {
        private readonly List<CombatUnit> _units;
        private readonly TurnOrderQueue _turnOrder = new TurnOrderQueue();

        public CombatUnit CurrentUnit { get; private set; }
        public BattleOutcome Outcome { get; private set; } = BattleOutcome.InProgress;
        public int RoundNumber => _turnOrder.RoundNumber;

        public Battle(IEnumerable<CombatUnit> units)
        {
            _units = units.ToList();
        }

        public void Start()
        {
            _turnOrder.StartNewRound(_units);
            CurrentUnit = _turnOrder.TakeNext();
            Outcome = EvaluateOutcome();
            if (Outcome != BattleOutcome.InProgress)
                CurrentUnit = null;
        }

        public void EndCurrentTurn()
        {
            if (Outcome != BattleOutcome.InProgress) return;

            CurrentUnit = _turnOrder.TakeNext();
            if (CurrentUnit == null)
            {
                _turnOrder.StartNewRound(_units);
                CurrentUnit = _turnOrder.TakeNext();
            }

            Outcome = EvaluateOutcome();
            if (Outcome != BattleOutcome.InProgress)
                CurrentUnit = null;
        }

        private BattleOutcome EvaluateOutcome()
        {
            bool anyPlayerAlive = _units.Any(u => u.Faction == Faction.Player && u.IsAlive);
            bool anyEnemyAlive = _units.Any(u => u.Faction == Faction.Enemy && u.IsAlive);

            if (!anyPlayerAlive) return BattleOutcome.PlayerDefeat;
            if (!anyEnemyAlive) return BattleOutcome.PlayerVictory;
            return BattleOutcome.InProgress;
        }

        /// <summary>
        /// Forecast of upcoming turns, restricted to units `include`
        /// accepts (the caller decides who's worth showing): the rest of
        /// this round (current unit first), then the whole next round,
        /// then further projected rounds until at least `minEntries`
        /// slots exist. Later rounds are a projection — they assume the
        /// same units are alive with the same Initiative as right now.
        /// Enumerate it right away; it reads live queue state.
        /// </summary>
        public IEnumerable<TurnSlot> GetProjectedTurnOrder(Func<CombatUnit, bool> include, int minEntries)
        {
            if (Outcome != BattleOutcome.InProgress) yield break;

            int count = 0;

            if (CurrentUnit != null && include(CurrentUnit))
            {
                yield return new TurnSlot(CurrentUnit, 0);
                count++;
            }

            foreach (var unit in _turnOrder.Remaining)
            {
                if (!unit.IsAlive || !include(unit)) continue;
                yield return new TurnSlot(unit, 0);
                count++;
            }

            var oneRound = TurnOrderQueue.InitiativeOrder(_units.Where(include)).ToList();
            if (oneRound.Count == 0) yield break;

            // Always the full next round, then more whole rounds only if
            // that still leaves us short of minEntries.
            for (int round = 1; round == 1 || count < minEntries; round++)
            {
                foreach (var unit in oneRound)
                {
                    yield return new TurnSlot(unit, round);
                    count++;
                }
            }
        }
    }
}
