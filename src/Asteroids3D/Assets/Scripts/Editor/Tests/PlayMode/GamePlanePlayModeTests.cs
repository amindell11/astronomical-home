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

    // NOTE: never `yield` while GamePlane is unconfigured. Objects leaked by earlier tests
    // (seen: pooled ProjectileBase via PlaneConstraints) tick during yielded frames and their
    // GamePlane access throws, failing THIS test. Reset only where no frame can run afterwards
    // (synchronous tests, or after the final yield — TearDown's CleanupTestArena resets anyway).

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

        // Yield while still configured; TearDown resets GamePlane synchronously afterwards.
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

        // Yield while still configured; TearDown resets GamePlane synchronously afterwards.
        yield return null;
    }
}

} // namespace Tests.PlayMode
