using System;
using Damage;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode.Damage
{
    [Category("Damage")]
    public class DamageableCollidersEditModeTests
    {
        private sealed class Fake : MonoBehaviour, IDamageable
        {
            public float TakeDamage(in DamageInfo hit) => 0f;
        }

        private Fake alpha;
        private Fake beta;

        [TearDown]
        public void TearDown()
        {
            Cleanup(alpha);
            Cleanup(beta);
        }

        [Test]
        public void Register_Lookup_Unregister_AndDoubleRegistration()
        {
            alpha = NewFake("Alpha", out var first);
            var child = new GameObject("Child");
            child.transform.SetParent(alpha.transform);
            var second = child.AddComponent<SphereCollider>();
            DamageableColliders.Register(alpha, new[] { first, second });

            Assert.IsTrue(DamageableColliders.TryGet(first, out var found));
            Assert.AreSame(alpha, found);
            Assert.IsTrue(DamageableColliders.TryGet(second, out found));
            Assert.AreSame(alpha, found);

            DamageableColliders.Unregister(alpha);
            Assert.IsFalse(DamageableColliders.TryGet(first, out _));
            Assert.IsFalse(DamageableColliders.TryGet(second, out _));

            DamageableColliders.Register(alpha, new[] { first });
            beta = NewFake("Beta", out _);
            var ex = Assert.Throws<InvalidOperationException>(() =>
                DamageableColliders.Register(beta, new[] { first }));
            StringAssert.Contains("Fake on 'Alpha'", ex.Message);
            StringAssert.Contains("Fake on 'Beta'", ex.Message);
        }

        private static Fake NewFake(string name, out Collider collider)
        {
            var go = new GameObject(name);
            collider = go.AddComponent<BoxCollider>();
            return go.AddComponent<Fake>();
        }

        private static void Cleanup(Fake fake)
        {
            if (!fake) return;
            DamageableColliders.Unregister(fake);
            Object.DestroyImmediate(fake.gameObject);
        }
    }
}
