using System.Collections;
using System.Collections.Generic;
using Cameras;
using Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class CameraFollowPlayMode
{
    private ObserverCam cam;
    private HashSet<GameObject> secondarySubjects;
    private GameObject subject;

    [SetUp]
    public void SetUp()
    {
        cam = new GameObject("Camera").AddComponent<ObserverCam>();
        cam.transform.position = new Vector3(0, 0, 0);
        
        subject = new GameObject("Subject") {
            transform = {
                position = new Vector3(0, 0, 0)}};
        
        secondarySubjects = new HashSet<GameObject>();
        for(var i = 0; i < 3; i++){
            var secondarySubject = new GameObject("SecondarySubject") {
                transform = {
                    position = GamePlane.Forward * i + GamePlane.Right * i}};
            secondarySubjects.Add(secondarySubject.gameObject);
        }
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(cam.gameObject);
        Object.DestroyImmediate(subject.gameObject);
        foreach (var secondarySubject in secondarySubjects) 
            Object.DestroyImmediate(secondarySubject.gameObject);
    }

    [UnityTest]
    public IEnumerator Single_Subject_LockCameraToSubject_FollowsSubject()
    {
        cam.SetSubject(subject.transform);
        cam.SetLockCameraToSubject(true);

        subject.transform.position = GamePlane.Forward * 2;
        yield return new WaitForSeconds(1);
        
        var camPose2D = GamePlane.WorldPointToPlane(cam.transform.position);
        var subjectPose2D = GamePlane.WorldPointToPlane(subject.transform.position);

        Assert.That(Vector2.Distance(camPose2D, subjectPose2D), Is.LessThan(0.1f));
    }
    
    [UnityTest]
    public IEnumerator Subject_Goes_Inactive_Camera_StillFollows()
    {
        cam.SetSubject(subject.transform);
        cam.SetLockCameraToSubject(true);

        subject.SetActive(false);
        subject.transform.position = GamePlane.Forward * 10;

        yield return new WaitForSeconds(1);

        var camPose2D = GamePlane.WorldPointToPlane(cam.transform.position);
        var subjectPose2D = GamePlane.WorldPointToPlane(subject.transform.position);

        Assert.That(Vector2.Distance(camPose2D, subjectPose2D), Is.LessThan(0.1f), "Camera should still follow the subject if it is inactive");
    }
}