using UnityEngine;
using ShipControl;

/// <summary>
/// Helper utility for programmatic scene composition to keep tests deterministic.
/// Mentioned in Testing Plan section 3.2 as a helper utility.
/// </summary>
public static class TestSceneBuilder
{
    /// <summary>
    /// Creates a basic test arena with reference plane for 2D gameplay.
    /// </summary>
    /// <param name="size">Size of the arena plane</param>
    /// <returns>Root GameObject of the test arena</returns>
    public static GameObject CreateTestArena(float size = 100f)
    {
        // TODO: Implementation pending
        // - Create parent GameObject for the arena
        // - Add reference plane for 2D gameplay constraints
        // - Configure physics layers if needed
        // - Return arena root GameObject
        
        var arena = new GameObject("TestArena");
        
        // Create reference plane
        var referencePlane = GameObject.CreatePrimitive(PrimitiveType.Plane);
        referencePlane.name = "ReferencePlane";
        referencePlane.tag = "ReferencePlane";
        referencePlane.transform.SetParent(arena.transform);
        referencePlane.transform.localScale = Vector3.one * (size / 10f); // Plane primitive is 10x10 units
        referencePlane.transform.rotation = Quaternion.Euler(90, 0, 0); // Orient for XZ plane
        
        // Remove collider from reference plane (it's just for reference)
        Object.DestroyImmediate(referencePlane.GetComponent<Collider>());
        
        // Ensure reference plane known to GamePlane
        GamePlane.SetReferencePlane(referencePlane.transform);

        // If debug rendering is active, ensure we have a camera and run at real-time speed.
        if (_debugRenderingEnabled)
        {
            EnsureDebugCameraExists();
            Time.timeScale = 1f;
        }
        
        return arena;
    }

    /// <summary>
    /// Creates a ship with basic components for testing by instantiating from prefab.
    /// </summary>
    /// <param name="shipName">Name for the ship GameObject</param>
    /// <param name="shipType">Type of ship to create</param>
    /// <returns>The created Ship component</returns>
    public static Ship CreateTestShip(string shipName = "TestShip", ShipType shipType = ShipType.Basic)
    {
        // Try to load from Resources first, fallback to AssetDatabase in editor
        GameObject shipPrefab = LoadShipPrefab(shipType);
        
        if (shipPrefab == null)
        {
            Debug.LogError($"Could not load ship prefab for type: {shipType}");
            return null;
        }
        
        return CreateTestShipFromPrefab(shipPrefab, shipName, shipType);
    }

    /// <summary>
    /// Creates a ship from a specific prefab GameObject.
    /// </summary>
    /// <param name="shipPrefab">The ship prefab to instantiate</param>
    /// <param name="shipName">Name for the instantiated ship</param>
    /// <returns>The created Ship component</returns>
    public static Ship CreateTestShipFromPrefab(GameObject shipPrefab, string shipName = "TestShip", ShipType shipType = ShipType.Basic)
    {
        if (shipPrefab == null)
        {
            Debug.LogError("Ship prefab is null");
            return null;
        }
        
        // Instantiate the prefab
        GameObject shipGO = Object.Instantiate(shipPrefab);
        shipGO.name = shipName;
        
        // Get the Ship component
        Ship ship = shipGO.GetComponent<Ship>();
        if (ship == null)
        {
            Debug.LogError($"Ship prefab does not have a Ship component: {shipPrefab.name}");
            Object.DestroyImmediate(shipGO);
            return null;
        }
        
        // Configure for testing if needed
        ConfigureShipForTesting(ship, shipType);
        
        return ship;
    }

    /// <summary>
    /// Loads a ship prefab based on the ship type.
    /// </summary>
    /// <param name="shipType">Type of ship to load</param>
    /// <returns>The loaded GameObject prefab, or null if not found</returns>
    private static GameObject LoadShipPrefab(ShipType shipType)
    {
        // First try Resources folder
        string resourcesPath = GetShipPrefabResourcesPath(shipType);
        GameObject prefab = Resources.Load<GameObject>(resourcesPath);
        
        if (prefab != null)
            return prefab;

#if UNITY_EDITOR
        // Fallback to AssetDatabase in editor
        string assetPath = GetShipPrefabAssetPath(shipType);
        prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        
        if (prefab != null)
            return prefab;
#endif

        // Last resort: try to find any ship prefab
        return TryFindAnyShipPrefab();
    }

    /// <summary>
    /// Gets the Resources folder path for the specified ship type.
    /// </summary>
    private static string GetShipPrefabResourcesPath(ShipType shipType)
    {
        return shipType switch
        {
            ShipType.Basic  => "Prefabs/Ships/Ship_1",
            ShipType.Player => "Prefabs/Ships/Ship_1",
            ShipType.AI     => "Prefabs/Ships/Ship_2",
            ShipType.Enemy  => "Prefabs/Ships/Ship_3",
            ShipType.RL     => "Prefabs/Ships/Ship_1",
            _               => "Prefabs/Ships/Ship_1"
        };
    }

#if UNITY_EDITOR
    /// <summary>
    /// Gets the asset path for the specified ship type.
    /// </summary>
    private static string GetShipPrefabAssetPath(ShipType shipType)
    {
        return shipType switch
        {
            ShipType.Basic  => "Assets/Prefabs/Ships/Ship_1.prefab",
            ShipType.Player => "Assets/Prefabs/Ships/Ship_1.prefab",
            ShipType.AI     => "Assets/Prefabs/Ships/Ship_2.prefab",
            ShipType.Enemy  => "Assets/Prefabs/Ships/Ship_3.prefab",
            ShipType.RL     => "Assets/Prefabs/Ships/Ship_1.prefab",
            _               => "Assets/Prefabs/Ships/Ship_1.prefab"
        };
    }
#endif

    /// <summary>
    /// Tries to find any available ship prefab as a fallback.
    /// </summary>
    private static GameObject TryFindAnyShipPrefab()
    {
#if UNITY_EDITOR
        // Search for any prefab with a Ship component
        string[] guids = UnityEditor.AssetDatabase.FindAssets("t:Prefab Ship");
        foreach (string guid in guids)
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null && prefab.GetComponent<Ship>() != null)
            {
                Debug.LogWarning($"Using fallback ship prefab: {prefab.name}");
                return prefab;
            }
        }
#endif
        return null;
    }

    /// <summary>
    /// Configures a ship for testing purposes.
    /// </summary>
    /// <param name="ship">Ship to configure</param>
    private static void ConfigureShipForTesting(Ship ship, ShipType shipType)
    {
        // Ensure ship has test-friendly settings
        if (ship.settings == null)
        {
            // Create default settings if none exist
            ship.settings = ScriptableObject.CreateInstance<ShipSettings>();
        }
        
        // Disable any problematic components for testing
        // (e.g., audio, effects that might interfere with test determinism)
        
        // Ensure physics is enabled
        var rb = ship.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = false; // Game uses 2D physics
        }

        // Attach pilot prefab when appropriate for tests
        AttachPilotForShipType(ship, shipType);
    }

    /// <summary>
    /// Instantiates and attaches a suitable pilot prefab for the specified ship type.
    /// If the prefab cannot be found (or is unnecessary) the method safely does nothing.
    /// </summary>
    private static void AttachPilotForShipType(Ship ship, ShipType shipType)
    {
#if UNITY_EDITOR
        // Determine pilot prefab name based on ship type
        string pilotName = shipType switch
        {
            ShipType.Player => "PlayerPilot",
            ShipType.RL     => "RLPilot",
            _               => "UtilityPilot" // Basic / AI / Enemy can share UtilityPilot
        };

        string assetPath = $"Assets/Prefabs/Ships/Pilots/{pilotName}.prefab";
        GameObject pilotPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        if (pilotPrefab == null) return; // Not found – tests may not require the pilot

        GameObject pilotInstance = Object.Instantiate(pilotPrefab, ship.transform);
        pilotInstance.name = pilotName;

        // Ensure any newly added command sources are initialised
        foreach (var src in pilotInstance.GetComponentsInChildren<IShipCommandSource>(true))
        {
            src.InitializeCommander(ship);
        }
#endif
    }

    /// <summary>
    /// Creates a simple targetable object for missile tests.
    /// </summary>
    /// <param name="targetName">Name for the target GameObject</param>
    /// <returns>The created GameObject with ITargetable implementation</returns>
    public static GameObject CreateTestTarget(string targetName = "TestTarget")
    {
        // TODO: Implementation pending
        // - Create GameObject with ITargetable component
        // - Add basic collider for hit detection
        // - Return configured target
        
        var targetGO = new GameObject(targetName);
        targetGO.AddComponent<BoxCollider>();
        
        // TODO: Add ITargetable implementation
        // TODO: Add any other required components
        
        return targetGO;
    }

    /// <summary>
    /// Creates an obstacle for line-of-sight testing.
    /// </summary>
    /// <param name="position">Position for the obstacle</param>
    /// <param name="size">Size of the obstacle</param>
    /// <returns>The created obstacle GameObject</returns>
    public static GameObject CreateObstacle(Vector3 position, Vector3 size)
    {
        // TODO: Implementation pending
        // - Create obstacle with collider
        // - Position and scale appropriately
        // - Configure for LOS blocking
        
        var obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
        obstacle.name = "TestObstacle";
        obstacle.transform.position = position;
        obstacle.transform.localScale = size;
        
        return obstacle;
    }

    /// <summary>
    /// Positions two objects with specified distance and angle for testing.
    /// </summary>
    /// <param name="shooter">The shooting object</param>
    /// <param name="target">The target object</param>
    /// <param name="distance">Distance between objects</param>
    /// <param name="angle">Angle in degrees (0 = target directly ahead)</param>
    public static void PositionForTest(Transform shooter, Transform target, float distance, float angle = 0f)
    {
        // TODO: Implementation pending
        // - Calculate target position based on shooter position, distance, and angle
        // - Apply positions to transforms
        // - Ensure both objects are on the reference plane
        
        if (shooter == null || target == null) return;
        
        // Place shooter at origin for simplicity
        shooter.position = Vector3.zero;
        shooter.rotation = Quaternion.identity;
        
        // Calculate target position
        float angleRad = angle * Mathf.Deg2Rad;
        Vector3 targetPos = new Vector3(
            Mathf.Sin(angleRad) * distance,
            0f, // Keep on reference plane
            Mathf.Cos(angleRad) * distance
        );
        
        target.position = targetPos;
    }

    /// <summary>
    /// Creates a moving target that follows a predictable pattern for testing.
    /// </summary>
    /// <param name="targetName">Name for the moving target</param>
    /// <param name="movementType">Type of movement pattern</param>
    /// <returns>The created GameObject with movement script</returns>
    public static GameObject CreateMovingTarget(string targetName = "MovingTarget", MovementType movementType = MovementType.Circular)
    {
        // TODO: Implementation pending
        // - Create target with ITargetable
        // - Add movement script based on movement type
        // - Configure predictable movement pattern
        
        var target = CreateTestTarget(targetName);
        
        // TODO: Add movement component based on movementType
        
        return target;
    }

    /// <summary>
    /// Applies damage directly to a ship's damage handler for testing.
    /// </summary>
    /// <param name="ship">Target ship</param>
    /// <param name="damage">Damage amount</param>
    /// <param name="source">Damage source (can be null)</param>
    public static void ApplyTestDamage(Ship ship, float damage, GameObject source = null)
    {
        // TODO: Implementation pending
        // - Apply damage through proper damage system
        // - Use realistic damage parameters
        
        if (ship?.damageHandler != null)
        {
            ship.damageHandler.TakeDamage(
                damage, 
                1f, // projectile mass
                Vector3.zero, // projectile velocity
                ship.transform.position, // hit point
                source // damage source
            );
        }
    }

    public enum MovementType
    {
        Stationary,
        Linear,
        Circular,
        Random
    }

    public enum ShipType
    {
        Basic,
        Player,
        AI,
        Enemy,
        RL
    }

    #region Debug Rendering Support

    /// <summary>
    /// When <c>true</c>, play-mode tests will execute at real-time speed and a simple
    /// camera will be spawned so the scene can be inspected while the test runs.
    /// This can be enabled in three ways:
    /// 1) Call <see cref="EnableDebugRendering"/> from your test code.
    /// 2) Define the environment variable <c>UNITY_TESTS_DEBUG_RENDER</c> with the value "1".
    /// 3) (Editor only) Add the scripting define symbol <c>UNITY_TESTS_DEBUG_RENDER</c>.
    /// </summary>
    private static bool _debugRenderingEnabled;

#if UNITY_TESTS_DEBUG_RENDER
    private const bool k_DefaultDebugRender = true;
#else
    private const bool k_DefaultDebugRender = false;
#endif

    static TestSceneBuilder()
    {
        // Initialise the flag from compile-time define or environment variable.
        _debugRenderingEnabled = k_DefaultDebugRender;

        string env = System.Environment.GetEnvironmentVariable("UNITY_TESTS_DEBUG_RENDER");
        if (!string.IsNullOrEmpty(env) && (env == "1" || env.Equals("true", System.StringComparison.OrdinalIgnoreCase)))
        {
            _debugRenderingEnabled = true;
        }
    }

    /// <summary>
    /// Enable or disable debug rendering at runtime.
    /// </summary>
    public static void EnableDebugRendering(bool enable = true)
    {
        _debugRenderingEnabled = enable;

        // Force real-time speed so behaviour matches in-game.
        if (enable)
            Time.timeScale = 1f;
    }

    /// <summary>
    /// Helper that conditionally spawns a basic top-down camera when debug rendering is on.
    /// </summary>
    private static void EnsureDebugCameraExists()
    {
        // No-op - cameras are no longer created by this test utility
    }

    #endregion Debug Rendering Support
} 