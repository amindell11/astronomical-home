using System;
using System.Collections.Generic;
using System.Text;
using Damage;
using Game.Player;
using Game.Runs;
using UnityEngine;
using UnityEngine.UI;
using static UI.Screens.ScreenWidgets;

namespace UI.Screens
{
    /// <summary>
    /// Post-death recap panel rendered from the damage ledger, the run tally and the scoreboard: what
    /// killed you, this run's kills, time survived and rank, the top five runs, a burst chart of the
    /// life's last seconds, and what hurt you this life, aggregated per source. Code-built (no prefab)
    /// so headless paths never touch it; <see cref="Game.GameHost"/> creates it for the recap hold
    /// between death and unload.
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    public class DeathRecapScreen : MonoBehaviour
    {
        private const int MaxRows = 6;
        private const float ChartWidth = 360f;
        private const float ChartHeight = 120f;
        private const float KillingBlowMarkHeight = 4f;

        private static readonly Color OtherColor = new(0.55f, 0.57f, 0.62f);

        // One per named burst series; the palette's size caps how many sources the chart names.
        private static readonly Color[] SourceColors =
        {
            new(0.95f, 0.62f, 0.22f),
            new(0.35f, 0.68f, 0.98f),
            new(0.55f, 0.85f, 0.38f),
            new(0.82f, 0.48f, 0.92f),
            new(0.95f, 0.88f, 0.35f),
        };

        public static DeathRecapScreen Create(Transform parent) =>
            CreateOverlay<DeathRecapScreen>(parent, "DeathRecapScreen");

        public void Show(in DamageInfo killingBlow, DamageLedger ledger, IRunTally tally, Scoreboard board,
            Action onContinue)
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var panel = AddPanel(transform);

            AddText(panel, "Title", "SHIP DESTROYED", font, 34,
                new Color(1f, 0.45f, 0.35f), FontStyle.Bold);
            AddText(panel, "Cause", CauseLine(killingBlow, ledger.Rows), font, 22, Color.white);
            AddText(panel, "Tally", TallyBlock(tally), font, 22, Color.white);
            AddScoreboard(panel, font, board);
            if (ledger.Hits.Count > 0)
                AddBurstChart(panel, DamageBurst.From(ledger, SourceColors.Length), font);
            AddText(panel, "Rows", RowsBlock(ledger.Rows), font, 17, DetailColor);

            AddContinueButton(panel, font, onContinue);
        }

        internal static string CauseLine(in DamageInfo killingBlow, IReadOnlyList<DamageLedger.Row> rows)
        {
            if (killingBlow.Kind == DamageKind.Collision)
                return "You flew into an asteroid.";

            var name = SourceNameFor(killingBlow, rows);
            return name != null
                ? $"Destroyed by {name} — {DamageLedger.DescribeKind(killingBlow.Kind)}."
                : $"Destroyed by {DamageLedger.DescribeKind(killingBlow.Kind)}.";
        }

        internal static string RowsBlock(IReadOnlyList<DamageLedger.Row> rows)
        {
            if (rows == null || rows.Count == 0) return "No damage recorded this life.";

            var sorted = new List<DamageLedger.Row>(rows);
            sorted.Sort((a, b) => b.Total.CompareTo(a.Total));

            var sb = new StringBuilder("Damage taken this life:\n");
            var shown = Mathf.Min(sorted.Count, MaxRows);
            for (var i = 0; i < shown; i++)
            {
                var row = sorted[i];
                sb.Append($"\n{SourceLabel(row)}: {row.Total:0} dmg ({row.Hits} {(row.Hits == 1 ? "hit" : "hits")})");
            }
            if (sorted.Count > shown)
                sb.Append($"\n… and {sorted.Count - shown} more");
            return sb.ToString();
        }

        internal static string LegendBlock(DamageBurst burst)
        {
            var sb = new StringBuilder();
            for (var s = 0; s < burst.SeriesCount; s++)
            {
                var label = s < burst.Sources.Count ? SourceLabel(burst.Sources[s]) : "other";
                if (s == burst.KillingSeries) label += " (killing blow)";
                if (s > 0) sb.Append('\n');
                sb.Append($"<color=#{ColorUtility.ToHtmlStringRGB(SeriesColor(burst, s))}>■ {label}</color>");
            }
            return sb.ToString();
        }

        private static string SourceLabel(DamageLedger.Row row) =>
            $"{row.SourceName} — {DamageLedger.DescribeKind(row.Kind)}";

        private static Color SeriesColor(DamageBurst burst, int series) =>
            series < burst.Sources.Count ? SourceColors[series] : OtherColor;

        // Bars and segments are anchor-placed boxes, so the panel's layout only sizes the chart.
        private static void AddBurstChart(Transform parent, DamageBurst burst, Font font)
        {
            AddText(parent, "BurstTitle",
                $"Damage taken in the last {DamageBurst.WindowSeconds:0} s (white mark: killing blow):",
                font, 17, DetailColor);

            var chart = new GameObject("BurstChart", typeof(RectTransform));
            chart.transform.SetParent(parent, false);
            var size = chart.AddComponent<LayoutElement>();
            size.preferredWidth = ChartWidth;
            size.preferredHeight = ChartHeight;

            for (var b = 0; b < DamageBurst.BucketCount; b++)
            {
                var bar = AddBox(chart.transform, $"Bucket{b}",
                    new Vector2((float)b / DamageBurst.BucketCount, 0f),
                    new Vector2((b + 1f) / DamageBurst.BucketCount, 1f));
                bar.offsetMin = new Vector2(2f, 0f);
                bar.offsetMax = new Vector2(-2f, 0f);

                var top = 0f;
                for (var s = 0; s < burst.SeriesCount; s++)
                {
                    var height = burst.Amount(b, s) / burst.Peak;
                    if (height <= 0f) continue;
                    var segment = AddBox(bar, $"Series{s}", new Vector2(0f, top), new Vector2(1f, top + height));
                    segment.gameObject.AddComponent<Image>().color = SeriesColor(burst, s);
                    top += height;
                }

                if (b < DamageBurst.BucketCount - 1) continue;
                var mark = AddBox(bar, "KillingBlow", new Vector2(0f, top), new Vector2(1f, top));
                mark.offsetMax = new Vector2(0f, KillingBlowMarkHeight);
                mark.gameObject.AddComponent<Image>().color = Color.white;
            }

            AddText(parent, "BurstLegend", LegendBlock(burst), font, 15, DetailColor);
        }

        private static RectTransform AddBox(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        private static string SourceNameFor(in DamageInfo killingBlow, IReadOnlyList<DamageLedger.Row> rows)
        {
            if (rows == null) return null;
            for (var i = 0; i < rows.Count; i++)
                if (rows[i].AttackerId == killingBlow.AttackerId && rows[i].Kind == killingBlow.Kind)
                    return rows[i].SourceName;
            return null;
        }
    }
}
