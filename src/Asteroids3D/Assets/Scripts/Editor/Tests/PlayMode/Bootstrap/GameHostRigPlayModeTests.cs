#if UNITY_EDITOR
using System.Collections;
using Cameras;
using Game;
using NUnit.Framework;
using Substrate.Sectors;
using Substrate.Sessions;
using Substrate.Services.Units;
using Tests.PlayMode.Common;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.PlayMode.Bootstrap
{
    /// <summary>
    /// The host is handed the rig as a prefab asset, as InitScene binds it, and must build the
    /// player on its own scene copy — never on the asset, which gets no lifecycle and outlives the session.
    /// </summary>
    [Category("Bootstrap")]
    public class GameHostRigPlayModeTests : PlayModeWorldFixture
    {
        private const string RigPrefabPath = "Assets/Prefabs/MiscObjects/PlayerRig.prefab";
        private const string ObserverCamPrefabPath = "Assets/Prefabs/Cameras/Main Camera.prefab";
        private const string SectorPrefabPath = "Assets/Prefabs/Sectors/ArenaSector.prefab";
        private const string SectorConfigPath = "Assets/Settings/Game/DefaultSectorConfig.asset";
        private const float BootTimeoutSec = 30f;

        private GameObject hostGo;

        public override void TearDown()
        {
            if (hostGo) hostGo.GetComponent<UnitService>().Clear();
            DestroyTestObject(hostGo);
            base.TearDown();
        }

        [UnityTest]
        public IEnumerator Boot_BuildsThePlayerOnASceneCopyOfTheRigPrefab()
        {
            var rigPrefab = AssetDatabase.LoadAssetAtPath<PlayerRig>(RigPrefabPath);
            Assert.IsNotNull(rigPrefab, $"PlayerRig prefab loads from {RigPrefabPath}");
            Assert.IsFalse(rigPrefab.gameObject.scene.IsValid(), "test premise: the rig handed in is an asset");

            hostGo = new GameObject("TestHost");
            hostGo.SetActive(false);
            var host = hostGo.AddComponent<GameHost>();
            host.sessionProfile = new SessionProfile
            {
                sectorEntry = new SectorEntry
                {
                    prefab = AssetDatabase.LoadAssetAtPath<Sector>(SectorPrefabPath),
                    config = AssetDatabase.LoadAssetAtPath<SectorSettings>(SectorConfigPath)
                },
                presentation = false
            };
            host.playerRig = rigPrefab;
            host.observerCamPrefab = AssetDatabase.LoadAssetAtPath<ObserverCam>(ObserverCamPrefabPath);
            hostGo.SetActive(true);

            // The first sector load follows the rig build, so a loaded sector means the rig is built.
            var deadline = Time.realtimeSinceStartup + BootTimeoutSec;
            while (!host.ActiveSector && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(host.ActiveSector, "the host reached its first sector load");

            var rig = hostGo.GetComponentInChildren<PlayerRig>();
            Assert.IsNotNull(rig, "the host holds a rig under itself");
            Assert.AreNotSame(rigPrefab, rig, "the host built on a copy, not the prefab asset");
            Assert.IsTrue(rig.gameObject.scene.IsValid(), "the rig the host builds on is a scene instance");
            Assert.IsTrue(rig.Player, "the copy carries the built player");
            Assert.IsFalse(rigPrefab.Player, "the prefab asset was never built on");
        }
    }
}
#endif
