using System;
using System.Collections;
using System.Collections.Generic;
using Combat.Projectiles;
using Combat.Weapons;
using Combat.Weapons.Arsenal;
using NUnit.Framework;
using Ships.Command;
using UI.Markers;
using UnityEngine;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Tests.PlayMode
{
    /// <summary>
    /// The HUD's target markers follow the launch readouts it is bound to: one marker per charge in
    /// flight, gone when the charge ends, dropped with a rebind, and hidden with the overlay.
    /// </summary>
    [Category("Weapons")]
    public class ChargeTargetMarkersPlayModeTests
    {
        private const string OverlayPrefabPath = "Assets/Prefabs/UI/UIOverlay.prefab";

        private sealed class FakeFlight : IChargeFlight
        {
            public Vector3 TargetPoint { get; set; }
            public float BlastRadius { get; set; } = 12f;
            public event Action Ended;
            public void End() => Ended?.Invoke();
        }

        private sealed class FakeLaunches : IChargeLaunchReadout
        {
            public event Action<IChargeFlight> Launched;
            public void Launch(IChargeFlight flight) => Launched?.Invoke(flight);
        }

        private sealed class FakeReadouts : IWeaponReadouts
        {
            private readonly IWeaponReadout[] readouts;

            public FakeReadouts(params IWeaponReadout[] readouts) => this.readouts = readouts;

            public IReadOnlyList<WeaponSlot> Slots => new[] { WeaponSlot.Secondary };
            public string DisplayName(WeaponSlot slot) => "Grenades";
            public IReadOnlyList<IWeaponReadout> Readouts(WeaponSlot slot) => readouts;
        }

        private GameObject host;

        [TearDown]
        public void TearDown()
        {
            if (host) UnityEngine.Object.DestroyImmediate(host);
        }

        private ChargeTargetMarkers CreateMarkers(IWeaponReadouts weapons)
        {
            host = new GameObject("ChargeTargetMarkersTest");
            var markers = host.AddComponent<ChargeTargetMarkers>();
            markers.Initialize(weapons);
            return markers;
        }

        [UnityTest]
        public IEnumerator Launch_AddsAMarkerAtTheTargetPoint_UntilTheChargeEnds()
        {
            var launches = new FakeLaunches();
            var markers = CreateMarkers(new FakeReadouts(launches));
            var flight = new FakeFlight { TargetPoint = new Vector3(4f, -3f, 0f) };

            launches.Launch(flight);

            Assert.AreEqual(1, markers.transform.childCount, "One marker per charge in flight.");
            var rings = markers.GetComponentsInChildren<LineRenderer>();
            Assert.AreEqual(2, rings.Length, "A ring at the point and a ring at the blast radius.");
            Assert.AreEqual(flight.BlastRadius, Vector3.Distance(rings[1].GetPosition(0), flight.TargetPoint), 0.001f,
                "The faint ring traces the blast radius around the target point.");

            flight.End();
            yield return null;

            Assert.AreEqual(0, markers.transform.childCount, "The marker goes when its charge ends.");
        }

        [UnityTest]
        public IEnumerator Rebind_DropsTheOldSourcesAndTheirMarkers()
        {
            var oldLaunches = new FakeLaunches();
            var markers = CreateMarkers(new FakeReadouts(oldLaunches));
            oldLaunches.Launch(new FakeFlight());

            markers.Initialize(new FakeReadouts(new FakeLaunches()));
            yield return null;
            oldLaunches.Launch(new FakeFlight());

            Assert.AreEqual(0, markers.transform.childCount, "A rebind clears live markers and stops listening to the old weapons.");
        }

        [Test]
        public void HiddenOverlay_HidesLiveAndNewMarkers()
        {
            var launches = new FakeLaunches();
            var markers = CreateMarkers(new FakeReadouts(launches));
            launches.Launch(new FakeFlight());

            markers.SetVisible(false);
            launches.Launch(new FakeFlight());

            foreach (Transform marker in markers.transform)
                Assert.IsFalse(marker.gameObject.activeSelf, "Markers hide with the overlay.");

            markers.SetVisible(true);
            foreach (Transform marker in markers.transform)
                Assert.IsTrue(marker.gameObject.activeSelf, "…and come back with it.");
        }

        [Test]
        public void OverlayPrefab_CarriesTheChargeMarkers()
        {
#if UNITY_EDITOR
            var overlay = AssetDatabase.LoadAssetAtPath<GameObject>(OverlayPrefabPath);
            Assert.IsNotNull(overlay, $"Failed to load {OverlayPrefabPath}");
            Assert.IsNotNull(overlay.GetComponentInChildren<ChargeTargetMarkers>(true),
                "The player's HUD carries the charge target markers.");
#else
            Assert.Ignore("Requires Unity Editor assets.");
#endif
        }
    }
}
