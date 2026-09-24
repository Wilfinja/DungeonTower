using System.Collections.Generic;
using System.Linq;

namespace DungeonTower.Combat
{
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

        public IEnumerable<CombatUnit> GetTurnOrder()
        {
            if (CurrentUnit != null) yield return CurrentUnit;
            foreach (var unit in _turnOrder.Remaining)
                if (unit.IsAlive) yield return unit;
        }
    }
}
