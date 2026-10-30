using Substrate.Sectors;
using Substrate.Sessions;
using UI.Projection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Preview
{
    /// <summary>
    /// Builds the preview host in an empty scene around the game's GameHost prefab, with one trial
    /// sector for Inspection and the two-sector crowded jump fixture.
    /// The chosen mode is assigned before activation, so Playtest never composes an inspection session.
    /// The host waits inactive through the play-mode reload; entering play mode snapshots the feel
    /// profiles the prefab ships with, then activates it, so the panel tunes those assets in place.
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

        internal static void Activate(PreviewHost host, FeelSnapshot snapshot)
        {
            var game = host.gameHostPrefab;
            snapshot.Take(game.recapFeel, game.jumpFeel, CanopyProfile(game));
            host.gameObject.SetActive(true);
        }

        // Read through serialization, so the rig grows no member for the preview.
        internal static CanopyProjectionProfile CanopyProfile(GameHost game)
        {
            using var rig = new SerializedObject(game.playerRig);
            return ((CanopyProjection)rig.FindProperty("uiCamPrefab").objectReferenceValue).Profile;
        }

        private static void ActivateWaitingHost(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.EnteredPlayMode) return;
            var host = Object.FindFirstObjectByType<PreviewHost>(FindObjectsInactive.Include);
            if (host && !host.gameObject.activeSelf) Activate(host, FeelSnapshot.instance);
        }

        private static SectorEntry JumpEntry(string name) => new()
        {
            prefab = AssetDatabase.LoadAssetAtPath<Sector>($"Assets/Prefabs/Sectors/Preview/{name}.prefab"),
            config = AssetDatabase.LoadAssetAtPath<SectorSettings>($"Assets/Settings/Game/Preview/{name}.asset")
        };
    }
}
