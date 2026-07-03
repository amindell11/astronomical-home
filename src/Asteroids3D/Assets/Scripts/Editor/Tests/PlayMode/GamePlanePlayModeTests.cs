using System.Collections;
using Game;
using NUnit.Framework;
using Tests.PlayMode.Common;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.PlayMode
{

[Category("Core")]
public class GamePlanePlayModeTests : PlayModeWorldFixture
{
    // Override to disable audio pause for these lightweight tests
    protected override bool PauseAudio => false;

    // These tests deliberately run frames with GamePlane unconfigured. Any object leaked by an
    // earlier test that touches GamePlane from Update/FixedUpdate (seen: ProjectileBase via
    // PlaneConstraints) throws during those frames and fails THIS test, so clear them first.
    [SetUp]
    public void DestroyLeakedPlaneConstrainedObjects()
    {
        foreach (var projectile in Object.FindObjectsByType<Combat.Projectile.ProjectileBase>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
            Object.DestroyImmediate(projectile.gameObject);
    }

    [Test]
    public void GamePlane_UnconfiguredAccess_Throws()
    {
        GamePlane.Reset();
        Assert.Throws<System.InvalidOperationException>(() => _ = GamePlane.Normal);
    }

    [UnityTest]
    [Category("Smoke")]
    public IEnumerator GamePlane_Configure_SetsAxesCorrectly()
    {
        // Arrange
        GamePlane.Reset();

        // Act
        GamePlane.Configure(PlaneAxis.Y, new Vector3(10, 0, 5));

        // Assert
        Assert.IsTrue(GamePlane.IsConfigured);
        Assert.AreEqual(Vector3.down, GamePlane.Normal);
        Assert.AreEqual(Vector3.forward, GamePlane.Forward);
        Assert.AreEqual(Vector3.right, GamePlane.Right);
        Assert.AreEqual(new Vector3(10, 0, 5), GamePlane.Origin);

        // Cleanup
        GamePlane.Reset();

        yield return null;
    }

    [UnityTest]
    public IEnumerator GamePlane_WorldToPlane_And_PlaneToWorld_AreConsistent()
    {
        // Arrange
        GamePlane.Reset();
        GamePlane.Configure(PlaneAxis.Y, new Vector3(10, 0, 5));

        Vector3 worldPoint = new Vector3(12, 0, 8);

        // Act
        var planePoint = GamePlane.WorldPointToPlane(worldPoint);
        var worldPointBack = GamePlane.PlanePointToWorld(planePoint);

        // Assert
        Assert.That(worldPointBack.x, Is.EqualTo(worldPoint.x).Within(0.01f));
        Assert.That(worldPointBack.z, Is.EqualTo(worldPoint.z).Within(0.01f));

        // Cleanup
        GamePlane.Reset();

        yield return null;
    }
}

} // namespace Tests.PlayMode
