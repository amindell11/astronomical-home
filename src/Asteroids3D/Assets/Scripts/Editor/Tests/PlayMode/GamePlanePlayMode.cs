using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Game;

public class GamePlanePlayMode
{
    [SetUp]
    public void SetUp()
    {
        // Optional: Setup code before each test runs
    }

    [TearDown]
    public void TearDown()
    {
        // Optional: Cleanup code after each test runs
    }

    [UnityTest]
    public IEnumerator GamePlane_ReferencePlane_CanBeSetAndQueried()
    {
        // Arrange
        var planeGO = new GameObject("TestReferencePlane");
        planeGO.tag = "ReferencePlane";
        var planeTransform = planeGO.transform;

        // Act
        GamePlane.SetReferencePlane(planeTransform);

        // Assert
        Assert.AreEqual(planeTransform, GamePlane.Plane);

        // Cleanup
        Object.DestroyImmediate(planeGO);

        yield return null;
    }

    [UnityTest]
    public IEnumerator GamePlane_WorldToPlane_And_PlaneToWorld_AreConsistent()
    {
        // Arrange
        var planeGO = new GameObject("TestReferencePlane");
        planeGO.tag = "ReferencePlane";
        var planeTransform = planeGO.transform;
        planeTransform.position = new Vector3(10, 0, 5);
        planeTransform.rotation = Quaternion.Euler(90f, 0f, 0f); // Horizontal plane: local XY maps to world XZ

        GamePlane.SetReferencePlane(planeTransform);

        Vector3 worldPoint = new Vector3(12, 0, 8);

        // Act
        Vector2 planePoint = GamePlane.WorldPointToPlane(worldPoint);
        Vector3 worldPointBack = GamePlane.PlanePointToWorld(planePoint);

        // Assert
        Assert.That(worldPointBack.x, Is.EqualTo(worldPoint.x).Within(0.01f));
        Assert.That(worldPointBack.z, Is.EqualTo(worldPoint.z).Within(0.01f));

        // Cleanup
        Object.DestroyImmediate(planeGO);

        yield return null;
    }
}
