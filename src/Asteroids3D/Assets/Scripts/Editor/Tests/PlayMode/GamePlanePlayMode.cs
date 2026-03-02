using System.Collections;
using Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.PlayMode
{

[Category("Regression")]
public class GamePlanePlayModeTests
{
    [UnityTest]
    [Category("Smoke")]
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
        GamePlane.Reset();

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
        var planePoint = GamePlane.WorldPointToPlane(worldPoint);
        var worldPointBack = GamePlane.PlanePointToWorld(planePoint);

        // Assert
        Assert.That(worldPointBack.x, Is.EqualTo(worldPoint.x).Within(0.01f));
        Assert.That(worldPointBack.z, Is.EqualTo(worldPoint.z).Within(0.01f));

        // Cleanup
        Object.DestroyImmediate(planeGO);
        GamePlane.Reset();

        yield return null;
    }
}

} // namespace Tests.PlayMode
