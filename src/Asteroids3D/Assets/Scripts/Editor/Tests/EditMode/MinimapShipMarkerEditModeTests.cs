using NUnit.Framework;
using Ships.Visuals;
using Substrate;
using Ships.Presentation;
using UnityEngine;

namespace Tests.EditMode
{
    [Category("UI")]
    public class MinimapShipMarkerEditModeTests
    {
        private GameObject go;
        private MinimapShipMarker marker;
        private int enemyLayer;

        [SetUp]
        public void SetUp()
        {
            enemyLayer = LayerIds.MinimapEnemy;
            if (enemyLayer < 0) Assert.Ignore("Minimap_Enemy layer not defined in this project.");
            go = new GameObject("MapMesh") { layer = 0 };
            marker = go.AddComponent<MinimapShipMarker>();
        }

        [TearDown]
        public void TearDown()
        {
            if (go) Object.DestroyImmediate(go);
        }

        private ShipView View(bool isPlayer) =>
            new ShipView(go.transform, null, null, null, isPlayer);

        [Test]
        public void Bind_NonPlayer_SwitchesToEnemyLayer()
        {
            marker.Bind(View(isPlayer: false));
            Assert.AreEqual(enemyLayer, go.layer);
        }

        [Test]
        public void Bind_Player_LeavesLayerUntouched()
        {
            marker.Bind(View(isPlayer: true));
            Assert.AreEqual(0, go.layer);
        }
    }
}
