using Cameras;
using Game.Travel;
using Substrate.Sectors;
using Substrate.Sessions;
using UI.Recap;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Preview
{
    /// <summary>
    /// Builds the preview host with the game's rig and presentation assets in an empty scene.
    /// Playtest uses normal combat or the two-sector crowded jump fixture.
    /// The chosen mode is assigned before activation, so Playtest never composes an inspection session.
    /// </summary>
    public static class PreviewHostBootstrap
    {
        private const string PlayerRigPath = "Assets/Prefabs/MiscObjects/PlayerRig.prefab";
        private const string ObserverCamPath = "Assets/Prefabs/Cameras/Main Camera.prefab";
        private const string SectorPrefabPath = "Assets/Prefabs/Sectors/TrialSector.prefab";
        private const string SectorConfigPath = "Assets/Settings/Game/SurvivalTrialConfig.asset";
        internal const string RecapFeelPath = "Assets/Settings/UI/RecapFeel.asset";

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
            host.playerRigPrefab = AssetDatabase.LoadAssetAtPath<PlayerRig>(PlayerRigPath);
            host.observerCamPrefab = AssetDatabase.LoadAssetAtPath<ObserverCam>(ObserverCamPath);
            host.recapFeel = AssetDatabase.LoadAssetAtPath<RecapFeelProfile>(RecapFeelPath);
            host.jumpFeel = AssetDatabase.LoadAssetAtPath<SectorJumpProfile>("Assets/Settings/Game/SectorJumpTuning.asset");
            host.jumpShader = Shader.Find("Astronomical/Travel/SectorJump");
            host.jumpPlaytestProfile = new SessionProfile
            {
                sectors = new[] { JumpEntry("CrowdedJumpDeparture"), JumpEntry("CrowdedJumpArrival") }
            };
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
