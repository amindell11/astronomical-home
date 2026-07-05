#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace Tests.PlayMode.ChaseBenchmark
{
    /// <summary>
    /// Writes one <see cref="ChaseRunResult"/> per line as JSONL (mirrors the
    /// <c>UtilityLogger</c> pattern) and mirrors the final file to <c>latest_runs.jsonl</c>
    /// so <c>scripts/benchmark_diff.ps1</c> can diff a baseline against a candidate.
    /// </summary>
    public sealed class ChaseBenchmarkLogger : IDisposable
    {
        private readonly StreamWriter writer;
        public string FilePath { get; }
        public string LatestPath { get; }

        /// <summary>Repo-relative results dir: <c>&lt;repoRoot&gt;/results/chase-benchmark</c>.</summary>
        public static string DefaultDir()
        {
            // Application.dataPath = <repo>/src/Asteroids3D/Assets → up 3 to repo root.
            var root = Directory.GetParent(Application.dataPath)?.Parent?.Parent?.FullName
                       ?? Application.persistentDataPath;
            return Path.Combine(root, "results", "chase-benchmark");
        }

        public ChaseBenchmarkLogger(string dir)
        {
            Directory.CreateDirectory(dir);
            var stamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            FilePath = Path.Combine(dir, $"runs_{stamp}.jsonl");
            LatestPath = Path.Combine(dir, "latest_runs.jsonl");
            // UTF-8 without BOM so strict per-line JSON parsers read row 1 cleanly.
            writer = new StreamWriter(FilePath, append: false, new UTF8Encoding(false)) { AutoFlush = false };
            Debug.Log($"[ChaseBenchmark] writing run rows to {FilePath}");
        }

        public void WriteRow(in ChaseRunResult row) => writer.WriteLine(row.ToJsonLine());

        public void Dispose()
        {
            writer.Flush();
            writer.Dispose();
            try { File.Copy(FilePath, LatestPath, overwrite: true); }
            catch (Exception e) { Debug.LogWarning($"[ChaseBenchmark] could not mirror latest: {e.Message}"); }
        }
    }
}
#endif
