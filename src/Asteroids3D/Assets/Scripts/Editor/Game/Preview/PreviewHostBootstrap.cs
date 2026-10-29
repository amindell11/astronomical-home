using Substrate.Sectors;
using Substrate.Sessions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Preview
{
    /// <summary>
    /// Builds the preview host around the game's GameHost prefab in an empty scene: Playtest runs that
    /// prefab, and Inspection loads one trial sector with the prefab's rig and presentation assets.
    /// The crowded jump setup swaps in the two-sector crowded jump fixture.
    /// The chosen mode is assigned before activation, so Playtest never composes an inspection session.
    /// </summary>
    public static class PreviewHostBootstrap
    {
        internal const string GameHostPath = "Assets/Prefabs/Game/GameHost.prefab";
        private const string SectorPrefabPath = "Assets/Prefabs/Sectors/TrialSector.prefab";
        private const string SectorConfigPath = "Assets/Settings/Game/SurvivalTrialConfig.asset";

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
            root.SetActive(true);
            return host;
        }

        private static SectorEntry JumpEntry(string name) => new()
        {
            prefab = AssetDatabase.LoadAssetAtPath<Sector>($"Assets/Prefabs/Sectors/Preview/{name}.prefab"),
            config = AssetDatabase.LoadAssetAtPath<SectorSettings>($"Assets/Settings/Game/Preview/{name}.asset")
        };
    }
}
