using Game;
using UnityEngine;

namespace Tests.PlayMode
{

/// <summary>
/// Minimal utilities for programmatic test scene composition.
/// For ship creation, use <see cref="Ships.Factory"/> directly.
/// </summary>
public static class TestSceneBuilder
{
    private static GameObject _currentArena;

    /// <summary>
    /// Creates or returns the existing test arena with a properly configured reference plane.
    /// Ensures only one arena exists per test run to prevent temp scene proliferation.
    /// </summary>
    public static GameObject CreateTestArena()
    {
        // Reuse existing arena if already created
        if (_currentArena)
            return _currentArena;

        _currentArena = new GameObject("TestArena");

        var plane = new GameObject("ReferencePlane");
        plane.tag = "ReferencePlane";
        plane.transform.SetParent(_currentArena.transform);
        plane.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(90f, 0f, 0f));

        GamePlane.SetReferencePlane(plane.transform);

        return _currentArena;
    }

    /// <summary>
    /// Cleans up the test arena and resets GamePlane. Call this in test TearDown.
    /// </summary>
    public static void CleanupTestArena()
    {
        if (_currentArena != null)
        {
            Object.DestroyImmediate(_currentArena);
            _currentArena = null;
        }
        GamePlane.Reset();
    }

    /// <summary>
    /// Positions two transforms for testing with specified distance and angle.
    /// </summary>
    /// <param name="shooter">The shooting object (placed at origin)</param>
    /// <param name="target">The target object</param>
    /// <param name="distance">Distance between objects</param>
    /// <param name="angle">Angle in degrees (0 = target directly ahead on plane Y axis)</param>
    public static void PositionForTest(Transform shooter, Transform target, float distance, float angle = 0f)
    {
        if (!shooter || !target) return;

        shooter.position = Vector3.zero;
        shooter.rotation = Quaternion.identity;

        var rad = angle * Mathf.Deg2Rad;
        var offset2D = new Vector2(Mathf.Sin(rad), Mathf.Cos(rad)) * distance;
        target.position = GamePlane.PlanePointToWorld(offset2D);
    }

    /// <summary>
    /// Creates an obstacle for line-of-sight / navigation testing.
    /// </summary>
    public static GameObject CreateObstacle(Vector3 position, Vector3 size)
    {
        var obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
        obstacle.name = "TestObstacle";
        obstacle.layer = LayerMask.NameToLayer("Asteroid");

        var planePosition = GamePlane.PlanePointToWorld(new Vector2(position.x, position.y));
        obstacle.transform.position = planePosition + GamePlane.Normal * position.z;
        obstacle.transform.localScale = size;
        return obstacle;
    }
}

} // namespace Tests.PlayMode
