using System;
using System.Collections.Generic;
using System.Linq;

namespace DungeonTower.Combat
{
    /// <summary>
    /// One entry in a turn forecast. RoundOffset 0 is the current round, 1
    /// is the next round, and so on.
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

    public enum TurnPhase { Hero, Enemy }

    /// <summary>
    /// Owns one fight from start to outcome: the roster, whose phase it is,
    /// and who is acting. A round is a hero phase followed by an enemy phase.
    ///
    /// Hero phase: the heroes act in any order, as often as their actions
    /// allow. CurrentUnit is just the hero the player has selected right now
    /// (TrySelectHero); the phase ends when the controller calls
    /// EndHeroPhase().
    ///
    /// Enemy phase: enemies act one after another, nearest to the party
    /// first. CurrentUnit is the enemy acting now; EndCurrentTurn() moves to
    /// the next, and after the last one a new round starts with the hero
    /// phase. The outcome is re-checked every time since any single action
    /// can end the fight.
    /// </summary>
    public sealed class Battle
    {
        private readonly List<CombatUnit> _units;
        private readonly Queue<CombatUnit> _enemyQueue = new Queue<CombatUnit>();

        public CombatUnit CurrentUnit { get; private set; }
        public BattleOutcome Outcome { get; private set; } = BattleOutcome.InProgress;
        public TurnPhase Phase { get; private set; } = TurnPhase.Hero;
        public int RoundNumber { get; private set; }

        /// <summary>
        /// When true (the default) the fight ends in PlayerVictory the moment
        /// the last enemy dies. BattleController turns it off so a cleared
        /// floor keeps running turns while the party walks to the exit.
        /// </summary>
        public bool EndOnEnemiesDefeated { get; set; } = true;

        public Battle(IEnumerable<CombatUnit> units)
        {
            _units = units.ToList();
        }

        public void Start()
        {
            RoundNumber = 1;
            Phase = TurnPhase.Hero;
            CurrentUnit = FirstLivingHero();
            Finish();
        }

        /// <summary>Hero phase only: makes `hero` the acting hero. False if that isn't allowed.</summary>
        public bool TrySelectHero(CombatUnit hero)
        {
            if (Outcome != BattleOutcome.InProgress || Phase != TurnPhase.Hero) return false;
            if (hero == null || hero.Faction != Faction.Player || !hero.IsAlive) return false;
            CurrentUnit = hero;
            return true;
        }

        /// <summary>Hands the round to the enemies (nearest to the party act first).</summary>
        public void EndHeroPhase()
        {
            if (Outcome != BattleOutcome.InProgress || Phase != TurnPhase.Hero) return;

            Phase = TurnPhase.Enemy;
            _enemyQueue.Clear();
            foreach (var enemy in EnemyOrder()) _enemyQueue.Enqueue(enemy);

            CurrentUnit = TakeNextEnemy();
            if (CurrentUnit == null) StartHeroPhase();
            Finish();
        }

        /// <summary>
        /// Enemy phase: the current enemy is done. Moves to the next one, or
        /// starts the next round's hero phase after the last. (Called during
        /// the hero phase it ends that phase instead.)
        /// </summary>
        public void EndCurrentTurn()
        {
            if (Outcome != BattleOutcome.InProgress) return;

            if (Phase == TurnPhase.Hero)
            {
                EndHeroPhase();
                return;
            }

            CurrentUnit = TakeNextEnemy();
            if (CurrentUnit == null) StartHeroPhase();
            Finish();
        }

        private void StartHeroPhase()
        {
            RoundNumber++;
            Phase = TurnPhase.Hero;
            CurrentUnit = FirstLivingHero();
        }

        private CombatUnit TakeNextEnemy()
        {
            // Skip anyone who died earlier this phase before their turn came up.
            while (_enemyQueue.Count > 0)
            {
                var next = _enemyQueue.Dequeue();
                if (next.IsAlive) return next;
            }
            return null;
        }

        private CombatUnit FirstLivingHero()
            => _units.FirstOrDefault(u => u.Faction == Faction.Player && u.IsAlive);

        // Living enemies, closest to any living hero first, so the front of
        // a pack resolves before the back. Name breaks ties so the order is
        // never random.
        private IEnumerable<CombatUnit> EnemyOrder(Func<CombatUnit, bool> include = null)
        {
            var heroes = _units.Where(u => u.Faction == Faction.Player && u.IsAlive).ToList();
            return _units
                .Where(u => u.Faction == Faction.Enemy && u.IsAlive && (include == null || include(u)))
                .OrderBy(e => heroes.Count == 0 ? 0 : heroes.Min(h => h.Position.ManhattanDistance(e.Position)))
                .ThenBy(e => e.DisplayName);
        }

        private void Finish()
        {
            Outcome = EvaluateOutcome();
            if (Outcome != BattleOutcome.InProgress)
                CurrentUnit = null;
        }

        private BattleOutcome EvaluateOutcome()
        {
            bool anyPlayerAlive = _units.Any(u => u.Faction == Faction.Player && u.IsAlive);
            bool anyEnemyAlive = _units.Any(u => u.Faction == Faction.Enemy && u.IsAlive);

            if (!anyPlayerAlive) return BattleOutcome.PlayerDefeat;
            if (!anyEnemyAlive && EndOnEnemiesDefeated) return BattleOutcome.PlayerVictory;
            return BattleOutcome.InProgress;
        }

        public void ReevaluateOutcome()
        {
            if (Outcome != BattleOutcome.InProgress) return;
            Outcome = EvaluateOutcome();
            if (Outcome != BattleOutcome.InProgress) CurrentUnit = null;
        }

        /// <summary>
        /// Forecast for the turn tracker, restricted to units `include`
        /// accepts: what's left of this phase (current unit first), then
        /// the rest of this round, then whole further rounds (heroes, then
        /// enemies) until at least `minEntries` slots exist. Later rounds
        /// are a projection of who's alive and where they stand right now.
        /// Enumerate it right away; it reads live state.
        /// </summary>
        public IEnumerable<TurnSlot> GetProjectedTurnOrder(Func<CombatUnit, bool> include, int minEntries)
        {
            if (Outcome != BattleOutcome.InProgress) yield break;

            int count = 0;
            var heroes = _units.Where(u => u.Faction == Faction.Player && u.IsAlive && include(u)).ToList();

            if (Phase == TurnPhase.Hero)
            {
                // Heroes who can still act, the selected one first, then the enemies' turn.
                var acting = heroes.Where(h => h == CurrentUnit || h.Actions.CanAct)
                                   .OrderBy(h => h == CurrentUnit ? 0 : 1).ToList();
                foreach (var hero in acting) { yield return new TurnSlot(hero, 0); count++; }
                foreach (var enemy in EnemyOrder(include)) { yield return new TurnSlot(enemy, 0); count++; }
            }
            else
            {
                if (CurrentUnit != null && include(CurrentUnit)) { yield return new TurnSlot(CurrentUnit, 0); count++; }
                foreach (var enemy in _enemyQueue)
                {
                    if (!enemy.IsAlive || !include(enemy)) continue;
                    yield return new TurnSlot(enemy, 0);
                    count++;
                }
            }

            var nextEnemies = EnemyOrder(include).ToList();
            if (heroes.Count == 0 && nextEnemies.Count == 0) yield break;

            // Always the full next round, then more whole rounds only if
            // that still leaves us short of minEntries.
            for (int round = 1; round == 1 || count < minEntries; round++)
            {
                foreach (var hero in heroes) { yield return new TurnSlot(hero, round); count++; }
                foreach (var enemy in nextEnemies) { yield return new TurnSlot(enemy, round); count++; }
            }
        }
    }
}
