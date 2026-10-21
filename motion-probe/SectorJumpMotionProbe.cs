#if UNITY_EDITOR
using System;
using System.Collections;
using Cameras;
using Capture;
using Game;
using NUnit.Framework;
using Ships;
using Substrate;
using Substrate.Services.Units;
using Tests.PlayMode.Common;
using UI.Hangar;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class SectorJumpMotionProbe : CaptureScenario
{
    public override GizmoCaptureProfile Profile => GizmoCaptureProfile.None;
    public override CaptureConfig Config => new()
    {
        clipName = "sector-jump-real-ship-motion",
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

            Film(ship);
            ship.Commander.enabled = false;
            cameraInput.enabled = false;
            observer.enabled = false;
            rig.Overlay.SetVisible(false);
            Time.timeScale = 0f;
            body.interpolation = RigidbodyInterpolation.None;
            body.isKinematic = true;
            observer.Cam.orthographicSize = 12f;
            observer.transform.position = start - GamePlane.Normal * 10f;
            try
            {
                yield return Frames(30);
                Debug.Log("[Jump probe] turn");
                for (var frame = 1; frame <= 15; frame++)
                {
                    Place(ship, start, Quaternion.Slerp(rotation, targetRotation, Mathf.SmoothStep(0f, 1f, frame / 15f)));
                    FilmStep();
                    yield return null;
                }
                yield return Frames(10);
                Debug.Log("[Jump probe] travel");
                for (var frame = 1; frame <= 75; frame++)
                {
                    var position = start + displacement * Mathf.SmoothStep(0f, 1f, frame / 75f);
                    Place(ship, position, targetRotation);
                    observer.transform.position = position - GamePlane.Normal * 10f;
                    var screen = observer.Cam.WorldToViewportPoint(ship.transform.position);
                    Assert.AreEqual(0.5f, screen.x, 0.001f, "Ship remains horizontally centered");
                    Assert.AreEqual(0.5f, screen.y, 0.001f, "Ship remains vertically centered");
                    Assert.AreEqual(hull, ship.HealthPct, "Travel does not change hull");
                    FilmStep();
                    yield return null;
                }
                yield return Frames(20);
                Debug.Log("[Jump probe] arrival snap");
                Place(ship, start, targetRotation);
                observer.transform.position = start - GamePlane.Normal * 10f;
                yield return Frames(40);
                Assert.Less(Quaternion.Angle(targetRotation, body.rotation), 0.01f);
                Assert.AreEqual(hull, ship.HealthPct);
            }
            finally
            {
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
            FilmStep();
            yield return null;
        }
    }

    private static void Place(Ship ship, Vector3 position, Quaternion rotation)
    {
        ship.transform.SetPositionAndRotation(position, rotation);
        ship.Rigidbody.position = position;
        ship.Rigidbody.rotation = rotation;
    }
}
#endif
