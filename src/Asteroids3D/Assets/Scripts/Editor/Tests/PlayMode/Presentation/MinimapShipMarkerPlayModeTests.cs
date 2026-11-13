#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using Combat.Targeting;
using NUnit.Framework;
using Ships;
using Ships.Presentation;
using Ships.Visuals;
using Substrate;
using UI.Screens;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Tests.PlayMode.Presentation
{
    [Category("UI"), Category("RequiresLfsPng")]
    public sealed class MinimapShipMarkerPlayModeTests
    {
        private readonly List<GameObject> created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in created)
                if (go) Object.Destroy(go);
            created.Clear();
        }

        [Test]
        public void EveryChassisMarker_DrawsItsKillMarkerSilhouette()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/Ships" }))
            {
                var ship = AssetDatabase.LoadAssetAtPath<Ship>(AssetDatabase.GUIDToAssetPath(guid));
                if (!ship || ship.name == "ShipBase") continue;
                var root = Spawn(ship.name);
                var marker = root.GetComponentInChildren<MinimapShipMarker>();
                var renderer = root.GetComponentInChildren<MeshRenderer>();
                var lockChannel = new LockChannel();
                marker.Bind(new ShipView(root.transform, null, null, lockChannel, false));
                var texture = Resources.Load<Texture2D>(RunRecapScreen.SilhouetteFolder + "/" + ship.name);
                Assert.IsNotNull(texture, ship.name);
                var block = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block);
                Assert.AreSame(texture, block.GetTexture("_MainTex"), ship.name);
            }
        }

        [Test]
        public void Bind_NonPlayer_SwitchesToEnemyLayer()
        {
            if (LayerIds.MinimapEnemy < 0)
                Assert.Ignore("Minimap_Enemy layer not defined in this project.");
            var root = Spawn(FirstChassis());
            var marker = root.GetComponentInChildren<MinimapShipMarker>();
            var lockChannel = new LockChannel();
            marker.Bind(new ShipView(root.transform, null, null, lockChannel, false));
            Assert.AreEqual(LayerIds.MinimapEnemy, marker.gameObject.layer);
        }

        [Test]
        public void Bind_Player_LeavesLayerUntouched()
        {
            if (LayerIds.MinimapEnemy < 0)
                Assert.Ignore("Minimap_Enemy layer not defined in this project.");
            var root = Spawn(FirstChassis());
            var marker = root.GetComponentInChildren<MinimapShipMarker>();
            var lockChannel = new LockChannel();
            marker.Bind(new ShipView(root.transform, null, null, lockChannel, true));
            Assert.AreEqual(0, marker.gameObject.layer);
        }

        [UnityTest]
        public IEnumerator EnemyMarker_HidesWhileConcealed()
        {
            var root = Spawn(FirstChassis());
            var marker = root.GetComponentInChildren<MinimapShipMarker>();
            var renderer = root.GetComponentInChildren<MeshRenderer>();
            var lockChannel = new LockChannel();
            marker.Bind(new ShipView(root.transform, null, null, lockChannel, false));
            lockChannel.Conceal();
            yield return null;
            Assert.IsFalse(renderer.enabled);
            lockChannel.Reveal();
            yield return null;
            Assert.IsTrue(renderer.enabled);
        }

        private GameObject Spawn(string chassis)
        {
            var root = new GameObject(chassis + "(Clone)");
            created.Add(root);
            var markerObject = new GameObject("MinimapMarker") { layer = 0 };
            markerObject.transform.SetParent(root.transform, false);
            markerObject.AddComponent<MeshFilter>();
            markerObject.AddComponent<MeshRenderer>();
            markerObject.AddComponent<MinimapShipMarker>();
            return root;
        }

        private static string FirstChassis()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/Ships" }))
            {
                var ship = AssetDatabase.LoadAssetAtPath<Ship>(AssetDatabase.GUIDToAssetPath(guid));
                if (ship && ship.name != "ShipBase") return ship.name;
            }

            throw new InvalidOperationException("No chassis prefab under Assets/Prefabs/Ships.");
        }
    }
}
#endif
