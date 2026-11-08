namespace Game.Runs
{
    /// <summary>
    /// What the run-record store says about the runs before this one: how many there were and how the
    /// last of them went. An unreadable store reads as no past runs; its reader has logged why.
    /// </summary>
    public sealed class PastRuns
    {
        public readonly struct LastRun
        {
            public readonly bool Died;
            /// <summary>Empty for an extraction.</summary>
            public readonly string KillingBlowKind;
            /// <summary>The 1-based number of the sector the run ended in.</summary>
            public readonly int Sector;
            /// <summary>It ranked first among every run up to and including it.</summary>
            public readonly bool WasBest;

            public LastRun(bool died, string killingBlowKind, int sector, bool wasBest)
            {
                Died = died;
                KillingBlowKind = killingBlowKind;
                Sector = sector;
                WasBest = wasBest;
            }
        }

        /// <summary>Readable run-record-v2 runs in the store.</summary>
        public int Count { get; }

        /// <summary>Null when <see cref="Count"/> is 0.</summary>
        public LastRun? Last { get; }

        private PastRuns(int count, LastRun? last)
        {
            Count = count;
            Last = last;
        }

        public static PastRuns Read(string path)
        {
            var runs = Scoreboard.ReadRuns(path);
            if (runs == null || runs.Count == 0) return new PastRuns(0, null);

            var (record, row) = runs[runs.Count - 1];
            var earlier = runs.GetRange(0, runs.Count - 1).ConvertAll(run => run.Row);
            var died = record.outcome == RunRecord.Died;
            // GameHost.AppendRunRecord counts the sector a death happened in as not cleared.
            var sector = died ? record.sectorsCleared + 1 : record.sectorsCleared;
            var wasBest = new Scoreboard(earlier, row).Rank == 1;
            return new PastRuns(runs.Count, new LastRun(died, record.killingBlow.kind ?? string.Empty, sector, wasBest));
        }
    }
}
