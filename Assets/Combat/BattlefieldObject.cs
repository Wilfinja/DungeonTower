using System.Collections.Generic;
using DungeonTower.Core;

namespace DungeonTower.Combat
{
    /// <summary>
    /// A placed, non-unit thing on the grid: a totem, a trap, a wall, or
    /// an obscuring cloud. Plain mutable data — BattleController and
    /// BattlefieldObjectRegistry own all the behavior; this just holds
    /// state. Built once (in BattleController.SpawnBattlefieldObject)
    /// from whichever ability placed it, and mutated afterward
    /// (RemainingDuration decays, CurrentHp drops).
    /// </summary>
    public sealed class BattlefieldObject
    {
        public string Name;

        // Whoever placed it, and the faction that owns it. OwnerUnit is
        // non-null only when EndsIfOwnerMoves is true (Aura) — for an
        // independent object (Totem/Trap/Wall/Cloud) it's null even
        // though OwnerFaction is still set, since "who gets buffed" and
        // "which specific unit anchors this" are different questions.
        public CombatUnit OwnerUnit;
        public Faction OwnerFaction;

        // The tile(s) it occupies — one for a Totem/Trap/Aura, several
        // for a Wall (Line) or Cloud (Blast/Ring/Cross).
        public IReadOnlyList<GridPosition> Tiles;

        // Ticks down once per ROUND (not per unit turn) in
        // BattleController.TickBattlefieldObjects. int.MaxValue means
        // "no duration cap" — it only ends via destruction, being
        // consumed, or its owner moving.
        public int RemainingDuration = int.MaxValue;

        // Null = indestructible. Otherwise this object can be damaged
        // like a unit; it's removed when CurrentHp reaches 0.
        public int? MaxHp;
        public int CurrentHp;

        public bool BlocksMovement;
        public bool BlocksLineOfSight;
        public bool IsHidden;

        public SummonTriggerMode TriggerMode;
        public int AuraRadius;
        public bool AffectsAllies;
        public bool IgnoreOwnerFaction;
        public bool ConsumedAfterTrigger;

        // What OnRoundTick/OnEntry actually applies — copied from the
        // placing ability's own Statuses list at creation time.
        public IReadOnlyList<StatusApplication> Statuses;
    }
}
