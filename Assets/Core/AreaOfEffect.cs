using System;
using System.Collections.Generic;

namespace DungeonTower.Core
{
    /// <summary>
    /// Pure grid geometry for turning an ability's AreaShape/AreaRadius
    /// into the actual set of tiles it hits, given where the caster
    /// stands and which tile the player aimed at. No map/wall awareness —
    /// a blast/line/cone reaches through walls same as it reaches
    /// through units right now; threading DungeonMap through here is a
    /// follow-up if that turns out to matter in play.
    ///
    /// Line and Cone snap the aim direction to the nearest of the four
    /// cardinal directions (no diagonals, matching how movement/attack
    /// range already work everywhere else via ManhattanDistance) and
    /// radiate away from the CASTER, independent of exactly how far the
    /// player clicked within Range — AreaRadius is the beam/cone's own
    /// length, separate from how far away the player was allowed to aim.
    /// Clicking the caster's own tile (zero displacement) defaults to
    /// facing +X — an arbitrary placeholder for an edge case that
    /// shouldn't come up in practice.
    /// </summary>
    public static class AreaOfEffect
    {
        public static List<GridPosition> GetAffectedTiles(
            GridPosition casterPosition, GridPosition impactTile, AttackShape shape, int areaRadius)
        {
            switch (shape)
            {
                case AttackShape.Blast:
                    return BlastTiles(impactTile, areaRadius);
                case AttackShape.Line:
                    return LineTiles(casterPosition, impactTile, areaRadius);
                case AttackShape.Cone:
                    return ConeTiles(casterPosition, impactTile, areaRadius);
                default: // Single
                    return new List<GridPosition> { impactTile };
            }
        }

        // Diamond-shaped radius (Manhattan distance) around the impact
        // tile — matches the Manhattan metric used for movement and
        // attack range everywhere else.
        private static List<GridPosition> BlastTiles(GridPosition center, int radius)
        {
            var tiles = new List<GridPosition>();
            for (int dx = -radius; dx <= radius; dx++)
            {
                int remaining = radius - Math.Abs(dx);
                for (int dy = -remaining; dy <= remaining; dy++)
                    tiles.Add(new GridPosition(center.X + dx, center.Y + dy));
            }
            return tiles;
        }

        // A 1-tile-wide beam of `length` tiles, starting adjacent to the
        // caster and extending in the aimed direction.
        private static List<GridPosition> LineTiles(GridPosition caster, GridPosition aimedAt, int length)
        {
            var (dx, dy) = CardinalDirection(caster, aimedAt);
            var tiles = new List<GridPosition>();
            for (int step = 1; step <= length; step++)
                tiles.Add(new GridPosition(caster.X + dx * step, caster.Y + dy * step));
            return tiles;
        }

        // A widening triangle in the aimed direction: 1 tile wide at
        // step 1, 3 wide at step 2, 5 wide at step 3, etc.
        private static List<GridPosition> ConeTiles(GridPosition caster, GridPosition aimedAt, int length)
        {
            var (dx, dy) = CardinalDirection(caster, aimedAt);
            // The axis perpendicular to travel — where the cone widens.
            int perpX = dy != 0 ? 1 : 0;
            int perpY = dx != 0 ? 1 : 0;

            var tiles = new List<GridPosition>();
            for (int step = 1; step <= length; step++)
            {
                int centerX = caster.X + dx * step;
                int centerY = caster.Y + dy * step;
                for (int spread = -(step - 1); spread <= step - 1; spread++)
                    tiles.Add(new GridPosition(centerX + perpX * spread, centerY + perpY * spread));
            }
            return tiles;
        }

        // Snaps whichever axis has the larger displacement to +1/-1; a
        // tie (a pure diagonal click) resolves to horizontal.
        private static (int dx, int dy) CardinalDirection(GridPosition from, GridPosition to)
        {
            int dx = to.X - from.X;
            int dy = to.Y - from.Y;
            return Math.Abs(dx) >= Math.Abs(dy)
                ? (Math.Sign(dx) == 0 ? 1 : Math.Sign(dx), 0)
                : (0, Math.Sign(dy));
        }
    }
}
