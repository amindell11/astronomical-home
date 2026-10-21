using System;
using System.IO;
using UnityEngine;

namespace Game.Runs
{
    /// <summary>Reads the build identity; false with the reason when it cannot be read.</summary>
    public delegate bool BuildIdentitySource(out BuildIdentity identity, out string failure);

    /// <summary>
    /// The git side of what a run was played on: the commit, and whether the Unity project tree had
    /// uncommitted work. The editor asks git on every read; a player build cannot, so its pre-build
    /// hook stamps a file into StreamingAssets that the player reads once. Informational only: run
    /// records are grouped by the stat fingerprint and the loadout stat hash, never by this.
    /// </summary>
    [Serializable]
    public struct BuildIdentity
    {
        public string commit;
        public bool dirty;

        public const string FileName = "build-identity.json";

        private const string BranchOidHeader = "# branch.oid ";

        public string ToJson() => JsonUtility.ToJson(this);

        public static BuildIdentity Parse(string json)
        {
            var identity = JsonUtility.FromJson<BuildIdentity>(json);
            if (string.IsNullOrEmpty(identity.commit))
                throw new FormatException($"Build identity has no commit: '{json}'.");
            return identity;
        }

        /// <summary>In a player this reads the stamped file, here, once, and throws when it is missing.</summary>
        public static BuildIdentitySource Source()
        {
#if UNITY_EDITOR
            return TryReadGit;
#else
            var stamped = Parse(File.ReadAllText(Path.Combine(Application.streamingAssetsPath, FileName)));
            return (out BuildIdentity identity, out string failure) =>
            {
                identity = stamped;
                failure = null;
                return true;
            };
#endif
        }

        /// <summary>Reads <c>git status --porcelain=v2 --branch</c> output: any line past the headers is a change.</summary>
        internal static BuildIdentity ParseGitStatus(string status)
        {
            var identity = new BuildIdentity();
            foreach (var line in status.Split('\n'))
            {
                if (line.StartsWith(BranchOidHeader, StringComparison.Ordinal))
                    identity.commit = line.Substring(BranchOidHeader.Length).Trim();
                else if (line.Length > 0 && line[0] != '#')
                    identity.dirty = true;
            }

            if (string.IsNullOrEmpty(identity.commit))
                throw new FormatException($"git status reported no commit: '{status}'.");
            return identity;
        }

#if UNITY_EDITOR
        // The pathspec scopes the dirty flag to the Unity project; --no-optional-locks skips the index lock.
        private const string GitStatusArguments = "--no-optional-locks status --porcelain=v2 --branch -- .";

        public static bool TryReadGit(out BuildIdentity identity, out string failure)
        {
            identity = default;
            var start = new System.Diagnostics.ProcessStartInfo("git", GitStatusArguments)
            {
                WorkingDirectory = Path.GetDirectoryName(Application.dataPath),
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            try
            {
                using var git = System.Diagnostics.Process.Start(start);
                var status = git.StandardOutput.ReadToEnd();
                var error = git.StandardError.ReadToEnd();
                git.WaitForExit();
                if (git.ExitCode != 0)
                {
                    failure = $"git status exited {git.ExitCode}: {error.Trim()}";
                    return false;
                }

                identity = ParseGitStatus(status);
                failure = null;
                return true;
            }
            catch (System.ComponentModel.Win32Exception e)
            {
                failure = $"git could not be started: {e.Message}";
                return false;
            }
        }
#endif
    }
}
