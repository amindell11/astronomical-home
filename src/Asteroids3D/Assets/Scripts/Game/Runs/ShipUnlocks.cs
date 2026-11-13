using System;
using System.Collections.Generic;
using Game.Player;
using Ships;

namespace Game.Runs
{
    /// <summary>
    /// Which ships the player has earned: every elite chassis a died or extracted run record shows
    /// the player killing. There is no save file; the store is the record. An abandoned record
    /// earns nothing. <see cref="Unlocked"/> fires at the kill itself, once per chassis ever.
    /// In the editor, <see cref="UnlockAllPref"/> earns every ship without touching the store.
    /// The arc and its rulings: https://github.com/amindell11/astronomical-home/issues/1318
    /// </summary>
    public sealed class ShipUnlocks
    {
        public const string UnlockAllPref = "ShipUnlocks.UnlockAll";

        private readonly string recordsPath;
        private readonly HashSet<string> earned = new();

        public event Action<ShipUnlocked> Unlocked;

        public ShipUnlocks(string recordsPath) => this.recordsPath = recordsPath;

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
                        earned.Add(spawn.loadout.chassis);
            }
        }

        public bool Earned(Ship chassis) => UnlockAll || earned.Contains(chassis.name);

        public void Claim(SpawnLog.EliteKill kill)
        {
            var first = earned.Count == 0;
            if (earned.Add(kill.Chassis))
                Unlocked?.Invoke(new ShipUnlocked(kill.SectorIndex, kill.FactionId, kill.Chassis, first));
        }

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
