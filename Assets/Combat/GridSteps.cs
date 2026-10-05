using System;
using System.Collections.Generic;
using DungeonTower.Core;

namespace DungeonTower.Combat
{
    /// <summary>
    /// Eight-way movement with a no-corner-cutting rule, shared by every
    /// piece of pathing code (reachable tiles, walking paths, enemy
    /// distance fields).
    ///
    /// Costs are in HALF-STEPS so everything stays integer: a straight
    /// step costs 2 and a diagonal 3 (i.e. 1.5), and a unit's Move Range
    /// of N is a budget of N * 2. Note that means Move Range 1 can't
    /// step diagonally (3 > 2), Move Range 2 gets one diagonal, etc.
    ///
    /// A diagonal step is only legal if the destination AND both tiles
    /// beside the diagonal are open — walls, blocking battlefield
    /// objects (via the IWalkableMap) and any `blocked` tiles (other
    /// units) all count, so nobody slips around a corner.
    /// </summary>
    public static class GridSteps
    {
        public const int StraightCost = 2;
        public const int DiagonalCost = 3;
        public const int NoBudget = int.MaxValue;

        public static int Budget(int moveRange) => moveRange * StraightCost;

        private static readonly (int dx, int dy)[] Straights = { (1, 0), (-1, 0), (0, 1), (0, -1) };
        private static readonly (int dx, int dy)[] Diagonals = { (1, 1), (1, -1), (-1, 1), (-1, -1) };

        public static IEnumerable<(GridPosition tile, int cost)> From(
            GridPosition from, IWalkableMap map, HashSet<GridPosition> blocked = null)
        {
            foreach (var (dx, dy) in Straights)
            {
                var next = new GridPosition(from.X + dx, from.Y + dy);
                if (IsOpen(next, map, blocked)) yield return (next, StraightCost);
            }

            foreach (var (dx, dy) in Diagonals)
            {
                var next = new GridPosition(from.X + dx, from.Y + dy);
                if (!IsOpen(next, map, blocked)) continue;

                // No cutting corners: both tiles beside the diagonal must be open too.
                if (!IsOpen(new GridPosition(from.X + dx, from.Y), map, blocked)) continue;
                if (!IsOpen(new GridPosition(from.X, from.Y + dy), map, blocked)) continue;

                yield return (next, DiagonalCost);
            }
        }

        private static bool IsOpen(GridPosition tile, IWalkableMap map, HashSet<GridPosition> blocked)
            => map.InBounds(tile) && map.IsWalkable(tile) && (blocked == null || !blocked.Contains(tile));

        // Dijkstra from `origin`. Returns the cheapest cost (in half-steps)
        // to every tile within `budget`. Pass `cameFrom` to also get the
        // route back to the origin; pass `stopAt` to quit as soon as that
        // tile is settled.
        public static Dictionary<GridPosition, int> CostMap(
            GridPosition origin, IWalkableMap map, HashSet<GridPosition> blocked, int budget,
            Dictionary<GridPosition, GridPosition> cameFrom = null, GridPosition? stopAt = null)
        {
            var cost = new Dictionary<GridPosition, int> { [origin] = 0 };
            var open = new SortedSet<(int cost, int x, int y)> { (0, origin.X, origin.Y) };

            while (open.Count > 0)
            {
                var first = open.Min;
                open.Remove(first);
                var current = new GridPosition(first.x, first.y);

                if (stopAt.HasValue && current.Equals(stopAt.Value)) break;

                foreach (var (next, stepCost) in From(current, map, blocked))
                {
                    int newCost = first.cost + stepCost;
                    if (newCost > budget) continue;

                    bool seen = cost.TryGetValue(next, out var known);
                    if (seen && known <= newCost) continue;
                    if (seen) open.Remove((known, next.X, next.Y));

                    cost[next] = newCost;
                    if (cameFrom != null) cameFrom[next] = current;
                    open.Add((newCost, next.X, next.Y));
                }
            }

            return cost;
        }
    }

    /// <summary>
    /// Ability and targeting range uses the SAME costs as movement: a
    /// straight tile costs 1, a diagonal 1.5, and a range of R reaches
    /// anything whose cost is at most R. (Distances are in half-steps —
    /// see GridSteps — so a diagonal is 3 and Range R is a limit of R * 2.)
    ///
    /// Unlike walking there is no corner rule here: whether a wall is
    /// in the way is LineOfSight's job, as it always was.
    /// </summary>
    public static class GridRange
    {
        // Range 1 can't reach a diagonal neighbor (1.5 > 1), exactly like
        // Move Range 1 can't step diagonally. Flip this to true to let
        // every adjacent tile, diagonals included, always count as in
        // range for melee-style abilities.
        public static bool DiagonalNeighborsAlwaysInRange = false;

        public static int StepDistance(this GridPosition from, GridPosition to)
        {
            int dx = Math.Abs(from.X - to.X);
            int dy = Math.Abs(from.Y - to.Y);
            int diagonals = Math.Min(dx, dy);
            int straights = Math.Max(dx, dy) - diagonals;
            return diagonals * GridSteps.DiagonalCost + straights * GridSteps.StraightCost;
        }

        public static bool IsWithinRange(this GridPosition from, GridPosition to, int range)
        {
            if (DiagonalNeighborsAlwaysInRange && range >= 1
                && Math.Max(Math.Abs(from.X - to.X), Math.Abs(from.Y - to.Y)) <= 1)
                return true;

            return from.StepDistance(to) <= range * GridSteps.StraightCost;
        }

        // Every tile within `radius` of `center` (an octagon), center included.
        public static List<GridPosition> Within(GridPosition center, int radius)
        {
            var tiles = new List<GridPosition>();
            for (int dx = -radius; dx <= radius; dx++)
                for (int dy = -radius; dy <= radius; dy++)
                {
                    var tile = new GridPosition(center.X + dx, center.Y + dy);
                    if (center.IsWithinRange(tile, radius)) tiles.Add(tile);
                }
            return tiles;
        }
    }
}
