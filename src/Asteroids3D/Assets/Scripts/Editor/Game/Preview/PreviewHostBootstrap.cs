using Substrate.Sectors;
using Substrate.Services.Locales;
using Substrate.Sessions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Preview
{
    /// <summary>
    /// Builds the preview host in an empty scene around the game's GameHost prefab, with one trial
    /// sector for Replay, the HUD replays' two fixture sectors and the two-sector crowded jump fixture.
    /// The chosen entry is assigned before activation, so Playtest never composes a Replay session.
    /// The host waits inactive through the play-mode reload; entering play mode snapshots every tuning
    /// target's feel profiles, then activates it, so the panel tunes those assets in place.
    /// </summary>
    [InitializeOnLoad]
    public static class PreviewHostBootstrap
    {
        internal const string GameHostPath = "Assets/Prefabs/Game/GameHost.prefab";
        private const string SectorPrefabPath = "Assets/Prefabs/Sectors/TrialSector.prefab";
        private const string SectorConfigPath = "Assets/Settings/Game/SurvivalTrialConfig.asset";
        private const string JumpDestinationPath = "Assets/Prefabs/Locales/Locale_1.prefab";

        static PreviewHostBootstrap() => EditorApplication.playModeStateChanged += ActivateWaitingHost;

        /// <summary>False when the user kept an unsaved scene open instead.</summary>
        public static bool EnterPlayMode(PreviewEntry entry = PreviewEntry.RunRecap)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            BuildHost(entry);
            EditorApplication.EnterPlaymode();
            return true;
        }

        internal static PreviewHost BuildHost(PreviewEntry entry = PreviewEntry.RunRecap)
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
            host.hudQuietSector = FixtureSector("HudQuietSector", SectorConfigPath);
            host.hudExtractionSector = FixtureSector("HudExtractionSector", SectorConfigPath);
            host.jumpDestination = AssetDatabase.LoadAssetAtPath<LocaleSky>(JumpDestinationPath);
            host.entry = entry;
            return host;
        }

        internal static void Activate(PreviewHost host, FeelSnapshot snapshot)
        {
            snapshot.Take(TuningTarget.ProfilesOfAll(host.gameHostPrefab));
            host.gameObject.SetActive(true);
        }

        private static void ActivateWaitingHost(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.EnteredPlayMode) return;
            var host = Object.FindFirstObjectByType<PreviewHost>(FindObjectsInactive.Include);
            if (host && !host.gameObject.activeSelf) Activate(host, FeelSnapshot.instance);
        }

        private static SectorEntry JumpEntry(string name) =>
            FixtureSector(name, $"Assets/Settings/Game/Preview/{name}.asset");

        private static SectorEntry FixtureSector(string name, string configPath) => new()
        {
            prefab = AssetDatabase.LoadAssetAtPath<Sector>($"Assets/Prefabs/Sectors/Preview/{name}.prefab"),
            config = AssetDatabase.LoadAssetAtPath<SectorSettings>(configPath)
        };
    }
}
