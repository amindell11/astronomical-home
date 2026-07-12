using Game;
using NUnit.Framework;
using UnityEngine;

namespace Tests.PlayMode
{

[Category("Core")]
public class GamePlanePlayModeTests
{
    [Test]
    public void Frame_Y_SetsAxesAndOrigin()
    {
        var frame = new GamePlaneFrame(PlaneAxis.Y, new Vector3(10, 0, 5));

        Assert.AreEqual(Vector3.down, frame.Normal);
        Assert.AreEqual(Vector3.forward, frame.Forward);
        Assert.AreEqual(Vector3.right, frame.Right);
        Assert.AreEqual(new Vector3(10, 0, 5), frame.Origin);
        Assert.AreEqual(RigidbodyConstraints.FreezePositionY, frame.PositionConstraint);
    }

    [Test]
    public void Frame_WorldToPlane_And_PlaneToWorld_AreConsistent()
    {
        var frame = new GamePlaneFrame(PlaneAxis.Y, new Vector3(10, 0, 5));
        var worldPoint = new Vector3(12, 0, 8);

        var planePoint = frame.WorldPointToPlane(worldPoint);
        var worldPointBack = frame.PlanePointToWorld(planePoint);

        Assert.That(worldPointBack.x, Is.EqualTo(worldPoint.x).Within(0.01f));
        Assert.That(worldPointBack.z, Is.EqualTo(worldPoint.z).Within(0.01f));
    }

    [Test]
    public void Frame_Z_SetsXYPlaneBasis()
    {
        var frame = new GamePlaneFrame(PlaneAxis.Z);

        Assert.AreEqual(Vector3.forward, frame.Normal);
        Assert.AreEqual(Vector3.up, frame.Forward);
        Assert.AreEqual(Vector3.right, frame.Right);
        Assert.AreEqual(RigidbodyConstraints.FreezePositionZ, frame.PositionConstraint);
    }

    [Test]
    public void Canonical_IsFrozenZFrameAtOrigin()
    {
        var z = new GamePlaneFrame(PlaneAxis.Z);

        Assert.AreEqual(Vector3.zero, GamePlane.Canonical.Origin);
        Assert.AreEqual(z.Normal, GamePlane.Canonical.Normal);
        Assert.AreEqual(z.Forward, GamePlane.Canonical.Forward);
        Assert.AreEqual(z.Right, GamePlane.Canonical.Right);
        Assert.AreEqual(z.PositionConstraint, GamePlane.Canonical.PositionConstraint);
    }
}

} // namespace Tests.PlayMode
