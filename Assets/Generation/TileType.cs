namespace DungeonTower.Generation
{
    // Floor and Wall keep their original values (0 and 1) so anything
    // already serialized with them stays valid; new types are appended.
    public enum TileType
    {
        Floor,
        Wall,
        Door,   // closed: blocks sight and movement; open: walkable and see-through
        Exit    // stairs down to the next floor
    }
}
