using System;
using System.Collections.Generic;
using Game.Player.Recorders;
using Ships;

namespace Game.Runs
{
    /// <summary>
    /// Which ships the player has earned: every faction ship a record shows the player killing as an
    /// elite earns that faction's unlocked ship. There is no save file; the store is the record. An
    /// abandoned record earns nothing. <see cref="Unlocked"/> fires at the kill itself, once per chassis
    /// ever. In the editor, <see cref="UnlockAllPref"/> earns every ship without touching the store.
    /// The arc and its rulings: https://github.com/amindell11/astronomical-home/issues/1318
    /// </summary>
    public sealed class ShipUnlocks
    {
        public const string UnlockAllPref = "ShipUnlocks.UnlockAll";

        private readonly string recordsPath;
        private readonly IReadOnlyDictionary<string, string> unlockedByElite;
        private readonly HashSet<string> earned = new();

        public event Action<ShipUnlocked> Unlocked;

        public ShipUnlocks(string recordsPath, IReadOnlyDictionary<string, string> unlockedByElite)
        {
            this.recordsPath = recordsPath;
            this.unlockedByElite = unlockedByElite;
        }

        public void Read()
        {
            earned.Clear();
            var records = Scoreboard.ReadRecords(recordsPath);
            if (records == null) return;

            foreach (var record in records)
            {
                if (record.spawns == null) continue;
                foreach (var spawn in record.spawns)
                    if (spawn.elite && spawn.killedByPlayer)
                        earned.Add(UnlockedShip(spawn.loadout.chassis));
            }
        }

        public bool Earned(Ship chassis) => UnlockAll || earned.Contains(chassis.name);

        public void Claim(SpawnLog.EliteKill kill)
        {
            var ship = UnlockedShip(kill.Chassis);
            var first = earned.Count == 0;
            if (earned.Add(ship))
                Unlocked?.Invoke(new ShipUnlocked(kill.SectorIndex, kill.FactionId, ship, first));
        }

        // Unmapped names are player ships a record stores.
        private string UnlockedShip(string eliteChassis) =>
            unlockedByElite.TryGetValue(eliteChassis, out var ship) ? ship : eliteChassis;

#if UNITY_EDITOR
        private static bool UnlockAll => UnityEditor.EditorPrefs.GetBool(UnlockAllPref, false);
#else
        private static bool UnlockAll => false;
#endif
    }

    public readonly struct ShipUnlocked
    {
        public readonly int SectorIndex;
        public readonly string FactionId;
        public readonly string Chassis;
        public readonly bool FirstOfAny;

        public ShipUnlocked(int sectorIndex, string factionId, string chassis, bool firstOfAny)
        {
            SectorIndex = sectorIndex;
            FactionId = factionId;
            Chassis = chassis;
            FirstOfAny = firstOfAny;
        }
    }
}
