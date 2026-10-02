using System.Linq;
using NUnit.Framework;
using Ships;
using Ships.Loadout;
using Ships.Weapons;
using Ships.Presentation;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode.Rendering.Illustrated
{
    [Category("Ships")]
    public sealed class ValisPrefabEditModeTests
    {
        private static Ship Load(string name) =>
            AssetDatabase.LoadAssetAtPath<Ship>($"Assets/Prefabs/Ships/{name}.prefab");

        [Test]
        public void RosterOffersValisSeparatelyWithShip3ChassisAndLoadout()
        {
            var valis = Load("Valis");
            var baseline = Load("Ship_3");
            var roster = AssetDatabase.LoadAssetAtPath<ItemSubset>("Assets/Settings/Ships/PlayerLoadout.asset");
            Assert.That(roster.ships.Count(s => s == valis), Is.EqualTo(1));
            Assert.That(roster.ships, Does.Contain(baseline));
            Assert.That(valis.Engine, Is.SameAs(baseline.Engine));
            Assert.That(valis.Shield, Is.SameAs(baseline.Shield));
            Assert.That(valis.mass, Is.EqualTo(baseline.mass));
            Assert.That(valis.maxHealth, Is.EqualTo(baseline.maxHealth));
            Assert.That(valis.maxBankAngle, Is.EqualTo(baseline.maxBankAngle));
            Assert.That(valis.startingLives, Is.EqualTo(baseline.startingLives));
            var weapons = valis.GetComponent<WeaponsController>();
            var original = baseline.GetComponent<WeaponsController>();
            Assert.That(weapons.PrimaryMountPrefab, Is.SameAs(original.PrimaryMountPrefab));
            Assert.That(weapons.SecondaryMountPrefab, Is.SameAs(original.SecondaryMountPrefab));
        }

        [Test]
        public void HullFacesForwardAndFitsItsConvexCollider()
        {
            var ship = Load("Valis");
            var rig = ship.GetComponentsInChildren<ShipVisualRig>(true).Single();
            var hull = rig.transform.Find("Valis");
            var mesh = hull.GetComponent<MeshFilter>().sharedMesh;
            var collider = ship.GetComponentInChildren<MeshCollider>();
            Assert.That(collider.convex, Is.True);
            Assert.That(mesh.subMeshCount, Is.EqualTo(8));
            var bounds = collider.sharedMesh.bounds;
            bounds.Expand(.002f);
            foreach (var vertex in mesh.vertices)
                Assert.That(bounds.Contains(collider.transform.InverseTransformPoint(hull.TransformPoint(vertex))), Is.True);
            var canopy = mesh.GetIndices(5).Distinct().Select(i => mesh.vertices[i]).ToArray();
            Assert.That(canopy.Average(v => v.y), Is.GreaterThan(0), "Canopy is at the forward end.");
            Assert.That(canopy.Average(v => v.z), Is.LessThan(0), "Canopy faces the game camera.");
            foreach (Transform mount in ship.transform.Find("Hardpoints"))
                Assert.That(mount.localPosition.y, Is.GreaterThan(mesh.bounds.max.y));
        }

        [Test]
        public void DefaultPaletteRetainsIndependentRegionsAndNeutralDamageTint()
        {
            var hull = Load("Valis").transform.Find("Valis VisualRig/Valis").GetComponent<MeshRenderer>();
            Assert.That(hull.sharedMaterials, Has.Length.EqualTo(8));
            foreach (var material in hull.sharedMaterials.Take(7))
            {
                Assert.That(AssetDatabase.GetAssetPath(material), Does.Contain("jade-iris"));
                Assert.That(material.GetColor("_BaseColor"), Is.EqualTo(Color.white));
                Assert.That(material.GetFloat("_TextureStrength"), Is.Zero);
            }
            Assert.That(hull.sharedMaterials.Take(7).Select(m => m.GetColor("_PaperColor")).Distinct().Count(), Is.EqualTo(7));
            Assert.That(hull.sharedMaterials[7].shader.name, Is.EqualTo("Astronomical/Comparison/Drawn Contour"));
        }
    }
}

