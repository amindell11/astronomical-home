#if UNITY_EDITOR || DEBUG
using System;
using System.IO;
using System.Text;
using AI.States;
using AI.Utility;
using UnityEngine;

namespace AI.Debug
{
    /// <summary>
    /// Logs AI utility decisions to JSONL files for offline analysis.
    /// Attach to any GameObject with an AICommander. Captures per-tick utility scores,
    /// factor breakdowns, context snapshots, and state transitions.
    /// </summary>
    public class UtilityLogger : MonoBehaviour
    {
        [Header("Logging")]
        [Tooltip("Log a snapshot every N fixed-update ticks (1 = every tick, 10 = every 10th)")]
        [SerializeField] private int tickInterval = 5;

        [Tooltip("Always log on state transitions regardless of tick interval")]
        [SerializeField] private bool alwaysLogTransitions = true;

        [Tooltip("Include per-state factor breakdowns (verbose but useful)")]
        [SerializeField] private bool includeFactorBreakdowns = true;

        [Tooltip("Subfolder under Application.persistentDataPath/AILogs/")]
        [SerializeField] private string sessionTag = "";

        [Header("Buffer")]
        [Tooltip("Flush to disk after this many entries")]
        [SerializeField] private int flushThreshold = 50;

        private AICommander commander;
        private UtilitySelector selector;
        private StreamWriter writer;
        private StringBuilder lineBuffer = new();
        private string shipLabel;
        private int tickCounter;
        private int entriesBuffered;
        private bool pendingTransition;
        private State transitionFrom;
        private State transitionTo;
        private float sessionStartTime;

        private void Start()
        {
            commander = GetComponent<AICommander>();
            if (!commander)
            {
                enabled = false;
                return;
            }

            selector = commander.UtilitySelector;
            if (!selector)
            {
                enabled = false;
                return;
            }

            var ship = GetComponent<Ships.Ship>();
            shipLabel = ship
                ? $"Team{ship.teamNumber}_{ship.Id.Value}"
                : $"Ship_{gameObject.GetInstanceID()}";

            sessionStartTime = Time.time;
            OpenLogFile();

            selector.OnStateTransition += OnTransition;
        }

        private void OnDestroy()
        {
            if (selector)
                selector.OnStateTransition -= OnTransition;
            CloseLogFile();
        }

        private void FixedUpdate()
        {
            if (!selector || selector.RegisteredStates == null || selector.RegisteredStates.Count == 0)
                return;

            tickCounter++;

            var isTransitionTick = pendingTransition && alwaysLogTransitions;
            var isIntervalTick = tickCounter % tickInterval == 0;

            if (!isTransitionTick && !isIntervalTick)
                return;

            WriteEntry(isTransitionTick);
            pendingTransition = false;
        }

        private void OnTransition(State from, State to)
        {
            pendingTransition = true;
            transitionFrom = from;
            transitionTo = to;
        }

        private void WriteEntry(bool isTransition)
        {
            if (writer == null) return;

            var ctx = selector.Context;
            if (ctx == null) return;

            lineBuffer.Clear();
            lineBuffer.Append('{');

            // Timestamp & identity
            AppendField("t", (Time.time - sessionStartTime));
            lineBuffer.Append(',');
            AppendFieldStr("ship", shipLabel);
            lineBuffer.Append(',');
            AppendFieldStr("state", selector.CurrentStateName);
            lineBuffer.Append(',');
            AppendField("tick", tickCounter);

            // Context snapshot
            lineBuffer.Append(",\"ctx\":{");
            AppendField("hp", ctx.HealthPct);
            lineBuffer.Append(',');
            AppendField("shield", ctx.ShieldPct);
            lineBuffer.Append(',');
            AppendField("inCombat", ctx.InCombat);
            lineBuffer.Append(',');
            AppendField("enemyDist", ctx.Enemy ? ctx.VectorToEnemy.magnitude : -1f);
            lineBuffer.Append(',');
            AppendField("enemyHp", ctx.Enemy ? ctx.EnemyHealthPct : -1f);
            lineBuffer.Append(',');
            AppendField("enemyShield", ctx.Enemy ? ctx.EnemyShieldPct : -1f);
            lineBuffer.Append(',');
            AppendField("los", ctx.Enemy && ctx.LineOfSightToEnemy);
            lineBuffer.Append(',');
            AppendField("enemies", ctx.NearbyEnemyCount);
            lineBuffer.Append(',');
            AppendField("friends", ctx.NearbyFriendCount);
            lineBuffer.Append(',');
            AppendField("closing", ctx.ClosingSpeed);
            lineBuffer.Append(',');
            AppendField("missile", ctx.IncomingMissile);
            lineBuffer.Append(',');
            AppendField("angle", ctx.AngleToEnemy);
            lineBuffer.Append('}');

            // Utility scores
            var scores = selector.UtilityScores;
            if (scores != null && scores.Count > 0)
            {
                lineBuffer.Append(",\"scores\":{");
                var first = true;
                foreach (var kvp in scores)
                {
                    if (!first) lineBuffer.Append(',');
                    AppendField(kvp.Key.ToString(), kvp.Value);
                    first = false;
                }
                lineBuffer.Append('}');
            }

            // Factor breakdowns per state
            if (includeFactorBreakdowns)
            {
                lineBuffer.Append(",\"factors\":{");
                var first = true;
                foreach (var state in selector.RegisteredStates)
                {
                    var builder = state.LastBuilder;
                    if (builder?.Factors == null || builder.Factors.Count == 0) continue;

                    if (!first) lineBuffer.Append(',');
                    lineBuffer.Append('"').Append(state.Type.ToString()).Append("\":{");

                    for (var i = 0; i < builder.Factors.Count; i++)
                    {
                        if (i > 0) lineBuffer.Append(',');
                        var (name, value) = builder.Factors[i];
                        AppendField(name, value);
                    }
                    lineBuffer.Append('}');
                    first = false;
                }
                lineBuffer.Append('}');
            }

            // Transition event
            if (isTransition)
            {
                lineBuffer.Append(",\"transition\":{");
                AppendFieldStr("from", transitionFrom?.Type.ToString() ?? "None");
                lineBuffer.Append(',');
                AppendFieldStr("to", transitionTo?.Type.ToString() ?? "None");
                lineBuffer.Append('}');
            }

            lineBuffer.Append('}');

            writer.WriteLine(lineBuffer.ToString());
            entriesBuffered++;

            if (entriesBuffered >= flushThreshold)
            {
                writer.Flush();
                entriesBuffered = 0;
            }
        }

        private void AppendField(string name, float value)
        {
            lineBuffer.Append('"').Append(name).Append("\":").Append(value.ToString("F3"));
        }

        private void AppendField(string name, int value)
        {
            lineBuffer.Append('"').Append(name).Append("\":").Append(value);
        }

        private void AppendField(string name, bool value)
        {
            lineBuffer.Append('"').Append(name).Append("\":").Append(value ? "true" : "false");
        }

        private void AppendFieldStr(string name, string value)
        {
            lineBuffer.Append('"').Append(name).Append("\":\"").Append(value ?? "null").Append('"');
        }

        private void OpenLogFile()
        {
            var logDir = Path.Combine(Application.persistentDataPath, "AILogs");
            if (!string.IsNullOrEmpty(sessionTag))
                logDir = Path.Combine(logDir, sessionTag);

            Directory.CreateDirectory(logDir);

            var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            var filePath = Path.Combine(logDir, $"{shipLabel}_{timestamp}.jsonl");

            writer = new StreamWriter(filePath, append: false, Encoding.UTF8) { AutoFlush = false };

            UnityEngine.Debug.Log($"[UtilityLogger] Logging to: {filePath}");
        }

        private void CloseLogFile()
        {
            if (writer == null) return;
            writer.Flush();
            writer.Close();
            writer = null;
        }
    }
}
#endif
