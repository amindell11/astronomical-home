using System.Collections.Generic;

namespace Game.Runs
{
    /// <summary>
    /// Which enemy ships the player has flown against: every chassis a run record lists among its spawns.
    /// There is no save file; the store is the record. A meeting in the current run counts from
    /// <see cref="Meet"/>, before any record of it is written. In the editor, <see cref="MeetAgainPref"/>
    /// makes every ship new again without touching the store.
    /// </summary>
    public sealed class EnemiesMet
    {
        public const string MeetAgainPref = "EnemiesMet.MeetAgain";

        private readonly HashSet<string> met = new();

        public static EnemiesMet Read(string recordsPath)
        {
            var enemies = new EnemiesMet();
            var records = Scoreboard.ReadRecords(recordsPath);
            if (records == null) return enemies;

            foreach (var record in records)
            {
                if (record.spawns == null) continue;
                foreach (var spawn in record.spawns)
                    enemies.met.Add(spawn.loadout.chassis);
            }
            return enemies;
        }

        /// <summary>Records the meeting; true when it is the first with this chassis.</summary>
        public bool Meet(string chassis) => met.Add(chassis) || MeetAgain;

#if UNITY_EDITOR
        private static bool MeetAgain => UnityEditor.EditorPrefs.GetBool(MeetAgainPref, false);
#else
        private static bool MeetAgain => false;
#endif
    }
}
