using System.IO;
using UnityEngine;

namespace Substrate.Results
{
    public static class ResultsRoot
    {
        /// <summary>The directory one kind of result is written under; not created here.</summary>
        public static string Folder(string folder)
        {
            // In a player Application.dataPath is the exe's Data dir, not the repo tree the editor layout climbs to.
            var baseDir = Application.isEditor
                ? Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", ".."))
                : Application.persistentDataPath;
            return Path.Combine(baseDir, "results", folder);
        }
    }
}
