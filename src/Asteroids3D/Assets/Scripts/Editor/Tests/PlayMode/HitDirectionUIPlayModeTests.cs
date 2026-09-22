using System;
using System.Collections;
using Damage;
using Movement;
using NUnit.Framework;
using Ships.Command;
using Ships.Damage;
using Ships.Registry;
using Substrate;
using Tests.PlayMode.Common;
using UI.PlayerState;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.PlayMode
{
    /// <summary>A damage event from a known bearing puts one arc pip at the matching screen edge, and the pip is gone once the fade has run. The widget is bound the way the overlay binds it (status + damage events), with no ship or camera in the scene.</summary>
    [Category("UI")]
    [Category("Slow")]
    public class HitDirectionUIPlayModeTests : PlayModeWorldFixture
    {
        private sealed class RaisableDamageEvents : IDamageEvents
        {
            public event Action<DamageInfo> OnDamaged;
            public event Action<ShipId, DamageInfo> OnDeath { add { } remove { } }
            public Resource Health { get; } = new Resource(100f);
            public RegenResource Shield { get; } = new RegenResource(50f, 0f, 999f);

            public void Raise(DamageInfo hit) => OnDamaged?.Invoke(hit);
        }

        private sealed class OriginStatus : IShipStatus
        {
            public ShipId Id => default;
            public Transform Transform => null;
            public Kinematics Kinematics => default;
            public Dynamics Dynamics => default;
            public float HealthPct => 1f;
            public float ShieldPct => 1f;
            public bool BoostAvailable => true;
            public float BoostCooldownRemaining => 0f;
            public float BoostCooldownPct => 0f;
            public float MaxSpeed => 10f;
            public float MaxYawRate => 90f;
        }

        private const float FadeSeconds = 0.8f;

        private GameObject canvasRoot;

        public override void TearDown()
        {
            DestroyTestObject(canvasRoot);
            canvasRoot = null;
            base.TearDown();
        }

        [UnityTest]
        public IEnumerator RestingHit_FromAKnownBearing_PipsAtThatScreenEdge_ThenFadesOut()
        {
            var widget = NewWidget(out var events);
            yield return null;

            events.Raise(HitAt(new Vector2(6f, 0f)));
            events.Raise(HitAt(new Vector2(0f, -4f)));
            yield return null;

            Assert.AreEqual(2, widget.PipCount, "one pip per hit");
            Assert.AreEqual(0f, widget.PipAngleDeg(0), 0.5f, "a hit from +X reads at the right edge");
            Assert.AreEqual(-90f, widget.PipAngleDeg(1), 0.5f, "a hit from -Y reads at the bottom edge");

            var born = Time.time;
            yield return new WaitUntil(() => Time.time - born > FadeSeconds + 0.1f);
            yield return null;
            Assert.AreEqual(0, widget.PipCount, "pips are gone once the fade has run");
        }

        [UnityTest]
        public IEnumerator MovingHit_BearsAlongTheReversedHitVelocity_NotTheHitPoint()
        {
            var widget = NewWidget(out var events);
            yield return null;

            var travellingLeft = GamePlane.PlaneDirToWorld(new Vector2(-30f, 0f));
            events.Raise(new DamageInfo(5f, DamageKind.Laser, ShipId.Invalid, 1f, travellingLeft,
                GamePlane.PlanePointToWorld(new Vector2(-0.5f, 0f))));
            yield return null;

            Assert.AreEqual(1, widget.PipCount);
            Assert.AreEqual(0f, widget.PipAngleDeg(0), 0.5f,
                "a shot travelling -X came from +X even when its hit point is already past the center");
        }

        private HitDirectionUI NewWidget(out RaisableDamageEvents events)
        {
            canvasRoot = new GameObject("HudCanvas", typeof(Canvas));
            var go = new GameObject("HitDirection", typeof(RectTransform), typeof(CanvasRenderer));
            go.transform.SetParent(canvasRoot.transform, false);
            var widget = go.AddComponent<HitDirectionUI>();
            events = new RaisableDamageEvents();
            widget.Initialize(new OriginStatus(), events);
            return widget;
        }

        private static DamageInfo HitAt(Vector2 planePoint) =>
            new(5f, DamageKind.Laser, ShipId.Invalid, 1f, Vector3.zero, GamePlane.PlanePointToWorld(planePoint));
    }
}
