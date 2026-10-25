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
    /// Edit-mode entry the control panel calls to get a preview host: a <see cref="PreviewHost"/> in a
    /// new empty scene over the game's first sector, given the rig, camera, sector and feel assets
    /// InitScene serializes, then play. The presentation-on sibling of <see cref="PresentationOffBootstrap"/>.
    /// </summary>
    public static class PreviewHostBootstrap
    {
        private const string PlayerRigPath = "Assets/Prefabs/MiscObjects/PlayerRig.prefab";
        private const string ObserverCamPath = "Assets/Prefabs/Cameras/Main Camera.prefab";
        private const string SectorPrefabPath = "Assets/Prefabs/Sectors/TrialSector.prefab";
        private const string SectorConfigPath = "Assets/Settings/Game/SurvivalTrialConfig.asset";
        internal const string RecapFeelPath = "Assets/Settings/UI/RecapFeel.asset";

        /// <summary>False when the user kept an unsaved scene open instead.</summary>
        public static bool EnterPlayMode(PreviewSubjectKind subject = PreviewSubjectKind.RunRecap)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            BuildHost(subject);
            EditorApplication.EnterPlaymode();
            return true;
        }

        internal static PreviewHost BuildHost(PreviewSubjectKind subject = PreviewSubjectKind.RunRecap)
        {
            var host = new GameObject("PreviewHost").AddComponent<PreviewHost>();
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
            host.subjectKind = subject;
            return host;
        }
    }
}
