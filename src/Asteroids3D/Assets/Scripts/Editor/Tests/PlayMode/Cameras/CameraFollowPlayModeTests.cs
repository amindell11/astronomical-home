using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Cameras;
using Cameras.Shake;
using Combat.Targeting;
using Damage;
using Movement;
using NUnit.Framework;
using Ships.Command;
using Ships.Damage;
using Ships.Registry;
using Tests.PlayMode.Common;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Substrate;

namespace Tests.PlayMode.Cameras
{

[Category("Camera")]
public class CameraFollowPlayModeTests : PlayModeWorldFixture
{
    private ObserverCam cam;
    private FlightCameraProfile feel;
    private CameraShakeProfile shakeProfile;
    private HashSet<GameObject> secondarySubjects;
    private GameObject subject;
    private SimulationMode simulationMode;
    private bool previousManual;

    // Deterministic timeout — camera follow should settle well within this.
    private const float FollowTimeoutSec = 3f;
    private const float FollowThreshold  = 0.1f;
    private const int NegativeProofFrames = 30;
    private const float SettleSec = 1f;
    private const float SizeTolerance = 0.05f;
    private const float LeadSpeed = 10f;
    private const float JumpFlightSec = 0.5f;
    private const float WhipWindowSec = 0.1f;
    private const string FlightCameraPath = "Assets/Settings/Cameras/FlightCamera.asset";

    [SetUp]
    public override void SetUp()
    {
        base.SetUp();
        previousManual = CameraSettings.ManualCamera;
        CameraSettings.ManualCamera = false;
        simulationMode = Physics.simulationMode;
        
        cam = TestAssets.NewObserverCam();
        // The cursor comes from SetCursor alone; the editor's live mouse would move the lead.
        cam.GetComponent<ObserverCamInputHandler>().enabled = false;
        cam.transform.position = Vector3.zero;
        feel = TestAssets.Load<FlightCameraProfile>(FlightCameraPath);

        subject = new GameObject("Subject");
        subject.transform.position = Vector3.zero;
        
        secondarySubjects = new HashSet<GameObject>();
        for (var i = 0; i < 3; i++)
        {
            var secondarySubject = new GameObject("SecondarySubject");
            secondarySubject.transform.position = GamePlane.Forward * i + GamePlane.Right * i;
            secondarySubjects.Add(secondarySubject);
        }
    }

    [TearDown]
    public override void TearDown()
    {
        if (shakeProfile)
            UnityEngine.Object.DestroyImmediate(shakeProfile);
        DestroyTestObject(cam);
        DestroyTestObject(subject);
        foreach (var s in secondarySubjects) 
            DestroyTestObject(s);
        Physics.simulationMode = simulationMode;
        CameraSettings.ManualCamera = previousManual;
        base.TearDown();
    }

    [UnityTest]
    [Category("Smoke")]
    public IEnumerator Single_Subject_LockCameraToSubject_FollowsSubject()
    {
        cam.SetSubject(subject.transform);
        cam.SetLockCameraToSubject(true);

        subject.transform.position = GamePlane.Forward * 2;

        yield return AsyncAssert.WaitUntilThen(
            () =>
            {
                var camPos2D = GamePlane.WorldPointToPlane(cam.transform.position);
                var subjectPos2D = GamePlane.WorldPointToPlane(subject.transform.position);
                return Vector2.Distance(camPos2D, subjectPos2D) < FollowThreshold;
            },
            FollowTimeoutSec,
            () =>
            {
                var finalCamPos2D = GamePlane.WorldPointToPlane(cam.transform.position);
                var finalSubjectPos2D = GamePlane.WorldPointToPlane(subject.transform.position);
                Assert.That(Vector2.Distance(finalCamPos2D, finalSubjectPos2D), Is.LessThan(FollowThreshold));
            });
    }
    
    [UnityTest]
    public IEnumerator Subject_Goes_Inactive_Camera_DoesNotFollow()
    {
        cam.SetSubject(subject.transform);
        cam.SetLockCameraToSubject(true);

        var initialCamPos2D = GamePlane.WorldPointToPlane(cam.transform.position);

        subject.SetActive(false);
        subject.transform.position = GamePlane.Forward * 10;

        // Follow is SmoothDamp over unscaled time (smoothTime 0.15): 0.35s ≈ 90% closure if regressed.
        yield return AsyncAssert.AssertRemainsFalseFor(
            () =>
            {
                var camPos2D = GamePlane.WorldPointToPlane(cam.transform.position);
                return Vector2.Distance(camPos2D, initialCamPos2D) > FollowThreshold;
            },
            "Camera moved when subject was inactive",
            minFrames: NegativeProofFrames,
            minUnscaledSeconds: 0.35f);

        var finalCamPos2D     = GamePlane.WorldPointToPlane(cam.transform.position);
        var finalSubjectPos2D = GamePlane.WorldPointToPlane(subject.transform.position);
        Assert.That(Vector2.Distance(finalCamPos2D, finalSubjectPos2D), Is.GreaterThan(FollowThreshold),
            "Camera should not follow the subject when it is inactive");
        Assert.That(Vector2.Distance(finalCamPos2D, initialCamPos2D), Is.LessThan(FollowThreshold),
            "Camera should remain at its initial position when subject is inactive");
    }

    [UnityTest]
    public IEnumerator Locked_CentersAheadAlongTheVelocity_AndBackOnTheSubjectWhenItStops()
    {
        var body = GiveSubjectVelocity(GamePlane.Right * LeadSpeed);
        cam.SetSubject(subject.transform, body);

        var subjectCenter = GamePlane.WorldPointToPlane(subject.transform.position);
        var leadDistance = Mathf.Min(LeadSpeed * feel.leadSeconds, feel.maxLead);
        yield return CameraSettlesOn(subjectCenter + GamePlane.WorldDirToPlane(GamePlane.Right) * leadDistance);

        body.linearVelocity = Vector3.zero;
        yield return CameraSettlesOn(subjectCenter);
    }

    [UnityTest]
    public IEnumerator ResumeFollow_EasesTheLeadInInsteadOfWhippingToIt()
    {
        cam.SetSubject(subject.transform);
        cam.SetLockCameraToSubject(true);
        cam.SetCursor(cam.Cam.ViewportToScreenPoint(new Vector3(1f, .5f)));
        var subjectCenter = GamePlane.WorldPointToPlane(subject.transform.position);

        var flightEnds = Time.realtimeSinceStartup + JumpFlightSec;
        while (Time.realtimeSinceStartup < flightEnds)
        {
            yield return null;
            cam.transform.position -= GamePlane.PlaneDirToWorld(GamePlane.WorldPointToPlane(cam.transform.position) - subjectCenter);
        }
        cam.ResumeFollow();
        yield return new WaitForSecondsRealtime(WhipWindowSec);

        var offset = Vector2.Distance(GamePlane.WorldPointToPlane(cam.transform.position), subjectCenter);
        Assert.That(offset, Is.LessThan(.25f * feel.cursorLead),
            "the camera whipped toward the lead it built while the jump flight held it");
        yield return CameraSettlesOn(subjectCenter + GamePlane.WorldDirToPlane(GamePlane.Right) * feel.cursorLead);
    }

    [UnityTest]
    public IEnumerator LockOff_ZoomsToFit_NotToTheProfile()
    {
        cam.SetSubject(subject.transform);
        cam.SetLockCameraToSubject(false);
        cam.SetLockZoomToSubject(false);
        yield return new WaitForSecondsRealtime(SettleSec);
        var autoFit = cam.Cam.orthographicSize;

        cam.SetLockZoomToSubject(true);
        yield return AsyncAssert.AssertRemainsFalseFor(
            () => Mathf.Abs(cam.Cam.orthographicSize - autoFit) > SizeTolerance,
            "with the camera lock off, the shipped zoom lock moved the framing off the auto-fit",
            minFrames: NegativeProofFrames,
            minUnscaledSeconds: 0.35f);
    }

    [UnityTest]
    public IEnumerator Locked_ZoomWidensWithSpeed()
    {
        Assume.That(feel.zoomPerSpeed, Is.GreaterThan(0f), "this tuning turns the speed zoom off");
        var speed = feel.maxWiden / feel.zoomPerSpeed / 2f;
        cam.SetSubject(subject.transform, GiveSubjectVelocity(GamePlane.Right * speed));

        yield return ZoomSettlesOn(feel.zoom + feel.zoomPerSpeed * speed);
    }

    [UnityTest]
    public IEnumerator Locked_AFasterHullGetsAWiderBaseZoomAtRest()
    {
        Assume.That(feel.zoomPerHullSpeed, Is.GreaterThan(0f), "this tuning turns the hull-speed zoom off");
        cam.SetSubject(subject.transform, newHull: new HullStub(feel.referenceHullSpeed + 10f));

        var expected = Mathf.Min(feel.zoom + feel.zoomPerHullSpeed * 10f, feel.maxZoomOut);
        Assume.That(expected, Is.GreaterThan(feel.zoom + SizeTolerance));
        yield return ZoomSettlesOn(expected);

        cam.SetSubject(subject.transform, newHull: new HullStub(feel.referenceHullSpeed));
        yield return ZoomSettlesOn(feel.zoom);
    }

    [UnityTest]
    public IEnumerator Locked_ZoomWidensToFitANearbyEnemy()
    {
        cam.SetSubject(subject.transform);
        var enemy = AddEngagedEnemyNeeding(feel.zoom + (FightCap - feel.zoom) / 2f);

        yield return ZoomSettlesOn(Mathf.Clamp(FitWith(enemy), feel.zoom, FightCap));
    }

    [UnityTest]
    public IEnumerator Locked_ZoomIgnoresAFarEnemy_AndHoldsAnEngagedOneUntilPastTheMargin()
    {
        cam.SetSubject(subject.transform);
        var band = feel.fightRadius + feel.releaseMargin / 2f;
        var enemy = AddEnemy(OnForward(2f * (feel.fightRadius + feel.releaseMargin)));
        yield return ZoomSettlesOn(feel.zoom);
        yield return ZoomHolds(feel.zoom, "an enemy beyond the release radius widened the zoom");

        enemy.position = OnForward(band);
        yield return ZoomHolds(feel.zoom, "an enemy that never came inside fightRadius widened the zoom");

        enemy.position = OnForward(feel.fightRadius / 2f);
        yield return ZoomSettlesOn(Mathf.Clamp(FitWith(enemy), feel.zoom, FightCap));

        enemy.position = OnForward(band);
        var held = Mathf.Clamp(FitWith(enemy), feel.zoom, FightCap);
        yield return ZoomSettlesOn(held);
        yield return ZoomHolds(held, "an engaged enemy was released inside the release margin");
    }

    [UnityTest]
    public IEnumerator Auto_ZoomNeverExceedsTheMax()
    {
        Assume.That(feel.zoomPerSpeed, Is.GreaterThan(0f), "this tuning turns the speed zoom off");
        var distance = 0.9f * feel.fightRadius;
        cam.SetSubject(subject.transform, GiveSubjectVelocity(GamePlane.Right * (FightCap / feel.zoomPerSpeed)));
        AddEnemy(OnForward(distance));
        AddEnemy(OnForward(-distance));
        Assume.That(2f * distance + 2f * Padding, Is.GreaterThan(2f * FightCap), "this tuning fits both enemies");

        var until = Time.realtimeSinceStartup + FollowTimeoutSec;
        while (Time.realtimeSinceStartup < until)
        {
            Assert.That(cam.Cam.orthographicSize, Is.LessThanOrEqualTo(FightCap + SizeTolerance),
                "the zoom passed the max zoom-out");
            yield return null;
        }

        yield return ZoomSettlesOn(FightCap);
    }

    [UnityTest]
    public IEnumerator Locked_PullsTowardAnEnemyBehindTheLead_SoItStaysInView()
    {
        cam.SetSubject(subject.transform, GiveSubjectVelocity(GamePlane.Forward * LeadSpeed));
        var behind = 0.9f * feel.fightRadius;
        var enemy = AddEnemy(OnForward(-behind));

        var lead = Mathf.Min(LeadSpeed * feel.leadSeconds, feel.maxLead);
        Assume.That(lead + behind, Is.GreaterThan(FightCap - Padding), "this tuning frames the enemy without a pull");
        Assume.That(behind + 2f * Padding, Is.LessThanOrEqualTo(2f * FightCap), "this tuning cannot fit the enemy at all");

        var subjectCenter = GamePlane.WorldPointToPlane(subject.transform.position);
        var enemyCenter = GamePlane.WorldPointToPlane(enemy.position);
        var leadCenter = subjectCenter + GamePlane.WorldDirToPlane(GamePlane.Forward) * lead;
        var min = Vector2.Min(subjectCenter, enemyCenter);
        var max = Vector2.Max(subjectCenter, enemyCenter);
        var focus = FightFocus(leadCenter, min, max);
        var needed = CameraUtils.ZoomToFitBounds(focus, min, max, Padding, cam.Cam.aspect);
        var speedZoom = feel.zoom + feel.zoomPerSpeed * LeadSpeed;
        yield return ZoomSettlesOn(Mathf.Min(Mathf.Max(
            Mathf.Clamp(speedZoom, feel.zoom, feel.zoom + feel.maxWiden), needed), FightCap));
        yield return CameraSettlesOn(focus);

        var offset = enemyCenter - GamePlane.WorldPointToPlane(cam.transform.position);
        // The zoom only approaches its target, so the view gets the settle tolerance too.
        var reach = new Vector2(cam.Cam.aspect, 1f) * (cam.Cam.orthographicSize + SizeTolerance) - Vector2.one * Padding;
        Assert.That(Mathf.Abs(offset.x), Is.LessThanOrEqualTo(reach.x + FollowThreshold), "the enemy is off the side of the view");
        Assert.That(Mathf.Abs(offset.y), Is.LessThanOrEqualTo(reach.y + FollowThreshold), "the enemy is off the end of the view");
    }

    [UnityTest]
    public IEnumerator Auto_ShiftsTowardAnEnemyOffToOneSide_AndKeepsBothInView()
    {
        Assume.That(feel.fightCenterBias, Is.GreaterThan(0f));
        cam.SetSubject(subject.transform);
        var enemy = AddEnemy(subject.transform.position + GamePlane.Right * (feel.fightRadius / 2f));

        yield return ZoomSettlesOn(Mathf.Clamp(FitWith(enemy), feel.zoom, FightCap));

        var subjectCenter = GamePlane.WorldPointToPlane(subject.transform.position);
        var enemyCenter = GamePlane.WorldPointToPlane(enemy.position);
        var min = Vector2.Min(subjectCenter, enemyCenter);
        var max = Vector2.Max(subjectCenter, enemyCenter);
        yield return CameraSettlesOn(FightFocus(subjectCenter, min, max));

        var center = GamePlane.WorldPointToPlane(cam.transform.position);
        Assert.That(Vector2.Dot(center - subjectCenter, GamePlane.WorldDirToPlane(GamePlane.Right)),
            Is.GreaterThan(FollowThreshold), "the camera's center did not shift toward the enemy");
        foreach (var point in new[] { subjectCenter, enemyCenter })
        {
            var offset = point - center;
            Assert.That(Mathf.Abs(offset.x), Is.LessThanOrEqualTo(
                cam.Cam.aspect * (cam.Cam.orthographicSize + SizeTolerance) - Padding + FollowThreshold));
            Assert.That(Mathf.Abs(offset.y), Is.LessThanOrEqualTo(
                cam.Cam.orthographicSize + SizeTolerance - Padding + FollowThreshold));
        }
    }

    [UnityTest]
    public IEnumerator Auto_FramesOnlyTheNearestFightCountEnemies()
    {
        cam.SetSubject(subject.transform);
        Transform farthest = null;
        for (var i = 0; i < feel.fightCount + 1; i++)
            farthest = AddEnemy(OnForward(feel.fightRadius * (i + 1) / (feel.fightCount + 2)));

        yield return new WaitForSecondsRealtime(SettleSec);

        Assert.That(cam.Engaged.Count, Is.EqualTo(feel.fightCount));
        // NUnit compares Transforms as child collections, so membership goes through the set.
        Assert.IsFalse(cam.Engaged.Contains(farthest), "the farthest enemy was framed past fightCount");
    }

    [UnityTest]
    public IEnumerator ManualCamera_IgnoresANearbyEnemy()
    {
        CameraSettings.ManualCamera = true;
        cam.SetSubject(subject.transform);
        AddEnemy(OnForward(feel.fightRadius / 2f));

        yield return ZoomSettlesOn(feel.zoom);
        yield return CameraSettlesOn(GamePlane.WorldPointToPlane(subject.transform.position));
        Assert.That(cam.Engaged, Is.Empty);
    }

    [UnityTest]
    public IEnumerator Hit_ShakesTheView_WithoutMovingTheTransform()
    {
        Assume.That(feel.traumaPerHit, Is.GreaterThan(0f), "this tuning turns hit shake off");
        Assume.That(feel.maxShakeOffset + feel.maxShakeRoll, Is.GreaterThan(0f), "this tuning turns the shake amplitude off");
        var damage = new DamageEventsStub();
        cam.SetSubject(subject.transform, newDamage: damage);
        cam.SetLockCameraToSubject(true);
        var subjectAt = subject.transform.position;
        var quiet = 0;
        var held = cam.transform.position;
        var until = Time.realtimeSinceStartup + FollowTimeoutSec;
        while (Time.realtimeSinceStartup < until && quiet < 2)
        {
            yield return null;
            var position = cam.transform.position;
            quiet = Vector3.Distance(position, held) < 1e-4f ? quiet + 1 : 0;
            held = position;
        }
        Assert.That(quiet, Is.EqualTo(2), "the camera did not settle");
        Assert.That(subject.transform.position, Is.EqualTo(subjectAt));
        damage.Hit(100f);
        yield return null;
        Assert.That(cam.Trauma, Is.GreaterThan(0f));
        Assert.That(ViewDelta(cam.Cam.worldToCameraMatrix, UnshakenView()), Is.GreaterThan(1e-4f));
        // The follow's residual drift is far below the shake's reach.
        Assert.That(Vector3.Distance(cam.transform.position, held), Is.LessThan(.01f), "the shake moved the transform");
        Assert.That(subject.transform.position, Is.EqualTo(subjectAt));
    }

    [UnityTest]
    public IEnumerator Disabling_ClearsAShakeFromTheView()
    {
        var damage = new DamageEventsStub();
        cam.SetSubject(subject.transform, newDamage: damage);
        damage.Hit(100f);
        yield return null;
        cam.enabled = false;
        Assert.That(ViewDelta(cam.Cam.worldToCameraMatrix, UnshakenView()), Is.LessThanOrEqualTo(1e-4f));
        Assert.That(cam.Trauma, Is.EqualTo(0f), "a re-enabled camera would resume a stale shake");
    }

    [UnityTest]
    public IEnumerator Trauma_DrainsToZero_AndTheViewSteadies()
    {
        Assume.That(feel.traumaDecay, Is.GreaterThan(0f), "this tuning turns the shake drain off");
        var damage = new DamageEventsStub();
        cam.SetSubject(subject.transform, newDamage: damage);
        damage.Hit(100f);
        yield return new WaitForSecondsRealtime(1f / feel.traumaDecay + .2f);
        Assert.That(cam.Trauma, Is.EqualTo(0f));
        Assert.That(ViewDelta(cam.Cam.worldToCameraMatrix, UnshakenView()), Is.LessThanOrEqualTo(1e-4f));
    }

    [UnityTest]
    public IEnumerator Death_AddsItsTrauma()
    {
        Assume.That(feel.deathTrauma, Is.GreaterThan(.05f), "this tuning turns death shake off");
        var damage = new DamageEventsStub();
        cam.SetSubject(subject.transform, newDamage: damage);
        damage.Die();
        Assert.That(cam.Trauma, Is.EqualTo(feel.deathTrauma).Within(1e-4f));
        yield return null;
    }

    [UnityTest]
    public IEnumerator Rebinding_IgnoresHitsOnThePreviousSource()
    {
        var first = new DamageEventsStub();
        var second = new DamageEventsStub();
        cam.SetSubject(subject.transform, newDamage: first);
        cam.SetSubject(subject.transform, newDamage: second);
        first.Hit(100f);
        yield return null;
        Assert.That(cam.Trauma, Is.EqualTo(0f));
    }

    [UnityTest]
    public IEnumerator SubjectChannel_KickShakesTheView()
    {
        var channel = Listen();
        channel.Raise(shakeProfile, shakeProfile, Vector2.up);
        yield return null;
        Assert.AreEqual(1, cam.ShakeLayerCount);
        Assert.That(ViewDelta(cam.Cam.worldToCameraMatrix, UnshakenView()), Is.GreaterThan(1e-4f));
    }

    [UnityTest]
    public IEnumerator OtherChannel_LeavesTheViewUnshaken()
    {
        Listen();
        var other = new ShakeChannel();
        other.Raise(shakeProfile, shakeProfile, Vector2.up);
        yield return null;
        Assert.AreEqual(0, cam.ShakeLayerCount);
        Assert.That(ViewDelta(cam.Cam.worldToCameraMatrix, UnshakenView()), Is.LessThanOrEqualTo(1e-4f));
    }

    [UnityTest]
    public IEnumerator Disabling_ClearsShakeLayers()
    {
        var channel = Listen();
        channel.Raise(shakeProfile, shakeProfile, Vector2.up);
        yield return null;
        Assert.AreEqual(1, cam.ShakeLayerCount);
        cam.enabled = false;
        Assert.AreEqual(0, cam.ShakeLayerCount);
    }

    [UnityTest]
    public IEnumerator StoppedClock_KeepsHeldLayers_OffTheView()
    {
        var channel = Listen();
        shakeProfile.sustainAt = .5f;
        channel.Raise(shakeProfile, shakeProfile, Vector2.up);
        var scale = Time.timeScale;
        Time.timeScale = 0f;
        try
        {
            yield return null;
            Assert.AreEqual(1, cam.ShakeLayerCount);
            Assert.That(ViewDelta(cam.Cam.worldToCameraMatrix, UnshakenView()), Is.LessThanOrEqualTo(1e-4f));
        }
        finally
        {
            Time.timeScale = scale;
        }
        yield return null;
        Assert.That(ViewDelta(cam.Cam.worldToCameraMatrix, UnshakenView()), Is.GreaterThan(1e-4f));
    }

    private ShakeChannel Listen()
    {
        shakeProfile = ScriptableObject.CreateInstance<CameraShakeProfile>();
        shakeProfile.envelope = AnimationCurve.Constant(0f, 1f, 1f);
        shakeProfile.jitter = 0f;
        shakeProfile.roll = 0f;
        shakeProfile.kick = .1f;
        var channel = new ShakeChannel();
        cam.SetSubject(subject.transform, newShake: channel);
        return channel;
    }

    private float FightCap => feel.maxZoomOut;
    private float Padding
    {
        get
        {
            using var serialized = new SerializedObject(cam);
            return serialized.FindProperty("padding").floatValue;
        }
    }

    // Script mode keeps the body's velocity without moving it, so follow smoothing adds no lag.
    private Rigidbody GiveSubjectVelocity(Vector3 velocity)
    {
        Physics.simulationMode = SimulationMode.Script;
        var body = subject.AddComponent<Rigidbody>();
        body.useGravity = false;
        body.linearVelocity = velocity;
        return body;
    }

    private Vector3 OnForward(float distance) => subject.transform.position + GamePlane.Forward * distance;

    private Transform AddEnemy(Vector3 position)
    {
        var enemy = new GameObject("Enemy");
        enemy.transform.position = position;
        secondarySubjects.Add(enemy);
        cam.AddSecondarySubject(enemy.transform);
        return enemy.transform;
    }

    private Transform AddEngagedEnemyNeeding(float size)
    {
        var distance = size - Padding;
        Assume.That(distance, Is.LessThan(feel.fightRadius), "this tuning cannot engage an enemy that far out");
        return AddEnemy(OnForward(distance));
    }

    private Vector2 FightFocus(Vector2 center, Vector2 min, Vector2 max)
    {
        var biased = Vector2.Lerp(center, CameraUtils.BoundsCenter(min, max), feel.fightCenterBias);
        var fitted = CameraUtils.ShiftToFitBounds(biased, min, max, FightCap, cam.Cam.aspect, Padding);
        var player = GamePlane.WorldPointToPlane(subject.transform.position);
        return CameraUtils.ShiftToKeepPointInView(fitted, player - fitted, FightCap, cam.Cam.aspect, Padding);
    }

    private float FitWith(Transform enemy)
    {
        var center = GamePlane.WorldPointToPlane(subject.transform.position);
        var at = GamePlane.WorldPointToPlane(enemy.position);
        var min = Vector2.Min(center, at);
        var max = Vector2.Max(center, at);
        return CameraUtils.ZoomToFitBounds(FightFocus(center, min, max), min, max, Padding, cam.Cam.aspect);
    }

    private IEnumerator CameraSettlesOn(Vector2 center)
    {
        float Miss() => Vector2.Distance(GamePlane.WorldPointToPlane(cam.transform.position), center);
        yield return AsyncAssert.WaitUntilThen(() => Miss() < FollowThreshold, FollowTimeoutSec,
            () => Assert.That(Miss(), Is.LessThan(FollowThreshold)));
    }

    private IEnumerator ZoomSettlesOn(float size)
    {
        var expected = size * cam.AppliedZoomScale;
        float Miss() => Mathf.Abs(cam.Cam.orthographicSize - expected);
        yield return AsyncAssert.WaitUntilThen(() => Miss() < SizeTolerance, FollowTimeoutSec + 5f * feel.widenTime,
            () => Assert.AreEqual(expected, cam.Cam.orthographicSize, SizeTolerance));
    }

    private IEnumerator ZoomHolds(float size, string failureMessage) =>
        AsyncAssert.AssertRemainsFalseFor(
            () => Mathf.Abs(cam.Cam.orthographicSize - size * cam.AppliedZoomScale) > SizeTolerance,
            failureMessage,
            minFrames: NegativeProofFrames,
            minUnscaledSeconds: Mathf.Max(0.35f, feel.widenTime));

    private Matrix4x4 UnshakenView() =>
        Matrix4x4.Scale(new Vector3(1f, 1f, -1f)) * cam.transform.worldToLocalMatrix;

    private static float ViewDelta(Matrix4x4 actual, Matrix4x4 expected)
    {
        var delta = 0f;
        for (var row = 0; row < 4; row++)
            for (var column = 0; column < 4; column++)
                delta = Mathf.Max(delta, Mathf.Abs(actual[row, column] - expected[row, column]));
        return delta;
    }

    private sealed class DamageEventsStub : IDamageEvents
    {
        public event Action<AppliedDamage> OnDamaged;
        public event Action<ShipId, DamageInfo> OnDeath;
        public Resource Health { get; } = new(100f);
        public RegenResource Shield { get; } = new(100f, 0f, 999f);

        public void Hit(float hull) => OnDamaged?.Invoke(new AppliedDamage(default, 0f, hull));

        public void Die() => OnDeath?.Invoke(default, default);
    }

    private sealed class HullStub : IShipStatus
    {
        private readonly float maxSpeed;

        public HullStub(float maxSpeed) => this.maxSpeed = maxSpeed;

        public ShipId Id => default;
        public Transform Transform => null;
        public Kinematics Kinematics => default;
        public Dynamics Dynamics => default;
        public float HealthPct => default;
        public float ShieldPct => default;
        public bool BoostAvailable => default;
        public float BoostCooldownRemaining => default;
        public float BoostCooldownPct => default;
        public float MaxSpeed => maxSpeed;
        public float MaxYawRate => default;
        public IReadOnlyList<ITrackingMissile> TrackingMissiles => null;
    }
}

} // namespace Tests.PlayMode.Cameras
