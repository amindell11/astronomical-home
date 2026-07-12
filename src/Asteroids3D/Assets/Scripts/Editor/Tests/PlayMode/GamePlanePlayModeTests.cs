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
    public void Frame_X_SetsYZPlaneBasis()
    {
        var frame = new GamePlaneFrame(PlaneAxis.X);

        Assert.AreEqual(Vector3.right, frame.Normal);
        Assert.AreEqual(Vector3.forward, frame.Forward);
        Assert.AreEqual(Vector3.up, frame.Right);
        Assert.AreEqual(RigidbodyConstraints.FreezePositionX, frame.PositionConstraint);
    }

    [Test]
    public void Frame_Rotation_IsPlanePoseOfNormalAndForward([Values(PlaneAxis.X, PlaneAxis.Y, PlaneAxis.Z)] PlaneAxis axis)
    {
        var frame = new GamePlaneFrame(axis, new Vector3(3, 4, 5));

        Assert.AreEqual(GamePlaneFrame.PlanePose(frame.Normal, frame.Forward), frame.Rotation);
    }

    [Test]
    public void Frame_DirConversions_RoundTrip([Values(PlaneAxis.X, PlaneAxis.Y, PlaneAxis.Z)] PlaneAxis axis)
    {
        // Origin is irrelevant for direction conversions; a nonzero one must not leak in.
        var frame = new GamePlaneFrame(axis, new Vector3(3, 4, 5));
        var planeDir = new Vector2(2f, -7f);

        var back = frame.WorldDirToPlane(frame.PlaneDirToWorld(planeDir));

        Assert.That(back.x, Is.EqualTo(planeDir.x).Within(0.001f));
        Assert.That(back.y, Is.EqualTo(planeDir.y).Within(0.001f));
    }

    [Test]
    public void Facade_DelegatesToCanonical()
    {
        var planePt = new Vector2(6f, -2f);
        var planeDir = new Vector2(-1f, 3f);
        var world = new Vector3(6f, -2f, 4f);

        Assert.AreEqual(GamePlane.Canonical.PlanePointToWorld(planePt), GamePlane.PlanePointToWorld(planePt));
        Assert.AreEqual(GamePlane.Canonical.PlaneDirToWorld(planeDir), GamePlane.PlaneDirToWorld(planeDir));
        Assert.AreEqual(GamePlane.Canonical.WorldPointToPlane(world), GamePlane.WorldPointToPlane(world));
        Assert.AreEqual(GamePlane.Canonical.WorldDirToPlane(world), GamePlane.WorldDirToPlane(world));
        Assert.AreEqual(GamePlane.Canonical.ProjectOntoPlane(world), GamePlane.ProjectOntoPlane(world));
        Assert.AreEqual(GamePlane.Canonical.Rotation, GamePlane.Rotation);
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
