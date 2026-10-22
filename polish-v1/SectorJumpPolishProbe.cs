#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using Cameras;
using Capture;
using Game;
using NUnit.Framework;
using Ships;
using Ships.Visuals;
using Substrate;
using Substrate.Services.Units;
using Tests.PlayMode.Common;
using UI.Hangar;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public sealed class SectorJumpPolishProbe : CaptureScenario
{
    private ParticleSystem[] exhaust;
    private float[] emissionRates;
    public override GizmoCaptureProfile Profile => GizmoCaptureProfile.None;
    public override CaptureConfig Config => new()
    {
        clipName = "sector-jump-polish-v1",
        width = 960,
        height = 540,
        everyFixedSteps = 1,
        configureView = (camera, light) => { camera.enabled = false; light.enabled = false; }
    };

    public override IEnumerator Run()
    {
        var boot = SceneManager.GetActiveScene();
        yield return SceneManager.LoadSceneAsync("InitScene", LoadSceneMode.Additive);
        GameHost host = null;
        try
        {
            HangarScreen hangar = null;
            var deadline = Time.realtimeSinceStartup + 60f;
            while (!hangar && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                hangar = UnityEngine.Object.FindFirstObjectByType<HangarScreen>();
            }
            Assert.IsTrue(hangar, "InitScene reached the hangar");
            host = UnityEngine.Object.FindFirstObjectByType<GameHost>();
            hangar.launchButton.onClick.Invoke();
            while (!host.ActiveSector && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(host.ActiveSector, "The first sector loaded");
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            var rig = host.GetComponentInChildren<PlayerRig>();
            var observer = host.GetComponentInChildren<ObserverCam>();
            var cameraInput = observer.GetComponent<ObserverCamInputHandler>();
            var ship = rig.Player;
            var body = ship.Rigidbody;
            var start = ship.transform.position;
            var rotation = ship.transform.rotation;
            var velocity = body.linearVelocity;
            var angularVelocity = body.angularVelocity;
            var interpolation = body.interpolation;
            var kinematic = body.isKinematic;
            var cameraPosition = observer.transform.position;
            var cameraSize = observer.Cam.orthographicSize;
            var commanderEnabled = ship.Commander.enabled;
            var inputEnabled = cameraInput.enabled;
            var observerEnabled = observer.enabled;
            var hudVisible = rig.Overlay.IsVisible;
            var clock = Time.timeScale;
            var hull = ship.HealthPct;
            var targetRotation = GamePlane.Rotation * Quaternion.AngleAxis(-135f, Vector3.forward);
            var displacement = GamePlane.PlaneDirToWorld(new Vector2(20000f, -20000f));
            var canvases = rig.Overlay.GetComponentsInChildren<Canvas>(true)
                .Concat(ship.VisualRig.GetComponentsInChildren<Canvas>(true))
                .Where(canvas => !canvas.transform.parent || !canvas.transform.parent.GetComponentInParent<Canvas>())
                .ToArray();
            var ownedGroups = canvases.Select(canvas => !canvas.GetComponent<CanvasGroup>()).ToArray();
            var groups = canvases.Select((canvas, index) => ownedGroups[index]
                ? canvas.gameObject.AddComponent<CanvasGroup>() : canvas.GetComponent<CanvasGroup>()).ToArray();
            var alphas = groups.Select(group => group.alpha).ToArray();
            var bars = ship.VisualRig.GetComponentsInChildren<UI.PlayerState.StatusBarUI>(true);
            Assert.IsNotEmpty(bars, "The capture includes the ship status bars");
            Assert.IsTrue(bars.All(bar => groups.Contains(bar.GetComponentInParent<CanvasGroup>())),
                "Ship status bars share the HUD fade");
            var thrusters = ship.VisualRig.GetComponentsInChildren<ThrusterVisuals>(true);
            var thrusterEnabled = thrusters.Select(thruster => thruster.enabled).ToArray();
            exhaust = thrusters.SelectMany(thruster => thruster.thrustParticles).Distinct().ToArray();
            Assert.IsNotEmpty(exhaust, "The ship supplies authored exhaust particles");
            emissionRates = exhaust.Select(particle => particle.emission.rateOverTimeMultiplier).ToArray();
            var particleState = exhaust.Select(particle => (particle.main.simulationSpace,
                particle.main.useUnscaledTime, particle.main.startLifetime, particle.main.startSpeedMultiplier,
                particle.main.startSizeMultiplier, particle.main.simulationSpeed, particle.emission.enabled,
                particle.useAutoRandomSeed, particle.randomSeed)).ToArray();
            var debugUi = Resources.FindObjectsOfTypeAll<MonoBehaviour>()
                .Where(component => component.GetType().Name == "FMODRuntimeManagerOnGUIHelper").ToArray();
            Assert.IsNotEmpty(debugUi, "The capture finds FMOD's hidden debug drawer");
            var debugEnabled = debugUi.Select(component => component.enabled).ToArray();
            var sourceVolume = UnityEngine.Object.FindObjectsByType<Volume>(FindObjectsSortMode.None)
                .First(volume => volume.sharedProfile && volume.sharedProfile.TryGet<MotionBlur>(out _));
            Assert.IsTrue(sourceVolume.sharedProfile.TryGet<ColorAdjustments>(out var sourceColor));
            Assert.IsTrue(sourceVolume.sharedProfile.TryGet<Vignette>(out var sourceVignette));
            var tuning = new GameObject("Jump polish volume").AddComponent<Volume>();
            tuning.gameObject.layer = sourceVolume.gameObject.layer;
            tuning.isGlobal = true;
            tuning.priority = sourceVolume.priority + 100f;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            tuning.sharedProfile = profile;
            profile.Add<MotionBlur>().quality.Override(MotionBlurQuality.High);
            var color = profile.Add<ColorAdjustments>();
            var vignette = profile.Add<Vignette>();
            color.postExposure.Override(sourceColor.postExposure.value);
            vignette.intensity.Override(sourceVignette.intensity.value);

            Film(ship);
            ship.Commander.enabled = false;
            cameraInput.enabled = false;
            observer.enabled = false;
            Time.timeScale = 0f;
            body.interpolation = RigidbodyInterpolation.None;
            body.isKinematic = true;
            observer.Cam.orthographicSize = 12f;
            observer.transform.position = start - GamePlane.Normal * 10f;
            try
            {
                foreach (var component in debugUi) component.enabled = false;
                foreach (var thruster in thrusters) thruster.enabled = false;
                for (var index = 0; index < exhaust.Length; index++)
                {
                    var particle = exhaust[index];
                    particle.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                    particle.useAutoRandomSeed = false;
                    particle.randomSeed = (uint)(117 + index);
                    var main = particle.main;
                    main.simulationSpace = ParticleSystemSimulationSpace.Local;
                    main.useUnscaledTime = false;
                    main.simulationSpeed = 1f;
                    main.startLifetime = 0.22f;
                    main.startSpeedMultiplier *= 1.6f;
                    main.startSizeMultiplier *= 1.15f;
                    var emission = particle.emission;
                    emission.enabled = true;
                    particle.Play(false);
                }
                yield return Frames(20);
                for (var frame = 1; frame <= 34; frame++)
                {
                    var time = frame * 0.02f;
                    Fade(groups, alphas, 1f - Ease(time / 0.3f));
                    Place(ship, start, Quaternion.Slerp(rotation, targetRotation, Ease(time / 0.52f)));
                    observer.Cam.orthographicSize = Mathf.Lerp(12f, 10.8f, Ease(time / 0.68f));
                    Tick(0.45f * Ease((time - 0.32f) / 0.36f));
                    yield return null;
                }
                Assert.IsTrue(groups.All(group => group.alpha == 0f), "HUD and ship bars have faded out");
                rig.Overlay.SetVisible(false);
                Assert.Greater(Vector3.Dot(targetRotation * Vector3.up, displacement.normalized), 0.999f);
                for (var frame = 1; frame <= 85; frame++)
                {
                    var time = frame * 0.02f;
                    var speed = Ease(time / 0.3f) * (1f - Ease((time - 1.05f) / 0.65f));
                    var position = start + displacement * Travel(time);
                    Place(ship, position, targetRotation);
                    observer.transform.position = position - GamePlane.Normal * 10f;
                    observer.Cam.orthographicSize = time <= 1.05f
                        ? Mathf.Lerp(10.8f, 14.4f, Ease(time / 0.3f))
                        : Mathf.Lerp(14.4f, 11.6f, Ease((time - 1.05f) / 0.65f));
                    color.postExposure.value = sourceColor.postExposure.value + 0.14f * speed;
                    vignette.intensity.value = sourceVignette.intensity.value + 0.07f * speed;
                    var screen = observer.Cam.WorldToViewportPoint(ship.transform.position);
                    Assert.AreEqual(0.5f, screen.x, 0.001f, "Ship remains horizontally centered");
                    Assert.AreEqual(0.5f, screen.y, 0.001f, "Ship remains vertically centered");
                    Assert.AreEqual(hull, ship.HealthPct, "Travel does not change hull");
                    Assert.IsTrue(groups.All(group => group.alpha == 0f), "The HUD stays hidden during travel");
                    Tick(0.45f * (1f - Ease(time / 0.2f)) + 1.6f * speed);
                    if (frame >= 30 && frame <= 60)
                        Assert.IsTrue(exhaust.All(particle => particle.particleCount > 0),
                            "Each nozzle sustains exhaust during cruise");
                    yield return null;
                }
                rig.Overlay.SetVisible(hudVisible);
                for (var frame = 1; frame <= 24; frame++)
                {
                    var time = frame * 0.02f;
                    observer.Cam.orthographicSize = Mathf.Lerp(11.6f, 12f, Ease(time / 0.48f));
                    Fade(groups, alphas, Ease((time - 0.12f) / 0.36f));
                    Tick(0f);
                    yield return null;
                }
                yield return Frames(35);
                Assert.Less(Quaternion.Angle(targetRotation, body.rotation), 0.01f);
                Assert.AreEqual(hull, ship.HealthPct);
                Assert.Less(Vector3.Distance(start + displacement, ship.transform.position), 0.01f);
                Capture.End();
            }
            finally
            {
                for (var index = 0; index < exhaust.Length; index++)
                {
                    var particle = exhaust[index];
                    particle.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                    var main = particle.main;
                    main.simulationSpace = particleState[index].simulationSpace;
                    main.useUnscaledTime = particleState[index].useUnscaledTime;
                    main.startLifetime = particleState[index].startLifetime;
                    main.startSpeedMultiplier = particleState[index].startSpeedMultiplier;
                    main.startSizeMultiplier = particleState[index].startSizeMultiplier;
                    main.simulationSpeed = particleState[index].simulationSpeed;
                    var emission = particle.emission;
                    emission.rateOverTimeMultiplier = emissionRates[index];
                    emission.enabled = particleState[index].enabled;
                    particle.randomSeed = particleState[index].randomSeed;
                    particle.useAutoRandomSeed = particleState[index].useAutoRandomSeed;
                }
                for (var index = 0; index < thrusters.Length; index++)
                    thrusters[index].enabled = thrusterEnabled[index];
                for (var index = 0; index < debugUi.Length; index++)
                    if (debugUi[index]) debugUi[index].enabled = debugEnabled[index];
                UnityEngine.Object.DestroyImmediate(tuning.gameObject);
                foreach (var component in profile.components) UnityEngine.Object.DestroyImmediate(component);
                UnityEngine.Object.DestroyImmediate(profile);
                Fade(groups, alphas, 1f);
                for (var index = 0; index < groups.Length; index++)
                    if (ownedGroups[index]) UnityEngine.Object.DestroyImmediate(groups[index]);
                Place(ship, start, rotation);
                body.isKinematic = kinematic;
                body.interpolation = interpolation;
                body.linearVelocity = velocity;
                body.angularVelocity = angularVelocity;
                observer.transform.position = cameraPosition;
                observer.Cam.orthographicSize = cameraSize;
                observer.enabled = observerEnabled;
                cameraInput.enabled = inputEnabled;
                ship.Commander.enabled = commanderEnabled;
                rig.Overlay.SetVisible(hudVisible);
                Time.timeScale = clock;
            }
        }
        finally
        {
            if (host)
            {
                host.GetComponent<UnitService>().Clear();
                UnityEngine.Object.DestroyImmediate(host.gameObject);
            }
            SceneManager.SetActiveScene(boot);
        }
        foreach (var name in new[] { "InitScene", "Locale_1", "Locale_2", "Locale_3" })
            if (SceneManager.GetSceneByName(name).isLoaded)
                yield return SceneManager.UnloadSceneAsync(name);
    }

    private IEnumerator Frames(int count)
    {
        for (var frame = 0; frame < count; frame++)
        {
            Tick(0f);
            yield return null;
        }
    }

    private void Tick(float thrust)
    {
        for (var index = 0; index < exhaust.Length; index++)
        {
            var emission = exhaust[index].emission;
            emission.rateOverTimeMultiplier = emissionRates[index] * 24f * thrust;
            exhaust[index].Simulate(0.02f, false, false, false);
        }
        FilmStep();
    }

    private static float Ease(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * t * (t * (t * 6f - 15f) + 10f);
    }

    private static float Integral(float t) => t * t * t * t * (t * t - 3f * t + 2.5f);

    private static float Travel(float time)
    {
        if (time < 0.3f) return 0.3f * Integral(time / 0.3f) / 1.225f;
        if (time < 1.05f) return (0.15f + time - 0.3f) / 1.225f;
        var t = Mathf.Clamp01((time - 1.05f) / 0.65f);
        return (0.9f + 0.65f * (t - Integral(t))) / 1.225f;
    }

    private static void Place(Ship ship, Vector3 position, Quaternion rotation)
    {
        ship.transform.SetPositionAndRotation(position, rotation);
        ship.Rigidbody.position = position;
        ship.Rigidbody.rotation = rotation;
    }

    private static void Fade(CanvasGroup[] groups, float[] alphas, float alpha)
    {
        for (var index = 0; index < groups.Length; index++)
            groups[index].alpha = alphas[index] * alpha;
    }
}
#endif
