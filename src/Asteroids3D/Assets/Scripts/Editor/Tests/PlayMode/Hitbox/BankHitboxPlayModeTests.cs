using System.Linq;
using NUnit.Framework;
using Ships;
using Ships.Hitbox;
using Substrate;
using Substrate.Services;
using Substrate.Services.Units;
using Tests.PlayMode.Common;
using UnityEditor;
using UnityEngine;

namespace Tests.PlayMode.Hitbox
{
    [Category("Ships")]
    public sealed class BankHitboxPlayModeTests : PlayModeWorldFixture
    {
        private GameObject host;
        private UnitService units;

        public override void SetUp()
        {
            base.SetUp();
            host = new GameObject("[BankHitboxTestServices]");
            units = host.AddComponent<UnitService>();
            ShipServices.Compose(units, host.transform, presentationEnabled: false);
        }

        public override void TearDown()
        {
            units.Clear();
            Object.DestroyImmediate(host);
            base.TearDown();
        }

        [TestCase("Bulwark")]
        [TestCase("Crimson")]
        [TestCase("Valis")]
        [TestCase("ValisMinion")]
        public void Spawn_SetsEachHitboxDepthToItsHittableShareAtFullBank(string name)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<Ship>($"Assets/Prefabs/Ships/{name}.prefab");
            var tuning = new SerializedObject(prefab.GetComponent<BankHitbox>());
            var share = ((HitboxFeelProfile)tuning.FindProperty("feel").objectReferenceValue).BankHittableShare;
            var wings = Enumerable.Range(0, tuning.FindProperty("wings").arraySize)
                .Select(i => tuning.FindProperty("wings").GetArrayElementAtIndex(i).objectReferenceValue.name).ToArray();
            var bank = prefab.maxBankAngle * Mathf.Deg2Rad;
            var bankDepth = Mathf.Sin(bank) * Mathf.Cos(bank);

            var ship = units.SpawnShip(prefab, null, 0, Vector3.zero, Quaternion.Euler(0f, 0f, 31f), Field);

            var hitboxes = ship.GetComponentsInChildren<MeshCollider>(true)
                .Where(c => !c.isTrigger && c.sharedMesh && c.gameObject.layer == LayerIds.Ship).ToArray();
            Assert.That(hitboxes, Is.Not.Empty);
            Assert.That(hitboxes.Count(h => wings.Contains(h.name)), Is.EqualTo(wings.Length));
            foreach (var hitbox in hitboxes)
            {
                var scale = tuning.FindProperty(wings.Contains(hitbox.name) ? "wingScale" : "bodyScale").floatValue;
                var toShip = ship.transform.worldToLocalMatrix * hitbox.transform.localToWorldMatrix;
                var vertices = hitbox.sharedMesh.vertices;
                var halfWidth = vertices.Max(v => Mathf.Abs(toShip.MultiplyPoint3x4(v).x));
                var axis = Enumerable.Range(0, 3).OrderBy(a => vertices.Max(v => Mathf.Abs(v[a])) - vertices.Min(v => Mathf.Abs(v[a]))).First();
                var unit = Vector3.zero;
                unit[axis] = 1f;
                var stretch = toShip.MultiplyVector(unit).magnitude;
                foreach (var vertex in vertices)
                    Assert.That(Mathf.Abs(vertex[axis]) * stretch, Is.EqualTo(share * scale * bankDepth * halfWidth).Within(1e-3f * halfWidth),
                        $"{name} {hitbox.name}");
            }
        }
    }
}
