using System;
using System.Collections.Generic;
using System.Globalization;
using Game.Runs;

namespace Game.Moments
{
    public sealed class RunStarted : IRunMoment
    {
        public const string FirstLaunchSlot = "run-start.first-launch";
        public const string AfterBestSlot = "run-start.after-best";
        public const string AfterDeathSlot = "run-start.after-death";
        public const string AfterExtractionSlot = "run-start.after-extraction";

        public static readonly IReadOnlyList<string> AllSlots =
            new[] { FirstLaunchSlot, AfterDeathSlot, AfterExtractionSlot, AfterBestSlot };

        public int CloneNumber { get; }
        public PastRuns.LastRun? Last { get; }
        public bool FirstLaunch => Last == null;
        public string Slot { get; }
        public IReadOnlyDictionary<string, string> Facts { get; }

        public RunStarted(PastRuns past)
        {
            if (past == null) throw new ArgumentNullException(nameof(past));
            CloneNumber = past.Count + 1;
            Last = past.Last;
            Slot = past.Last is not { } last ? FirstLaunchSlot
                : last.WasBest ? AfterBestSlot
                : last.Died ? AfterDeathSlot
                : AfterExtractionSlot;
            Facts = new Dictionary<string, string>
            {
                ["clone"] = CloneNumber.ToString(CultureInfo.InvariantCulture),
                ["last_sector"] = past.Last is { } run ? run.Sector.ToString(CultureInfo.InvariantCulture) : string.Empty,
                ["last_killed_by"] = past.Last?.KillingBlowKind ?? string.Empty,
            };
        }
    }
}
