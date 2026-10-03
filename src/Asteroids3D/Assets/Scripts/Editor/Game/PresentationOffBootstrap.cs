using Cameras;
using Substrate.Sectors;
using Substrate.Sessions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Edit-mode entry the live CLI lane calls to get a presentation-off game: a <see cref="GameHost"/>
    /// in a new empty scene, given the rig, camera and sector assets InitScene serializes, then play.
    /// The splash, hangar screen and hangar offer stay unset: a presentation-off host shows neither screen.
    /// </summary>
    public static class PresentationOffBootstrap
    {
        private const string PlayerRigPath = "Assets/Prefabs/MiscObjects/PlayerRig.prefab";
        private const string ObserverCamPath = "Assets/Prefabs/Cameras/Main Camera.prefab";
        private const string SectorPrefabPath = "Assets/Prefabs/Sectors/TrialSector.prefab";
        private const string SectorConfigPath = "Assets/Settings/Game/SurvivalTrialConfig.asset";

        public static void EnterPlayMode()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            BuildHost();
            EditorApplication.EnterPlaymode();
        }

        internal static GameHost BuildHost()
        {
            var host = new GameObject("GameHost").AddComponent<GameHost>();
            host.sessionProfile = new SessionProfile
            {
                sectorEntry = new SectorEntry
                {
                    prefab = AssetDatabase.LoadAssetAtPath<Sector>(SectorPrefabPath),
                    config = AssetDatabase.LoadAssetAtPath<SectorSettings>(SectorConfigPath)
                },
                presentation = false
            };
            host.playerRig = AssetDatabase.LoadAssetAtPath<PlayerRig>(PlayerRigPath);
            host.observerCamPrefab = AssetDatabase.LoadAssetAtPath<ObserverCam>(ObserverCamPath);
            return host;
        }
    }
}
