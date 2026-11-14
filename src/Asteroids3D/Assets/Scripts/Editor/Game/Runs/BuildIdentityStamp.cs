using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Game.Runs
{
    /// <summary>
    /// Stamps the build identity into every player build: git is read before the build, the build
    /// label is taken from -gameBuildLabel when the build was given one, and both are written to
    /// StreamingAssets, where the player finds them, then removed once the build is done so the
    /// project tree stays as it was. A refused label fails the build. A failed build skips the
    /// removal; the file is gitignored and the next build overwrites it.
    /// </summary>
    public class BuildIdentityStamp : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        private const string FolderAssetPath = "Assets/StreamingAssets";
        private const string FileAssetPath = FolderAssetPath + "/" + BuildIdentity.FileName;

        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (!BuildIdentity.TryReadGit(out var identity, out var failure))
                throw new BuildFailedException($"Build identity not stamped: {failure}");

            var label = ReadBuildLabel();
            if (label != null)
            {
                if (!BuildIdentity.TryAcceptLabel(label, out var labelFailure))
                    throw new BuildFailedException(labelFailure);
                identity.label = label;
            }

            Directory.CreateDirectory(Application.streamingAssetsPath);
            File.WriteAllText(Path.Combine(Application.streamingAssetsPath, BuildIdentity.FileName), identity.ToJson());
            AssetDatabase.Refresh();
        }

        // Null means omitted; empty means the flag had no text.
        private static string ReadBuildLabel()
        {
            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length; i++)
            {
                if (args[i] != "-gameBuildLabel")
                    continue;
                return i + 1 < args.Length ? args[i + 1] : "";
            }

            return null;
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            AssetDatabase.DeleteAsset(FileAssetPath);
            if (Directory.GetFileSystemEntries(Application.streamingAssetsPath).Length == 0)
                AssetDatabase.DeleteAsset(FolderAssetPath);
        }
    }
}
