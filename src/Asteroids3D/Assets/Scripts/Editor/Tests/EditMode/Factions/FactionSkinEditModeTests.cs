using System.Collections.Generic;
using NUnit.Framework;
using Ships;
using Ships.Presentation;
using Substrate.Sectors.Factions;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode.Factions
{
    /// <summary>A faction skin on a primitive ship's hull: a ship of the faction wears the faction's material in place of the one it carries, and the elite wears its own over it.</summary>
    [TestFixture]
    [Category("Sectors")]
    public class FactionSkinEditModeTests
    {
        private readonly List<Object> created = new();
        private Material carried;
        private Material factionPaint;
        private Material elitePaint;
        private FactionSkin skin;

        [SetUp]
        public void SetUp()
        {
            carried = Track(new Material(Shader.Find("Hidden/InternalErrorShader")));
            factionPaint = Track(new Material(carried));
            elitePaint = Track(new Material(carried));
            skin = new FactionSkin
            {
                swaps = new[] { new MaterialSwap { from = carried, to = factionPaint } },
                elite = new[] { new MaterialSwap { from = carried, to = elitePaint } },
            };
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var item in created)
                if (item) Object.DestroyImmediate(item);
            created.Clear();
        }

        [Test]
        public void Dress_SwapsTheFactionsMaterialIn()
        {
            var (ship, hull) = NewShip();

            skin.Dress(ship, isElite: false);

            Assert.AreSame(factionPaint, hull.sharedMaterial);
        }

        [Test]
        public void Dress_TheEliteWearsItsOwnSwapOverTheFactions()
        {
            var (ship, hull) = NewShip();

            skin.Dress(ship, isElite: true);

            Assert.AreSame(elitePaint, hull.sharedMaterial);
        }

        private (Ship ship, Renderer hull) NewShip()
        {
            var ship = Track(new GameObject("Ship")).AddComponent<Ship>();
            var rigObject = new GameObject("VisualRig");
            rigObject.transform.SetParent(ship.transform, false);
            var hull = rigObject.AddComponent<MeshRenderer>();
            hull.sharedMaterial = carried;
            var rig = rigObject.AddComponent<ShipVisualRig>();

            var rigFields = new SerializedObject(rig);
            var silhouette = rigFields.FindProperty("silhouette");
            silhouette.arraySize = 1;
            silhouette.GetArrayElementAtIndex(0).objectReferenceValue = hull;
            rigFields.ApplyModifiedPropertiesWithoutUndo();

            var shipFields = new SerializedObject(ship);
            shipFields.FindProperty("visualRig").objectReferenceValue = rig;
            shipFields.ApplyModifiedPropertiesWithoutUndo();
            return (ship, hull);
        }

        private T Track<T>(T item) where T : Object
        {
            created.Add(item);
            return item;
        }
    }
}
