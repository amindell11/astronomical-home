using NUnit.Framework;
using Ships;
using Ships.Presentation;
using UnityEngine;

namespace Tests.EditMode
{
    [Category("Ship")]
    public class MinimapLayerSetterEditModeTests
    {
        private GameObject go;
        private MinimapLayerSetter setter;
        private int enemyLayer;

        [SetUp]
        public void SetUp()
        {
            enemyLayer = LayerMask.NameToLayer("Minimap_Enemy");
            if (enemyLayer < 0) Assert.Ignore("Minimap_Enemy layer not defined in this project.");
            go = new GameObject("MapMesh") { layer = 0 };
            setter = go.AddComponent<MinimapLayerSetter>();
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
            setter.Bind(View(isPlayer: false));
            Assert.AreEqual(enemyLayer, go.layer);
        }

        [Test]
        public void Bind_Player_LeavesLayerUntouched()
        {
            setter.Bind(View(isPlayer: true));
            Assert.AreEqual(0, go.layer);
        }
    }
}
