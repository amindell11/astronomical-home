using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using AI.Tactics;
using RL.Opponents;
using UnityEditor;
using UnityEngine;

public static class RegeneratePilots
{
    public static string Run() => KeepingCommittedQuality(() =>
        Regenerate("ElitePilot", "AggressorPilot", "EliteTemper") + "\n"
        + Regenerate("EscortPilot", "KiterPilot", "EscortTemper"));

    private static string KeepingCommittedQuality(Func<string> regenerate)
    {
        var path = Path.Combine(Path.GetDirectoryName(Application.dataPath), "ProjectSettings", "QualitySettings.asset");
        var committed = CommittedCurrentQuality(path);
        ClearQualityDirty();
        try
        {
            return regenerate();
        }
        finally
        {
            try
            {
                RestoreCurrentQuality(path, committed);
            }
            finally
            {
                ClearQualityDirty();
            }
        }
    }

    private static string CommittedCurrentQuality(string qualityPath)
    {
        var relative = RepoRelative(qualityPath);
        var spec = "HEAD:" + relative;
        var start = new ProcessStartInfo("git", "cat-file blob " + spec)
        {
            WorkingDirectory = Path.GetDirectoryName(qualityPath),
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        Process git;
        try
        {
            git = Process.Start(start);
        }
        catch (Win32Exception e)
        {
            throw new InvalidOperationException($"git could not be started: {e.Message}");
        }

        if (git == null)
            throw new InvalidOperationException("git did not start; cannot read committed QualitySettings.asset.");

        using (git)
        {
            var text = git.StandardOutput.ReadToEnd();
            var error = git.StandardError.ReadToEnd();
            git.WaitForExit();
            if (git.ExitCode != 0)
                throw new InvalidOperationException($"git cat-file exited {git.ExitCode} for '{spec}': {error.Trim()}");
            return QualityValue(text, spec);
        }
    }

    private static void RestoreCurrentQuality(string path, string committedValue)
    {
        var bytes = File.ReadAllBytes(path);
        var marker = Encoding.ASCII.GetBytes("m_CurrentQuality: ");
        var index = IndexOf(bytes, marker);
        if (index < 0)
            throw new InvalidOperationException($"'{path}' has no m_CurrentQuality.");

        var valueStart = index + marker.Length;
        var valueEnd = valueStart;
        while (valueEnd < bytes.Length && bytes[valueEnd] >= (byte)'0' && bytes[valueEnd] <= (byte)'9')
            valueEnd++;
        if (valueEnd == valueStart)
            throw new InvalidOperationException($"'{path}' m_CurrentQuality is not an integer.");

        var current = Encoding.ASCII.GetString(bytes, valueStart, valueEnd - valueStart);
        if (current == committedValue)
            return;

        var committedBytes = Encoding.ASCII.GetBytes(committedValue);
        var updated = new byte[bytes.Length - (valueEnd - valueStart) + committedBytes.Length];
        Buffer.BlockCopy(bytes, 0, updated, 0, valueStart);
        Buffer.BlockCopy(committedBytes, 0, updated, valueStart, committedBytes.Length);
        Buffer.BlockCopy(bytes, valueEnd, updated, valueStart + committedBytes.Length, bytes.Length - valueEnd);
        try
        {
            File.WriteAllBytes(path, updated);
        }
        catch (IOException e)
        {
            throw new InvalidOperationException($"Could not restore '{path}': {e.Message}");
        }
    }

    private static int IndexOf(byte[] haystack, byte[] needle)
    {
        if (needle.Length == 0 || needle.Length > haystack.Length)
            return -1;

        for (var i = 0; i <= haystack.Length - needle.Length; i++)
        {
            var match = true;
            for (var j = 0; j < needle.Length; j++)
            {
                if (haystack[i + j] == needle[j])
                    continue;
                match = false;
                break;
            }

            if (match)
                return i;
        }

        return -1;
    }

    private static string QualityValue(string text, string origin)
    {
        const string marker = "m_CurrentQuality: ";
        var index = text.IndexOf(marker, StringComparison.Ordinal);
        if (index < 0)
            throw new InvalidOperationException($"'{origin}' has no m_CurrentQuality.");

        var start = index + marker.Length;
        var end = start;
        while (end < text.Length && text[end] >= '0' && text[end] <= '9')
            end++;
        if (end == start)
            throw new InvalidOperationException($"'{origin}' m_CurrentQuality is not an integer.");
        return text.Substring(start, end - start);
    }

    private static string RepoRelative(string absolutePath)
    {
        var dir = Path.GetDirectoryName(absolutePath);
        while (!string.IsNullOrEmpty(dir))
        {
            if (File.Exists(Path.Combine(dir, ".git")) || Directory.Exists(Path.Combine(dir, ".git")))
            {
                var relative = absolutePath.Substring(dir.Length)
                    .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                return relative.Replace('\\', '/');
            }

            dir = Path.GetDirectoryName(dir);
        }

        throw new InvalidOperationException($"No git root above '{absolutePath}'.");
    }

    private static void ClearQualityDirty()
    {
        var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset");
        if (assets == null)
            return;

        foreach (var asset in assets)
        {
            if (asset)
                EditorUtility.ClearDirty(asset);
        }
    }

    private static string Regenerate(string pilot, string source, string temper)
    {
        var path = $"Assets/Prefabs/Pilots/{pilot}.prefab";
        var sourcePath = $"Assets/Prefabs/Pilots/{source}.prefab";
        var temperPath = $"Assets/Settings/AI/Tactics/{temper}.asset";

        var guid = AssetDatabase.AssetPathToGUID(path);
        if (string.IsNullOrEmpty(guid))
            throw new InvalidOperationException($"'{path}' has no GUID.");

        var temperAsset = AssetDatabase.LoadAssetAtPath<TacticsSettings>(temperPath);
        if (!temperAsset)
            throw new InvalidOperationException($"'{temperPath}' did not load.");

        // Edit the loaded contents; saving a fresh instance would mint new file IDs.
        var root = PrefabUtility.LoadPrefabContents(path);
        if (!root)
            throw new InvalidOperationException($"LoadPrefabContents returned nothing for '{path}'.");

        try
        {
            var loadedSource = AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(root));
            if (loadedSource != sourcePath)
                throw new InvalidOperationException($"'{path}' source is '{loadedSource}', expected '{sourcePath}'.");

            PrefabUtility.RevertPrefabInstance(root, InteractionMode.AutomatedAction);

            var brain = root.GetComponent<ArchetypeBrain>();
            if (!brain)
                throw new InvalidOperationException($"'{path}' has no {nameof(ArchetypeBrain)}.");

            using (var serialized = new SerializedObject(brain))
            {
                var tactics = serialized.FindProperty("tactics");
                if (tactics == null)
                    throw new InvalidOperationException($"'{path}' {nameof(ArchetypeBrain)} has no tactics field.");

                tactics.objectReferenceValue = temperAsset;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            PrefabUtility.SaveAsPrefabAsset(root, path, out var saved);
            if (!saved)
                throw new InvalidOperationException($"SaveAsPrefabAsset failed for '{path}'.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        if (AssetDatabase.AssetPathToGUID(path) != guid)
            throw new InvalidOperationException($"'{path}' GUID changed from '{guid}'.");

        var reloaded = AssetDatabase.LoadMainAssetAtPath(path) as GameObject;
        if (!reloaded)
            throw new InvalidOperationException($"Reload of '{path}' did not return a GameObject.");

        var reloadedBrain = reloaded.GetComponent<ArchetypeBrain>();
        if (!reloadedBrain)
            throw new InvalidOperationException($"Reloaded '{path}' has no {nameof(ArchetypeBrain)}.");

        using (var reloadedSerialized = new SerializedObject(reloadedBrain))
        {
            var reloadedTactics = reloadedSerialized.FindProperty("tactics");
            if (reloadedTactics == null)
                throw new InvalidOperationException($"Reloaded '{path}' {nameof(ArchetypeBrain)} has no tactics field.");
            if (reloadedTactics.objectReferenceValue != temperAsset)
                throw new InvalidOperationException($"Reloaded '{path}' tactics is not '{temperPath}'.");
        }

        var reloadedSource = AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(reloaded));
        if (reloadedSource != sourcePath)
            throw new InvalidOperationException($"Reloaded '{path}' source is '{reloadedSource}', expected '{sourcePath}'.");

        return $"{pilot}: guid={guid} source={sourcePath} tactics={temperPath}";
    }
}
