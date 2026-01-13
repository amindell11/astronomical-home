using System;
using Game;
using Ships;
using Ships.Control;
using UnityEngine;

/// <summary>
/// Lightweight test bootstrapper that reuses <see cref="Factory.CreateShip"/> for deterministic ship creation.
/// Provides clean setup/teardown for play mode tests.
/// </summary>
public class TestServices : IDisposable
{
    public Ship Player { get; }
    public Ship Enemy { get; }
    public GameObject Arena { get; }

    private TestServices(Ship player, Ship enemy, GameObject arena)
    {
        Player = player;
        Enemy = enemy;
        Arena = arena;
    }

    /// <summary>
    /// Creates a test environment with two ships using the production Factory.
    /// Sets up GamePlane deterministically. Commanders are disabled by default to prevent AI interference.
    /// </summary>
    public static TestServices Create(
        Ship playerPrefab,
        Commander playerCommander,
        Ship enemyPrefab,
        Commander enemyCommander,
        Settings settings,
        float separation = 20f,
        bool disableCommanders = true)
    {
        var arena = CreateArena();

        var player = Factory.CreateShip(
            playerPrefab,
            playerCommander,
            settings,
            team: 0,
            Vector3.zero,
            Quaternion.identity);

        var enemy = Factory.CreateShip(
            enemyPrefab,
            enemyCommander,
            settings,
            team: 1,
            GamePlane.PlaneDirToWorld(Vector2.up) * separation,
            Quaternion.identity);

        if (disableCommanders)
        {
            DisableCommander(player);
            DisableCommander(enemy);
        }

        return new TestServices(player, enemy, arena);
    }

    private static void DisableCommander(Ship ship)
    {
        if (!ship) return;
        var commander = ship.GetComponentInChildren<Commander>();
        if (commander) commander.enabled = false;
    }

    /// <summary>
    /// Creates a test environment with a single ship.
    /// </summary>
    public static TestServices CreateSingle(
        Ship shipPrefab,
        Commander commander,
        Settings settings)
    {
        var arena = CreateArena();

        var ship = Factory.CreateShip(
            shipPrefab,
            commander,
            settings,
            team: 0,
            Vector3.zero,
            Quaternion.identity);

        return new TestServices(ship, null, arena);
    }

    private static GameObject CreateArena()
    {
        var arena = new GameObject("TestArena");

        var plane = new GameObject("ReferencePlane");
        plane.tag = "ReferencePlane";
        plane.transform.SetParent(arena.transform);
        plane.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(90f, 0f, 0f));

        GamePlane.SetReferencePlane(plane.transform);

        return arena;
    }

    public void Dispose()
    {
        if (Player)
            UnityEngine.Object.DestroyImmediate(Player.gameObject);
        if (Enemy)
            UnityEngine.Object.DestroyImmediate(Enemy.gameObject);
        if (Arena)
            UnityEngine.Object.DestroyImmediate(Arena);

        GamePlane.SetReferencePlane(null);
    }
}
