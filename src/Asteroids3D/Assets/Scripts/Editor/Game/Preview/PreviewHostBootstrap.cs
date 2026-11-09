using System.Linq;
using Substrate.Sectors;
using Substrate.Services.Locales;
using Substrate.Sessions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Preview
{
    /// <summary>
    /// Builds the preview host in an empty scene around the game's GameHost prefab. Replay flies the first
    /// row of the prefab's sector run; the HUD replays' two fixture sectors, sector 1's elite fixture and
    /// the crowded fight fixture take that row's locale, sector 2's elite fixture the second row's and the
    /// boss fixture the last row's, a Random row standing in as its pool's first locale, while the
    /// two-sector crowded jump fixture keeps its own two locales.
    /// The chosen entry is assigned before activation, so Playtest never composes a Replay session.
    /// The host waits inactive through the play-mode reload; entering play mode snapshots every tuning
    /// target's feel profiles, then activates it, so the panel tunes those assets in place.
    /// </summary>
    [InitializeOnLoad]
    public static class PreviewHostBootstrap
    {
        internal const string GameHostPath = "Assets/Prefabs/Game/GameHost.prefab";
        private const string LocalesFolder = "Assets/Prefabs/Locales";
        private const string IllustratedBluePath = "Assets/Prefabs/Locales/IllustratedBlue.prefab";
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
            host.crowdedJumpSectors = new[]
            {
                FixtureSector("CrowdedJumpDeparture", Locale(IllustratedBluePath)),
                FixtureSector("CrowdedJumpArrival", Locale(CopperTurquoisePath))
            };
            host.hudQuietSector = FixtureSector("HudQuietSector", RowLocale(run, 0));
            host.hudExtractionSector = FixtureSector("HudExtractionSector", RowLocale(run, 0));
            host.bossSector = FixtureSector("BossSector", RowLocale(run, run.Sectors.Count - 1));
            host.sector1EliteSector = FixtureSector("EliteSector1", RowLocale(run, 0));
            host.sector2EliteSector = FixtureSector("EliteSector2", RowLocale(run, 1));
            host.crowdedFightSector = FixtureSector("CrowdedFightSector", RowLocale(run, 0));
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

        private static SectorEntry FixtureSector(string name, LocaleSky locale) => new()
        {
            prefab = AssetDatabase.LoadAssetAtPath<Sector>($"Assets/Prefabs/Sectors/Preview/{name}.prefab"),
            locale = locale
        };

        private static LocaleSky Locale(string path) => AssetDatabase.LoadAssetAtPath<LocaleSky>(path);

        private static LocaleSky RowLocale(SectorRun run, int row) => PreviewHost.Named(run, run.Sectors[row]).locale;
    }
}
