using System;
using System.Globalization;
using System.IO;
using System.Text;
using AI.Context;
using AI.Scanning;
using AI.States;
using Ships.Command;
using UnityEngine;

namespace AI.Observation
{
    /// <summary>
    /// Per-tick RL data pipe: builds a <see cref="TacticalObservation"/> from the ship's live views
    /// plus its own <see cref="ThreatScanner"/>, pairs it with the action the brain produced, and
    /// writes one JSONL record per transition: {obs, action, reward, next_obs, terminal}. The
    /// <c>reward</c> slot is written as an explicit zero — PR-S3 owns reward and episode boundaries.
    /// Read-only over the sim; nothing here influences behavior.
    /// </summary>
    public sealed class ObservationRecorder
    {
        private const int ObstacleCapacity = 32;

        private readonly IShipStatus self;
        private readonly EnemyTracker combat;
        private readonly Scout scout;
        private readonly ThreatScanner threatScanner;

        private readonly TacticalObservation bufferA;
        private readonly TacticalObservation bufferB;
        private TacticalObservation pending;
        private TacticalObservation current;
        private NavigationIntent pendingAction;
        private bool hasPending;

        private StreamWriter writer;
        private readonly StringBuilder line = new();
        private float elapsed;

        public ObservationRecorder(IShipStatus self, EnemyTracker combat, Scout scout,
            ThreatScanner threatScanner, string sessionTag)
        {
            this.self = self;
            this.combat = combat;
            this.scout = scout;
            this.threatScanner = threatScanner;

            var threatCapacity = threatScanner.Buffer.Length;
            bufferA = new TacticalObservation(threatCapacity, ObstacleCapacity);
            bufferB = new TacticalObservation(threatCapacity, ObstacleCapacity);
            current = bufferA;

            OpenLogFile(sessionTag);
        }

        public void Record(in NavigationIntent action, float dt)
        {
            if (writer == null) return;

            elapsed += dt;
            threatScanner.Scan();
            ObservationExtractor.Populate(current, self, BuildTargetView(),
                threatScanner.Buffer, threatScanner.Count, scout.ObstacleScan, elapsed);

            if (hasPending)
                WriteTransition(pending, pendingAction, current, terminal: false);

            pending = current;
            pendingAction = action;
            hasPending = true;
            current = ReferenceEquals(current, bufferA) ? bufferB : bufferA;
        }

        public void Close()
        {
            if (writer == null) return;
            if (hasPending)
                WriteTransition(pending, pendingAction, pending, terminal: true);
            writer.Flush();
            writer.Close();
            writer = null;
        }

        private TargetView BuildTargetView()
        {
            if (!combat.HasEnemy) return TargetView.None;
            return new TargetView(true, combat.EnemyPos, combat.EnemyVel, combat.EnemyForward,
                combat.EnemyHealthPct, combat.EnemyShieldPct);
        }

        private void WriteTransition(TacticalObservation obs, in NavigationIntent action,
            TacticalObservation nextObs, bool terminal)
        {
            line.Clear();
            line.Append("{\"v\":").Append(TacticalObservation.SchemaVersion);
            Num(",\"t\":", obs.time);
            line.Append(",\"reward\":0");
            line.Append(",\"terminal\":").Append(terminal ? "true" : "false");
            line.Append(",\"obs\":");
            AppendObservation(obs);
            line.Append(",\"action\":");
            AppendAction(action);
            line.Append(",\"next_obs\":");
            AppendObservation(nextObs);
            line.Append('}');

            writer.WriteLine(line.ToString());
        }

        private void AppendObservation(TacticalObservation obs)
        {
            line.Append('{');
            var s = obs.self;
            line.Append("\"self\":{\"vel\":");
            Vec(s.velocity);
            Num(",\"speedPct\":", s.speedPct);
            Num(",\"yawRatePct\":", s.yawRatePct);
            Num(",\"hp\":", s.healthPct);
            Num(",\"shield\":", s.shieldPct);
            Num(",\"boost\":", s.boostAvailable);
            Num(",\"boostCd\":", s.boostCooldownPct);
            line.Append('}');

            line.Append(",\"hasTarget\":").Append(obs.hasTarget ? "true" : "false");
            if (obs.hasTarget)
            {
                var tk = obs.target;
                line.Append(",\"target\":{\"pos\":");
                Vec(tk.relPosition);
                Num(",\"dist\":", tk.distance);
                line.Append(",\"vel\":");
                Vec(tk.relVelocity);
                line.Append(",\"facing\":");
                Vec(tk.facing);
                Num(",\"hp\":", tk.healthPct);
                Num(",\"shield\":", tk.shieldPct);
                line.Append('}');
            }

            line.Append(",\"threats\":[");
            for (var i = 0; i < obs.threatCount; i++)
            {
                if (i > 0) line.Append(',');
                var th = obs.threats[i];
                line.Append("{\"pos\":");
                Vec(th.relPosition);
                Num(",\"dist\":", th.distance);
                line.Append(",\"vel\":");
                Vec(th.relVelocity);
                line.Append(",\"kind\":").Append((int)th.kind).Append('}');
            }
            line.Append(']');

            line.Append(",\"obstacles\":[");
            for (var i = 0; i < obs.obstacleCount; i++)
            {
                if (i > 0) line.Append(',');
                var ob = obs.obstacles[i];
                line.Append("{\"pos\":");
                Vec(ob.relPosition);
                Num(",\"dist\":", ob.distance);
                Num(",\"radius\":", ob.radius);
                line.Append('}');
            }
            line.Append(']');
            line.Append('}');
        }

        private void AppendAction(in NavigationIntent action)
        {
            line.Append("{\"valid\":").Append(action.isValid ? "true" : "false");
            line.Append(",\"goalMode\":\"").Append(action.goalMode).Append('"');
            line.Append(",\"goalPos\":");
            Vec(action.goalPosition);
            line.Append(",\"goalVel\":");
            Vec(action.goalVelocity);
            Num(",\"desiredRange\":", action.desiredRange);
            line.Append(",\"hasTarget\":").Append(action.hasTarget ? "true" : "false");
            line.Append(",\"tacticalCosts\":").Append(action.applyTacticalCosts ? "true" : "false");
            line.Append(",\"firing\":").Append(action.enableFiring ? "true" : "false");
            line.Append('}');
        }

        private void Num(string key, float value)
        {
            line.Append(key).Append(value.ToString("F4", CultureInfo.InvariantCulture));
        }

        private void Vec(Vector2 v)
        {
            line.Append('[').Append(v.x.ToString("F4", CultureInfo.InvariantCulture))
                .Append(',').Append(v.y.ToString("F4", CultureInfo.InvariantCulture)).Append(']');
        }

        private void OpenLogFile(string sessionTag)
        {
            var dir = DefaultLogDir();
            if (!string.IsNullOrEmpty(sessionTag))
                dir = Path.Combine(dir, sessionTag);
            Directory.CreateDirectory(dir);

            var label = self.Id.Value.ToString(CultureInfo.InvariantCulture);
            var stamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            var path = Path.Combine(dir, $"obs_{label}_{stamp}.jsonl");
            writer = new StreamWriter(path, append: false, Encoding.UTF8) { AutoFlush = true };
            UnityEngine.Debug.Log($"[ObservationRecorder] Logging to: {path}");
        }

        private static string DefaultLogDir()
        {
            var repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", ".."));
            return Path.Combine(repoRoot, "results", "ai-observations");
        }
    }
}
