using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Ships.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.PlayMode.Presentation
{
    [Category("Ships")]
    public sealed class HullVisualsWearPlayModeTests
    {
        private const string ValisPath = "Assets/Prefabs/Ships/Valis.prefab";

        private GameObject rig;
        private readonly List<Material> worn = new();

        [TearDown]
        public void TearDown()
        {
            if (rig) Object.Destroy(rig);
            foreach (var material in worn)
                if (material) Object.Destroy(material);
        }

        [UnityTest]
        public IEnumerator Wear_ReReadsHullColour_AfterDisableAndEnable()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ValisPath);
            rig = Object.Instantiate(prefab.GetComponentInChildren<ShipVisualRig>(true).gameObject);
            yield return null;

            var colour = new Color(0.15f, 0.55f, 0.85f, 1f);
            var visual = rig.GetComponent<ShipVisualRig>();
            foreach (var surface in visual.Silhouette)
            foreach (var material in surface.sharedMaterials)
            {
                var copy = new Material(material);
                if (copy.HasProperty("_BaseColor")) copy.SetColor("_BaseColor", colour);
                worn.Add(copy);
            }
            visual.Wear(new ShipLivery(worn, null, default));
            rig.SetActive(false);
            rig.SetActive(true);

            var block = new MaterialPropertyBlock();
            var checkedOne = false;
            foreach (var surface in visual.Silhouette)
            {
                if (!surface.sharedMaterial || !surface.sharedMaterial.HasProperty("_BaseColor")) continue;
                surface.GetPropertyBlock(block);
                Assert.That(Vector4.Distance(block.GetColor("_BaseColor"), colour), Is.LessThan(1e-4f), surface.name);
                checkedOne = true;
            }
            Assert.IsTrue(checkedOne, "a silhouette renderer wears _BaseColor");
        }
    }
}
