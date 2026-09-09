#if UNITY_EDITOR
using System;
using Game.Diagnostics;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>Pins the global Colliders toggle to Unity's real gizmo state: setting it flips every
    /// built-in collider type's display through GizmoUtility, the pref round-trips, and the API this
    /// version exposes actually covers built-in colliders (the arc's open risk).</summary>
    [Category("Core")]
    public class GizmoViewCollidersEditModeTests
    {
        private static readonly Type[] ColliderTypes =
        {
            typeof(BoxCollider), typeof(SphereCollider), typeof(CapsuleCollider), typeof(MeshCollider),
        };

        private bool priorColliders;
        private bool[] priorEnabled;

        [SetUp]
        public void SetUp()
        {
            priorColliders = GizmoView.CollidersOn;
            priorEnabled = new bool[ColliderTypes.Length];
            for (var i = 0; i < ColliderTypes.Length; i++)
            {
                Assert.IsTrue(GizmoUtility.TryGetGizmoInfo(ColliderTypes[i], out var info),
                    $"{ColliderTypes[i].Name} has no registered Unity gizmo — the native collider toggle would no-op.");
                priorEnabled[i] = info.gizmoEnabled;
            }
        }

        [TearDown]
        public void TearDown()
        {
            for (var i = 0; i < ColliderTypes.Length; i++)
                GizmoUtility.SetGizmoEnabled(ColliderTypes[i], priorEnabled[i], false);
            GizmoView.CollidersOn = priorColliders;
        }

        [Test]
        public void CollidersOn_FlipsEveryColliderTypeDisplay_AndPrefRoundTrips()
        {
            GizmoView.CollidersOn = true;
            Assert.IsTrue(GizmoView.CollidersOn, "pref did not round-trip on");
            AssertAllColliderDisplay(true);

            GizmoView.CollidersOn = false;
            Assert.IsFalse(GizmoView.CollidersOn, "pref did not round-trip off");
            AssertAllColliderDisplay(false);
        }

        private static void AssertAllColliderDisplay(bool expected)
        {
            foreach (var type in ColliderTypes)
            {
                Assert.IsTrue(GizmoUtility.TryGetGizmoInfo(type, out var info));
                Assert.AreEqual(expected, info.gizmoEnabled, $"{type.Name} display state");
            }
        }
    }
}
#endif
