using System.Linq;
using Game.Preview.Fixtures;
using Substrate.Services.Locales;
using Substrate.Sessions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Preview
{
    /// <summary>
    /// Builds the preview host in an empty scene around the game's GameHost prefab. Replay flies the first
    /// row of the prefab's sector run. The host stores fixture entries from the preview fixtures asset,
    /// found by layout name, and resolves them against the game run when an entry composes.
    /// The chosen entry is assigned before activation, so Playtest never composes a Replay session.
    /// The host waits inactive through the play-mode reload; entering play mode snapshots every tuning
    /// target's feel profiles, then activates it, so the panel tunes those assets in place.
    /// </summary>
    [InitializeOnLoad]
    public static class PreviewHostBootstrap
    {
        internal const string GameHostPath = "Assets/Prefabs/Game/GameHost.prefab";
        internal const string FixturesPath = "Assets/Settings/Game/Preview/PreviewFixtures.asset";
        private const string LocalesFolder = "Assets/Prefabs/Locales";
        private const string CopperTurquoisePath = "Assets/Prefabs/Locales/CopperTurquoise.prefab";

        static PreviewHostBootstrap() => EditorApplication.playModeStateChanged += ActivateWaitingHost;

        /// <summary>False when the user kept an unsaved scene open instead.</summary>
        public static bool EnterPlayMode(PreviewEntry entry = PreviewEntry.RunRecap,
            int combatSector = PreviewHost.WholeRun, LocaleSky combatLocale = null,
            bool playerInvulnerable = false)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            BuildHost(entry, combatSector, combatLocale, playerInvulnerable);
            EditorApplication.EnterPlaymode();
            return true;
        }

        internal static PreviewHost BuildHost(PreviewEntry entry = PreviewEntry.RunRecap,
            int combatSector = PreviewHost.WholeRun, LocaleSky combatLocale = null,
            bool playerInvulnerable = false)
        {
            var root = new GameObject("PreviewHost");
            root.SetActive(false);
            var host = root.AddComponent<PreviewHost>();
            host.gameHostPrefab = AssetDatabase.LoadAssetAtPath<GameHost>(GameHostPath);
            var run = host.gameHostPrefab.sessionProfile.run;
            host.sessionProfile = new SessionProfile { run = run, presentation = true };
            host.crowdedJumpSectors = new[] { Fixture("CrowdedJumpDeparture"), Fixture("CrowdedJumpArrival") };
            host.hudQuietSector = Fixture("HudQuietSector");
            host.hudExtractionSector = Fixture("HudExtractionSector");
            host.bossSector = Fixture("BossSector");
            host.sector1EliteSector = Fixture("EliteSector1");
            host.sector2EliteSector = Fixture("EliteSector2");
            host.crowdedFightSector = Fixture("CrowdedFightSector");
            host.jumpDestination = Locale(CopperTurquoisePath);
            host.entry = entry;
            host.combatSector = combatSector;
            host.combatLocale = combatLocale;
            host.playerInvulnerable = playerInvulnerable;
            return host;
        }

        internal static LocaleSky[] Locales() => AssetDatabase.FindAssets("t:Prefab", new[] { LocalesFolder })
            .Select(guid => AssetDatabase.LoadAssetAtPath<LocaleSky>(AssetDatabase.GUIDToAssetPath(guid)))
            .Where(locale => locale)
            .OrderBy(locale => locale.name)
            .ToArray();

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

        internal static SectorEntry FixtureRow(string layout) =>
            Fixture(layout).Resolve(AssetDatabase.LoadAssetAtPath<GameHost>(GameHostPath).sessionProfile.run);

        private static PreviewFixture Fixture(string layout)
        {
            var fixtures = AssetDatabase.LoadAssetAtPath<PreviewFixtures>(FixturesPath);
            if (!fixtures)
                throw new System.InvalidOperationException($"'{FixturesPath}' is not a {nameof(PreviewFixtures)} asset.");
            return fixtures.Fixture(layout);
        }

        private static LocaleSky Locale(string path) => AssetDatabase.LoadAssetAtPath<LocaleSky>(path);
    }
}
