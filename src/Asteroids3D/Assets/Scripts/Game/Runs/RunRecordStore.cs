using System;
using System.IO;
using Substrate.Results;
using UnityEngine;

namespace Game.Runs
{
    /// <summary>
    /// The one append-only file of run records. It owns its path: a reader is handed
    /// <see cref="Path"/> (logged on every append), never derives it. A failed write is logged and
    /// swallowed so the run loop goes on without that record — an exception here would stop the
    /// game host's coroutine on the death frame.
    /// </summary>
    public sealed class RunRecordStore
    {
        public const string Folder = "balance";
        public const string FileName = "run-records.jsonl";

        public string Path { get; }

        public RunRecordStore(string pathOverride = null) =>
            Path = string.IsNullOrEmpty(pathOverride)
                ? System.IO.Path.Combine(ResultsRoot.Folder(Folder), FileName)
                : pathOverride;

        public void Append(in RunRecord record)
        {
            try
            {
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path));
                File.AppendAllText(Path, record.ToJsonLine() + "\n");
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                Debug.LogError($"Run record not written to '{Path}': {e.Message}");
                return;
            }

            Debug.Log($"Run record appended to '{Path}'.");
        }
    }
}
