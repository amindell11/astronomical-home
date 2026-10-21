using System.Collections.Generic;
using Damage;
using Game.Player;
using Ships.Registry;
using UnityEngine;

namespace UI.Screens
{
    /// <summary>
    /// The death recap's burst chart as data: the life's last <see cref="WindowSeconds"/>, ending at
    /// the killing blow (the ledger's last hit), cut into equal time buckets that each stack the
    /// damage per source — a ledger row. The heaviest sources in the window get their own series,
    /// heaviest first and lowest in every stack; the rest stack as one trailing "other" series.
    /// </summary>
    internal sealed class DamageBurst
    {
        public const float WindowSeconds = 10f;
        public const int BucketCount = 10;
        private const float BucketSeconds = WindowSeconds / BucketCount;

        private readonly float[,] amounts;

        /// <summary>The named series' rows; when <see cref="HasOther"/>, series <c>Sources.Count</c> is "other".</summary>
        public IReadOnlyList<DamageLedger.Row> Sources { get; }
        public bool HasOther { get; }
        public int KillingSeries { get; }

        /// <summary>The tallest bucket's stacked total.</summary>
        public float Peak { get; }

        public int SeriesCount => Sources.Count + (HasOther ? 1 : 0);

        public float Amount(int bucket, int series) => amounts[bucket, series];

        private DamageBurst(List<DamageLedger.Row> sources, bool hasOther, float[,] amounts, int killingSeries,
            float peak)
        {
            Sources = sources;
            HasOther = hasOther;
            this.amounts = amounts;
            KillingSeries = killingSeries;
            Peak = peak;
        }

        /// <summary>Needs a ledger with at least one hit.</summary>
        public static DamageBurst From(DamageLedger ledger, int maxNamedSources)
        {
            var rows = ledger.Rows;
            var hits = ledger.Hits;
            var rowOf = new Dictionary<(ShipId, DamageKind), int>(rows.Count);
            for (var r = 0; r < rows.Count; r++)
                rowOf[(rows[r].AttackerId, rows[r].Kind)] = r;

            var killingBlow = hits[hits.Count - 1];
            var start = killingBlow.Time - WindowSeconds;
            var rowTotals = new float[rows.Count];
            for (var i = 0; i < hits.Count; i++)
                if (hits[i].Time >= start)
                    rowTotals[rowOf[(hits[i].AttackerId, hits[i].Kind)]] += hits[i].Amount;

            var ranked = new List<int>();
            for (var r = 0; r < rows.Count; r++)
                if (rowTotals[r] > 0f) ranked.Add(r);
            ranked.Sort((a, b) =>
            {
                var byTotal = rowTotals[b].CompareTo(rowTotals[a]);
                return byTotal != 0 ? byTotal : a.CompareTo(b);
            });

            var named = Mathf.Min(ranked.Count, maxNamedSources);
            var hasOther = ranked.Count > named;
            var sources = new List<DamageLedger.Row>(named);
            var seriesOfRow = new int[rows.Count];
            for (var rank = 0; rank < ranked.Count; rank++)
            {
                seriesOfRow[ranked[rank]] = Mathf.Min(rank, named);
                if (rank < named) sources.Add(rows[ranked[rank]]);
            }

            var amounts = new float[BucketCount, named + (hasOther ? 1 : 0)];
            var bucketTotals = new float[BucketCount];
            for (var i = 0; i < hits.Count; i++)
            {
                var hit = hits[i];
                if (hit.Time < start) continue;
                var bucket = Mathf.Min((int)((hit.Time - start) / BucketSeconds), BucketCount - 1);
                amounts[bucket, seriesOfRow[rowOf[(hit.AttackerId, hit.Kind)]]] += hit.Amount;
                bucketTotals[bucket] += hit.Amount;
            }

            var killingSeries = seriesOfRow[rowOf[(killingBlow.AttackerId, killingBlow.Kind)]];
            return new DamageBurst(sources, hasOther, amounts, killingSeries, Mathf.Max(bucketTotals));
        }
    }
}
