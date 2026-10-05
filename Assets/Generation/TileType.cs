namespace DungeonTower.Generation
{
    // Floor and Wall keep their original values (0 and 1) so anything
    // already serialized with them stays valid; new types are appended.
    public enum TileType
    {
        Floor,
        Wall,
        Door,   // walkable; blocks sight until something steps onto it
        Exit    // stairs down to the next floor
    }
}
