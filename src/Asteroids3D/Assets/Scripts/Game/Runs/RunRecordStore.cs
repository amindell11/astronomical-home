using System;
using System.Globalization;
using System.IO;
using Substrate.Results;
using UnityEngine;

namespace Game.Runs
{
    public struct RunRecordExport
    {
        public enum Kind { Saved, NothingYet, Failed }

        public Kind Outcome;
        public string WrittenPath;

        public static RunRecordExport Saved(string path) =>
            new() { Outcome = Kind.Saved, WrittenPath = path };

        public static RunRecordExport NothingYet() => new() { Outcome = Kind.NothingYet };

        public static RunRecordExport Failed() => new() { Outcome = Kind.Failed };
    }

    /// <summary>
    /// The one append-only file of run records. It owns its path: a reader is handed
    /// <see cref="Path"/> (logged on every append), never derives it. A failed write is logged and
    /// swallowed so the run loop goes on without that record — an exception here would stop the
    /// game host's coroutine on the death frame. Export copies that file into a chosen folder and
    /// never rewrites it; no file yet is a normal state, reported apart from a failed copy.
    /// Playtest feedback goes to its own file beside it, so no run-record reader meets a foreign line.
    /// </summary>
    public sealed class RunRecordStore
    {
        public const string Folder = "balance";
        public const string FileName = "run-records.jsonl";
        public const string FeedbackFileName = "run-feedback.jsonl";

        public string Path { get; }

        public string FeedbackPath { get; }

        public RunRecordStore(string pathOverride = null)
        {
            Path = string.IsNullOrEmpty(pathOverride)
                ? System.IO.Path.Combine(ResultsRoot.Folder(Folder), FileName)
                : pathOverride;
            FeedbackPath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Path), FeedbackFileName);
        }

        /// <summary>False when the record did not land; the failure is logged.</summary>
        public bool Append(in RunRecord record) => AppendLine(Path, record.ToJsonLine(), "Run record");

        /// <summary>False when the note did not land; the failure is logged.</summary>
        public bool AppendFeedback(in RunFeedback feedback) =>
            AppendLine(FeedbackPath, feedback.ToJsonLine(), "Run feedback");

        private static bool AppendLine(string path, string line, string what)
        {
            try
            {
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
                File.AppendAllText(path, line + "\n");
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                Debug.LogError($"{what} not written to '{path}': {e.Message}");
                return false;
            }

            Debug.Log($"{what} appended to '{path}'.");
            return true;
        }

        public RunRecordExport Export(string folder, in BuildIdentity identity)
        {
            if (!File.Exists(Path))
                return RunRecordExport.NothingYet();

            var token = string.IsNullOrEmpty(identity.label) ? identity.commit.Substring(0, 8) : identity.label;
            var name = "andromeda-runs-" + token + "-"
                + DateTime.UtcNow.ToString("yyyyMMdd-HHmm", CultureInfo.InvariantCulture) + ".jsonl";
            var destination = System.IO.Path.Combine(folder, name);
            try
            {
                File.Copy(Path, destination, overwrite: true);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                Debug.LogError($"Run records not exported to '{destination}': {e.Message}");
                return RunRecordExport.Failed();
            }

            return RunRecordExport.Saved(destination);
        }
    }
}
