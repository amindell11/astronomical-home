using AI;
using Game;
using Game.Services;
using Movement;
using Ships;
using UnityEngine;
using Game.Services.Projectiles;

namespace Tests.PlayMode.Common
{

/// <summary>Creates ships with common PlayMode-test configurations.</summary>
public static class ShipTestFactory
{
    public static Ship CreateKinematicPrimitiveShipAt(Vector2 planePosition)
    {
        var go = new GameObject("Kinematic Primitive Test Ship");
        go.transform.SetPositionAndRotation(
            GamePlane.PlanePointToWorld(planePosition), GamePlane.Rotation);

        var body = go.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        go.AddComponent<SphereCollider>().radius = 0.5f;
        go.AddComponent<KinematicsPoller>();

        var ship = go.AddComponent<Ship>();
        ship.Movement.enabled = false;
        return ship;
    }

    public static void MoveKinematicShip(Ship ship, Vector3 position)
    {
        if (!ship) throw new System.ArgumentNullException(nameof(ship));

        ship.transform.position = position;
        Physics.SyncTransforms();
        ship.Body.position = position;
    }

    /// <summary>Creates a ship with default test settings at the origin.</summary>
    /// <param name="projectiles">Registry the ship arms its weapons with (a fixture's <c>Projectiles</c>).</param>
    public static Ship CreateDefaultShip(IProjectileService projectiles, int team = 0, int decisionSeed = 0)
    {
        return CreateDefaultShipAt(Vector3.zero, Quaternion.identity, projectiles, team, decisionSeed);
    }

    /// <summary>Creates a ship with default test settings at a specified position and rotation.</summary>
    /// <param name="projectiles">Registry the ship arms its weapons with (a fixture's <c>Projectiles</c>).</param>
    public static Ship CreateDefaultShipAt(Vector3 position, Quaternion rotation, IProjectileService projectiles, int team = 0, int decisionSeed = 0)
    {
        var shipPrefab = TestAssets.LoadShip2Prefab();
        var cmdrPrefab = TestAssets.LoadTestPilotMpc();

        if (shipPrefab == null || cmdrPrefab == null)
        {
            Debug.LogError("Failed to load required assets for ship creation");
            return null;
        }

        return Factory.CreateShip(shipPrefab, cmdrPrefab, team, decisionSeed, projectiles, position, rotation);
    }

    /// <summary>Creates a ship with custom assets.</summary>
    /// <param name="projectiles">Registry the ship arms its weapons with (a fixture's <c>Projectiles</c>).</param>
    public static Ship CreateShip(
        Ship shipPrefab,
        AICommander cmdrPrefab,
        IProjectileService projectiles,
        int team = 0,
        Vector3? position = null,
        Quaternion? rotation = null,
        int decisionSeed = 0)
    {
        return Factory.CreateShip(
            shipPrefab,
            cmdrPrefab,
            team,
            decisionSeed,
            projectiles,
            position ?? Vector3.zero,
            rotation ?? Quaternion.identity);
    }

    /// <summary>Safely destroys a ship game object (null-tolerant).</summary>
    public static void DestroyShip(Ship ship)
    {
        if (ship != null)
            Object.Destroy(ship.gameObject);
    }
}

}
