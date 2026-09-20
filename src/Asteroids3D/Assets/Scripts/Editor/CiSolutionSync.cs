using System;
using System.Linq;
using System.Reflection;
using UnityEditor;

/// <summary>
/// Makes a hosted test boot write the .sln/.csproj files the ReSharper ratchet inspects, so the
/// hosted ratchet needs no Unity boot of its own. Runs only under <c>-syncSolutionForCi</c>, which
/// the headless-suite workflow passes; local boots never see it.
/// </summary>
public static class CiSolutionSync
{
    private const string Arg = "-syncSolutionForCi";
    private const string DoneKey = "CiSolutionSync.Done";

    [InitializeOnLoadMethod]
    private static void SyncOnce()
    {
        if (!Environment.GetCommandLineArgs().Contains(Arg) || SessionState.GetBool(DoneKey, false)) { return; }
        SessionState.SetBool(DoneKey, true);

        // Reflection: Core.Editor does not reference the Rider package's assembly.
        var editor = Type.GetType("Packages.Rider.Editor.RiderScriptEditor, Unity.Rider.Editor", throwOnError: true);
        var sync = editor.GetMethod("SyncSolution", BindingFlags.Public | BindingFlags.Static)
            ?? throw new MissingMethodException(editor.FullName, "SyncSolution");
        sync.Invoke(null, null);
        UnityEngine.Debug.Log("CiSolutionSync: solution written.");
    }
}
