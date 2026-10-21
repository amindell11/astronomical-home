using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Game.Runs
{
    /// <summary>
    /// Stamps the build identity into every player build: git is read before the build and written
    /// to StreamingAssets, where the player finds it, then removed once the build is done so the
    /// project tree stays as it was. A failed build skips the removal; the file is gitignored and
    /// the next build overwrites it.
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

            Directory.CreateDirectory(Application.streamingAssetsPath);
            File.WriteAllText(Path.Combine(Application.streamingAssetsPath, BuildIdentity.FileName), identity.ToJson());
            AssetDatabase.Refresh();
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            AssetDatabase.DeleteAsset(FileAssetPath);
            if (Directory.GetFileSystemEntries(Application.streamingAssetsPath).Length == 0)
                AssetDatabase.DeleteAsset(FolderAssetPath);
        }
    }
}
