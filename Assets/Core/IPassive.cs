using System.Collections.Generic;

namespace DungeonTower.Core
{

    public interface IPassive
    {
        string Name { get; }

        int passivempRegen { get; }

        int passivehpRegen { get; }
    }
}
