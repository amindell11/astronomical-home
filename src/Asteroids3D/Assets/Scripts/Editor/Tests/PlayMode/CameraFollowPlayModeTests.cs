using System.Collections;
using System.Collections.Generic;
using Cameras;
using Game;
using NUnit.Framework;
using Tests.PlayMode.Common;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.PlayMode
{

[Category("Integration")]
public class CameraFollowPlayModeTests : PlayModeWorldFixture
{
    private ObserverCam cam;
    private HashSet<GameObject> secondarySubjects;
    private GameObject subject;

    // Deterministic timeout — camera follow should settle well within this.
    private const float FollowTimeoutSec = 3f;
    private const float FollowThreshold  = 0.1f;

    [SetUp]
    public override void SetUp()
    {
        base.SetUp();
        
        cam = new GameObject("Camera").AddComponent<ObserverCam>();
        cam.transform.position = Vector3.zero;
        
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
        DestroyTestObject(cam);
        DestroyTestObject(subject);
        foreach (var s in secondarySubjects) 
            DestroyTestObject(s);
        base.TearDown();
    }

    [UnityTest]
    [Category("Smoke")]
    public IEnumerator Single_Subject_LockCameraToSubject_FollowsSubject()
    {
        cam.SetSubject(subject.transform);
        cam.SetLockCameraToSubject(true);

        subject.transform.position = GamePlane.Forward * 2;

        // Poll until camera catches up (exit early), fall through on timeout.
        var deadline = Time.realtimeSinceStartup + FollowTimeoutSec;
        while (Time.realtimeSinceStartup < deadline)
        {
            var camPos2D     = GamePlane.WorldPointToPlane(cam.transform.position);
            var subjectPos2D = GamePlane.WorldPointToPlane(subject.transform.position);
            if (Vector2.Distance(camPos2D, subjectPos2D) < FollowThreshold) break;
            yield return null;
        }
        
        var finalCamPos2D     = GamePlane.WorldPointToPlane(cam.transform.position);
        var finalSubjectPos2D = GamePlane.WorldPointToPlane(subject.transform.position);
        Assert.That(Vector2.Distance(finalCamPos2D, finalSubjectPos2D), Is.LessThan(FollowThreshold));
    }
    
    [UnityTest]
    [Category("Slow")]
    public IEnumerator Subject_Goes_Inactive_Camera_DoesNotFollow()
    {
        cam.SetSubject(subject.transform);
        cam.SetLockCameraToSubject(true);

        // Capture camera's initial position
        var initialCamPos2D = GamePlane.WorldPointToPlane(cam.transform.position);

        subject.SetActive(false);
        subject.transform.position = GamePlane.Forward * 10;

        // Wait a bit to ensure camera would have moved if it was going to
        var deadline = Time.realtimeSinceStartup + FollowTimeoutSec;
        while (Time.realtimeSinceStartup < deadline)
        {
            yield return null;
        }

        var finalCamPos2D     = GamePlane.WorldPointToPlane(cam.transform.position);
        var finalSubjectPos2D = GamePlane.WorldPointToPlane(subject.transform.position);
        Assert.That(Vector2.Distance(finalCamPos2D, finalSubjectPos2D), Is.GreaterThan(FollowThreshold),
            "Camera should not follow the subject when it is inactive");
        Assert.That(Vector2.Distance(finalCamPos2D, initialCamPos2D), Is.LessThan(FollowThreshold),
            "Camera should remain at its initial position when subject is inactive");
    }
}

} // namespace Tests.PlayMode
