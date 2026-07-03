using AI;
using Ships;
using UnityEngine;

namespace Tests.PlayMode.Common
{

/// <summary>
/// Factory for creating ships in PlayMode tests with common configurations.
/// Reduces duplication of ship creation and AI commander setup.
/// </summary>
public static class ShipTestFactory
{
    /// <summary>
    /// Creates a ship with default test settings at the origin.
    /// </summary>
    /// <param name="useMpcPilot">Retained for call-site compatibility; the MPC pilot is the only pilot now.</param>
    /// <param name="team">Team ID for the ship (default: 0)</param>
    /// <returns>Created ship instance, or null if asset loading fails</returns>
    public static Ship CreateDefaultShip(bool useMpcPilot = true, int team = 0)
    {
        return CreateDefaultShipAt(Vector3.zero, Quaternion.identity, useMpcPilot, team);
    }

    /// <summary>
    /// Creates a ship with default test settings at a specified position and rotation.
    /// </summary>
    /// <param name="position">World position</param>
    /// <param name="rotation">World rotation</param>
    /// <param name="useMpcPilot">Retained for call-site compatibility; the MPC pilot is the only pilot now.</param>
    /// <param name="team">Team ID for the ship (default: 0)</param>
    /// <returns>Created ship instance, or null if asset loading fails</returns>
    public static Ship CreateDefaultShipAt(Vector3 position, Quaternion rotation, bool useMpcPilot = true, int team = 0)
    {
        var settings = TestAssets.LoadDefaultShipSettings();
        var shipPrefab = TestAssets.LoadShip2Prefab();
        var cmdrPrefab = TestAssets.LoadTestPilotMpc();

        if (settings == null || shipPrefab == null || cmdrPrefab == null)
        {
            Debug.LogError("Failed to load required assets for ship creation");
            return null;
        }

        return Factory.CreateShip(shipPrefab, cmdrPrefab, settings, team, position, rotation);
    }

    /// <summary>
    /// Creates a ship with custom assets.
    /// </summary>
    /// <param name="shipPrefab">Ship prefab to instantiate</param>
    /// <param name="cmdrPrefab">AI commander prefab</param>
    /// <param name="settings">Ship settings</param>
    /// <param name="team">Team ID for the ship (default: 0)</param>
    /// <param name="position">World position (default: origin)</param>
    /// <param name="rotation">World rotation (default: identity)</param>
    /// <returns>Created ship instance</returns>
    public static Ship CreateShip(
        Ship shipPrefab,
        AICommander cmdrPrefab,
        FrameSettings settings,
        int team = 0,
        Vector3? position = null,
        Quaternion? rotation = null)
    {
        return Factory.CreateShip(
            shipPrefab,
            cmdrPrefab,
            settings,
            team,
            position ?? Vector3.zero,
            rotation ?? Quaternion.identity);
    }

    /// <summary>
    /// Safely destroys a ship game object.
    /// </summary>
    /// <param name="ship">Ship to destroy (can be null)</param>
    public static void DestroyShip(Ship ship)
    {
        if (ship != null)
            Object.Destroy(ship.gameObject);
    }
}

} // namespace Tests.PlayMode.Common
