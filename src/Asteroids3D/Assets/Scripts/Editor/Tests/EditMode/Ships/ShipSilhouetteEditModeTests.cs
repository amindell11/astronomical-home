using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Ships;
using Ships.Loadout;
using Ships.Presentation;
using Ships.Visuals;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode
{
    // Covers legacy chassis too: the hangar frames every offered ship from these references.
    [Category("Ships")]
    public sealed class ShipSilhouetteEditModeTests
    {
        public static IEnumerable<TestCaseData> RiggedChassis()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>(ItemCatalog.AssetPath);
            foreach (var chassis in catalog.Chassis)
                if (chassis.GetComponentInChildren<ShipVisualRig>(true))
                    yield return new TestCaseData(AssetDatabase.GetAssetPath(chassis)).SetName($"{{m}}({chassis.name})");
        }

        [TestCaseSource(nameof(RiggedChassis))]
        public void Chassis_WiresItsRigAndEveryHullRenderer(string path)
        {
            var ship = AssetDatabase.LoadAssetAtPath<Ship>(path);
            var rig = ship.GetComponentInChildren<ShipVisualRig>(true);
            Assert.That(ship.VisualRig, Is.SameAs(rig), "The ship's rig reference is its own visual rig.");

            var hull = rig.GetComponentsInChildren<Renderer>(true)
                .Where(r => (r as MeshRenderer || r as SkinnedMeshRenderer) && !r.GetComponentInParent<MinimapShipMarker>(true));
            Assert.That(rig.Silhouette, Is.EquivalentTo(hull), "The silhouette lists every hull renderer and nothing else.");
        }
    }
}
