using Substrate.Sectors;
using Substrate.Sessions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Preview
{
    /// <summary>
    /// Builds the preview host in an empty scene around the game's GameHost prefab, with one trial
    /// sector for Inspection and the two-sector crowded jump fixture.
    /// The chosen mode is assigned before activation, so Playtest never composes an inspection session.
    /// The host waits inactive through the play-mode reload; entering play mode activates it on working
    /// copies of the prefab's feel profiles, so neither mode ever runs on the assets themselves.
    /// </summary>
    [InitializeOnLoad]
    public static class PreviewHostBootstrap
    {
        internal const string GameHostPath = "Assets/Prefabs/Game/GameHost.prefab";
        private const string SectorPrefabPath = "Assets/Prefabs/Sectors/TrialSector.prefab";
        private const string SectorConfigPath = "Assets/Settings/Game/SurvivalTrialConfig.asset";

        static PreviewHostBootstrap() => EditorApplication.playModeStateChanged += ActivateWaitingHost;

        /// <summary>False when the user kept an unsaved scene open instead.</summary>
        public static bool EnterPlayMode(PreviewSubjectKind subject = PreviewSubjectKind.RunRecap,
            PreviewMode mode = PreviewMode.Inspection, PreviewPlaytestSetup setup = PreviewPlaytestSetup.Combat)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            BuildHost(subject, mode, setup);
            EditorApplication.EnterPlaymode();
            return true;
        }

        internal static PreviewHost BuildHost(PreviewSubjectKind subject = PreviewSubjectKind.RunRecap,
            PreviewMode mode = PreviewMode.Inspection, PreviewPlaytestSetup setup = PreviewPlaytestSetup.Combat)
        {
            var root = new GameObject("PreviewHost");
            root.SetActive(false);
            var host = root.AddComponent<PreviewHost>();
            host.gameHostPrefab = AssetDatabase.LoadAssetAtPath<GameHost>(GameHostPath);
            host.sessionProfile = new SessionProfile
            {
                sectors = new[]
                {
                    new SectorEntry
                    {
                        prefab = AssetDatabase.LoadAssetAtPath<Sector>(SectorPrefabPath),
                        config = AssetDatabase.LoadAssetAtPath<SectorSettings>(SectorConfigPath)
                    }
                },
                presentation = true
            };
            host.crowdedJumpSectors = new[] { JumpEntry("CrowdedJumpDeparture"), JumpEntry("CrowdedJumpArrival") };
            host.subjectKind = subject;
            host.mode = mode;
            host.playtestSetup = setup;
            return host;
        }

        internal static void Activate(PreviewHost host, FeelWorkingCopies copies)
        {
            host.recapFeel = copies.For(host.gameHostPrefab.recapFeel);
            host.jumpFeel = copies.For(host.gameHostPrefab.jumpFeel);
            host.canopyFeel = copies.For(host.gameHostPrefab.playerRig.AuthoredCanopyProfile);
            host.gameObject.SetActive(true);
        }

        private static void ActivateWaitingHost(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.EnteredPlayMode) return;
            var host = Object.FindFirstObjectByType<PreviewHost>(FindObjectsInactive.Include);
            if (host && !host.gameObject.activeSelf) Activate(host, FeelWorkingCopies.Current);
        }

        private static SectorEntry JumpEntry(string name) => new()
        {
            prefab = AssetDatabase.LoadAssetAtPath<Sector>($"Assets/Prefabs/Sectors/Preview/{name}.prefab"),
            config = AssetDatabase.LoadAssetAtPath<SectorSettings>($"Assets/Settings/Game/Preview/{name}.asset")
        };
    }
}
