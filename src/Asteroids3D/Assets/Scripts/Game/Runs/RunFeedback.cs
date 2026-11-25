using System;
using UnityEngine;

namespace Game.Runs
{
    /// <summary>
    /// A playtester's note on one run, as one JSON line beside the run records. <see cref="runEndedUtc"/>
    /// is that run's <see cref="RunRecord.endedUtc"/>, which pairs the two lines.
    /// </summary>
    [Serializable]
    public struct RunFeedback
    {
        public string schema;
        public string runEndedUtc;
        public string writtenUtc;
        public string outcome;
        public string sector;
        public BuildIdentity buildIdentity;
        public string note;

        public const string SchemaId = "run-feedback-v1";

        public static RunFeedback Of(in RunRecord run, DateTime writtenUtc, string note) => new()
        {
            schema = SchemaId,
            runEndedUtc = run.endedUtc,
            writtenUtc = writtenUtc.ToString(RunRecord.EndedUtcFormat, System.Globalization.CultureInfo.InvariantCulture),
            outcome = run.outcome,
            sector = run.sector,
            buildIdentity = run.buildIdentity,
            note = note,
        };

        public string ToJsonLine() => JsonUtility.ToJson(this);
    }
}
