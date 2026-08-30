#if UNITY_EDITOR
using Game.Diagnostics;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>Pins the per-subview default the marker relies on: on a pref miss, <c>IsOn</c> returns the
    /// <c>defaultOn</c> declared at registration — true for a default-on subview, false otherwise.</summary>
    [Category("Core")]
    public class GizmoViewRegistryEditModeTests
    {
        private const string OnKey = "test-default-on";
        private const string OffKey = "test-default-off";

        [SetUp]
        public void SetUp()
        {
            GizmoView.Register(typeof(Transform), OnKey, "On", "", "Test", defaultOn: true);
            GizmoView.Register(typeof(Transform), OffKey, "Off", "", "Test");
            EditorPrefs.DeleteKey($"GizmoView.Transform.{OnKey}");
            EditorPrefs.DeleteKey($"GizmoView.Transform.{OffKey}");
        }

        [TearDown]
        public void TearDown()
        {
            EditorPrefs.DeleteKey($"GizmoView.Transform.{OnKey}");
            EditorPrefs.DeleteKey($"GizmoView.Transform.{OffKey}");
        }

        [Test]
        public void IsOn_OnPrefMiss_HonorsRegisteredDefault()
        {
            Assert.IsTrue(GizmoView.IsOn(typeof(Transform), OnKey), "default-on subview should read on");
            Assert.IsFalse(GizmoView.IsOn(typeof(Transform), OffKey), "default-off subview should read off");
        }

        [Test]
        public void IsOn_ExplicitPref_OverridesDefault()
        {
            GizmoView.SetOn(typeof(Transform), OnKey, false);
            Assert.IsFalse(GizmoView.IsOn(typeof(Transform), OnKey), "explicit off must win over default-on");
        }
    }
}
#endif
