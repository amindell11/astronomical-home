using System;
using System.Globalization;
using System.IO;
using Game.Sectors;
using UnityEngine;

namespace Diagnostics.Performance
{
    public static class LatencyProfilingScenario
    {
        public const string CombatBaseline = "combat_baseline";

        private static LatencyProfilingSettings activeSettings;
        private static bool initialized;

        public static bool IsActive => activeSettings != null;
        public static LatencyProfilingSettings ActiveSettings => activeSettings;

        public static bool EnsureInitializedFromCommandLine()
        {
            if (initialized)
                return IsActive;

            initialized = true;
            activeSettings = LatencyProfilingSettings.TryParse(Environment.GetCommandLineArgs());
            return IsActive;
        }

        public static bool HasLatencyArguments(string[] args)
        {
            if (args == null)
                return false;

            for (var i = 0; i < args.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(args[i]) &&
                    args[i].StartsWith("-latency", StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        public static bool TryConfigureSector(SectorManager sectorManager)
        {
            if (!EnsureInitializedFromCommandLine() || sectorManager == null)
                return false;

            if (!string.Equals(activeSettings.Scenario, CombatBaseline, StringComparison.OrdinalIgnoreCase))
                return false;

            if (sectorManager is not ArenaSectorManager arenaSector)
                return false;

            arenaSector.ApplyLatencyProfilingSettings(activeSettings);
            return true;
        }
    }

    [Serializable]
    public sealed class LatencyProfilingSettings
    {
        public string Scenario = LatencyProfilingScenario.CombatBaseline;
        public int WarmupSeconds = 5;
        public int SampleSeconds = 30;
        public string OutDir = "results/latency-profiling";
        public int Seed = 1337;
        public int Width = 1920;
        public int Height = 1080;
        public string QualityLevel = string.Empty;
        public int TeamACount = 1;
        public int TeamBCount = 6;
        public int AsteroidCount = 20;
        public float SpawnRadius = 60f;
        public float RespawnDelay = 3f;
        public int StartupTimeoutSeconds = 90;
        public bool DisableDebugOverlay = true;
        public bool DisableVSync = true;

        public string GetResolvedOutDir()
        {
            if (Path.IsPathRooted(OutDir))
                return Path.GetFullPath(OutDir);

            return Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), OutDir));
        }

        public static string TryResolveOutDir(string[] args)
        {
            if (args == null)
                return null;

            for (var i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], "-latencyOutDir", StringComparison.OrdinalIgnoreCase))
                {
                    var outDir = args[i + 1];
                    if (string.IsNullOrWhiteSpace(outDir))
                        return null;

                    if (Path.IsPathRooted(outDir))
                        return Path.GetFullPath(outDir);

                    return Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), outDir));
                }
            }

            return null;
        }

        public static LatencyProfilingSettings TryParse(string[] args)
        {
            if (args == null || args.Length == 0)
                return null;

            var hasScenario = false;
            var settings = new LatencyProfilingSettings();

            for (var i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "-latencyProfileScenario":
                        settings.Scenario = ReadValue(args, ref i, settings.Scenario);
                        hasScenario = true;
                        break;
                    case "-latencyWarmupSeconds":
                        settings.WarmupSeconds = ReadInt(args, ref i, settings.WarmupSeconds);
                        break;
                    case "-latencySampleSeconds":
                        settings.SampleSeconds = ReadInt(args, ref i, settings.SampleSeconds);
                        break;
                    case "-latencyOutDir":
                        settings.OutDir = ReadValue(args, ref i, settings.OutDir);
                        break;
                    case "-latencySeed":
                        settings.Seed = ReadInt(args, ref i, settings.Seed);
                        break;
                    case "-latencyWidth":
                        settings.Width = ReadInt(args, ref i, settings.Width);
                        break;
                    case "-latencyHeight":
                        settings.Height = ReadInt(args, ref i, settings.Height);
                        break;
                    case "-latencyQuality":
                        settings.QualityLevel = ReadValue(args, ref i, settings.QualityLevel);
                        break;
                    case "-latencyTeamACount":
                        settings.TeamACount = ReadInt(args, ref i, settings.TeamACount);
                        break;
                    case "-latencyTeamBCount":
                        settings.TeamBCount = ReadInt(args, ref i, settings.TeamBCount);
                        break;
                    case "-latencyAsteroidCount":
                        settings.AsteroidCount = ReadInt(args, ref i, settings.AsteroidCount);
                        break;
                    case "-latencySpawnRadius":
                        settings.SpawnRadius = ReadFloat(args, ref i, settings.SpawnRadius);
                        break;
                    case "-latencyRespawnDelay":
                        settings.RespawnDelay = ReadFloat(args, ref i, settings.RespawnDelay);
                        break;
                    case "-latencyStartupTimeoutSeconds":
                        settings.StartupTimeoutSeconds = ReadInt(args, ref i, settings.StartupTimeoutSeconds);
                        break;
                    case "-latencyDisableDebugOverlay":
                        settings.DisableDebugOverlay = true;
                        break;
                    case "-latencyKeepDebugOverlay":
                        settings.DisableDebugOverlay = false;
                        break;
                    case "-latencyDisableVSync":
                        settings.DisableVSync = true;
                        break;
                    case "-latencyKeepVSync":
                        settings.DisableVSync = false;
                        break;
                }
            }

            if (!hasScenario)
                return null;

            settings.WarmupSeconds = Mathf.Max(0, settings.WarmupSeconds);
            settings.SampleSeconds = Mathf.Max(1, settings.SampleSeconds);
            settings.TeamACount = Mathf.Max(1, settings.TeamACount);
            settings.TeamBCount = Mathf.Max(1, settings.TeamBCount);
            settings.AsteroidCount = Mathf.Max(0, settings.AsteroidCount);
            settings.Width = Mathf.Max(320, settings.Width);
            settings.Height = Mathf.Max(200, settings.Height);
            settings.SpawnRadius = Mathf.Max(1f, settings.SpawnRadius);
            settings.RespawnDelay = Mathf.Max(0f, settings.RespawnDelay);
            settings.StartupTimeoutSeconds = Mathf.Max(5, settings.StartupTimeoutSeconds);

            return settings;
        }

        private static string ReadValue(string[] args, ref int index, string fallback)
        {
            if (index + 1 >= args.Length)
                return fallback;

            index++;
            return args[index];
        }

        private static int ReadInt(string[] args, ref int index, int fallback)
        {
            var raw = ReadValue(args, ref index, fallback.ToString(CultureInfo.InvariantCulture));
            return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : fallback;
        }

        private static float ReadFloat(string[] args, ref int index, float fallback)
        {
            var raw = ReadValue(args, ref index, fallback.ToString(CultureInfo.InvariantCulture));
            return float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : fallback;
        }
    }
}
