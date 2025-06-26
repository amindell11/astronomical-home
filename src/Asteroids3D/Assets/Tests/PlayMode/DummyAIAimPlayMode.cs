using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using ShipControl;

/// <summary>
/// PlayMode test for dummy AI aim validation.
/// Test Case: Dummy vs stationary player - AIShipInput.TryFireLaser called when LOS within tolerance.
/// </summary>
public class DummyAIAimPlayMode
{
    private Ship shooterShip;
    private Ship targetShip;
    private AIShipInput aiCommander;
    private LaserGun aiLaserGun;
    private GameObject referencePlane;
    private GameObject arenaRoot;

    // Shared constants for easy tuning
    private const float FireDistance = 20f;

    [SetUp]
    public void SetUp()
    {
        LogAssert.ignoreFailingMessages = true;

      TestSceneBuilder.EnableDebugRendering();      // --------------    ----------------------------------------------------
        // 1. Minimal 2-D arena & reference plane so GamePlane logic works
        // ------------------------------------------------------------------
        referencePlane = GameObject.CreatePrimitive(PrimitiveType.Plane);
        referencePlane.name = "ReferencePlane";
        referencePlane.tag  = "ReferencePlane";
        Object.DestroyImmediate(referencePlane.GetComponent<Collider>()); // not needed
        GamePlane.SetReferencePlane(referencePlane.transform);

        arenaRoot = new GameObject("DummyAIAimArena");

        // ------------------------------------------------------------------
        // 2. Create AI-controlled shooter ship from prefab (Enemy variant)
        // ------------------------------------------------------------------
        shooterShip = TestSceneBuilder.CreateTestShip("AI_Ship", TestSceneBuilder.ShipType.Enemy);
        if (shooterShip == null)
            Assert.Fail("Failed to load EnemyShip Variant prefab for shooter ship.");

        shooterShip.transform.SetParent(arenaRoot.transform);

        // Ensure we have an AI commander – add one if the prefab lacks it
        aiCommander = shooterShip.GetComponent<AIShipInput>();
        if (aiCommander == null)
            aiCommander = shooterShip.gameObject.AddComponent<AIShipInput>();

        aiLaserGun  = shooterShip.GetComponentInChildren<LaserGun>();
        aiCommander.difficulty = 1f; // max skill for most tests
        
        // Enable ship movement so it can actually rotate when commanded
        var movement = shooterShip.GetComponent<ShipMovement>();
        if (movement != null)
        {
            movement.enabled = true;
        }

        // ------------------------------------------------------------------
        // 3. Create target ship (stationary) from prefab (Basic variant)
        // ------------------------------------------------------------------
        targetShip = TestSceneBuilder.CreateTestShip("Target_Ship", TestSceneBuilder.ShipType.Basic);
        if (targetShip == null)
            Assert.Fail("Failed to load Basic Ship prefab for target ship.");

        targetShip.transform.SetParent(arenaRoot.transform);

        // Basic collider for LOS checks
        if (!targetShip.GetComponent<Collider>())
        {
            var col = targetShip.gameObject.AddComponent<SphereCollider>();
            col.radius = 1f;
        }

        // ------------------------------------------------------------------
        // 4. Default positioning – directly ahead within fire distance
        // ------------------------------------------------------------------
        PositionForTest(shooterShip.transform, targetShip.transform, 10f, 0f);

        // Set navigation target so AIShipInput believes it has an enemy
        aiCommander.SetNavigationTarget(targetShip.transform, false);
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(arenaRoot);
        Object.DestroyImmediate(referencePlane);

        shooterShip = null;
        targetShip  = null;
        aiCommander = null;
        aiLaserGun  = null;
    }

    // --------------------------------------------------------------------------------------------
    // Helper – create a minimal but functional ship GameObject with required components
    private Ship CreateShip(string name, bool isAI)
    {
        GameObject go = new GameObject(name);
        go.transform.position = Vector3.zero;
        go.layer = LayerMask.NameToLayer("Ship");

        // Physics & collider
        var rb = go.AddComponent<Rigidbody>();
        rb.useGravity = false;
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        var collider = go.AddComponent<SphereCollider>();
        collider.radius = 1f;

        // Ship infrastructure
        var movement = go.AddComponent<ShipMovement>();
        movement.ApplySettings(ScriptableObject.CreateInstance<ShipSettings>());
        // Don't disable movement - ships need to rotate in tests
        go.AddComponent<ShipDamageHandler>();
        var laserGun = go.AddComponent<LaserGun>();
        // LaserGun doesn't need projectile prefab for this behavioural test

        if (isAI)
        {
            go.AddComponent<AIShipInput>();
        }

        var ship = go.AddComponent<Ship>();
        // ShipSettings auto-created by Ship if null; tuning not critical here
        return ship;
    }

    // Simple planar positioning helper (distance & bearing in degrees where 0° = forward/+Y)
    private void PositionForTest(Transform shooter, Transform target, float distance, float bearingDegrees)
    {
        shooter.position = Vector3.zero;
        shooter.rotation = Quaternion.identity;

        float rad = bearingDegrees * Mathf.Deg2Rad;
        Vector3 offset = new Vector3(Mathf.Sin(rad) * distance, Mathf.Cos(rad) * distance, 0f);
        target.position = offset;
    }

    // Generate a default ShipState struct for manual TryGetCommand calls
    private ShipState BuildDefaultState(Ship ship)
    {
        // Get actual position and velocity from the ship
        Vector2 pos2D = GamePlane.WorldToPlane(ship.transform.position);
        Vector2 vel2D = Vector2.zero;
        float angleDeg = 0f;
        float yawRate = 0f;
        
        // Try to get velocity from movement component if available
        var movement = ship.GetComponent<ShipMovement>();
        if (movement != null)
        {
            vel2D = movement.Velocity2D;
            angleDeg = movement.Kinematics.AngleDeg;
            yawRate = movement.Kinematics.YawRate;
        }
        else
        {
            // Fall back to calculating angle from transform
            Vector3 forward3D = ship.transform.up; // ships use up as forward in top-down view
            Vector2 forward2D = new Vector2(forward3D.x, forward3D.z).normalized;
            angleDeg = Vector2.SignedAngle(Vector2.up, forward2D);
            if (angleDeg < 0) angleDeg += 360f;
        }
        
        var kin = new ShipKinematics(pos2D, vel2D, angleDeg, yawRate);
        return new ShipState
        {
            Kinematics   = kin,
            IsLaserReady = true,
            MissileState = MissileLauncher.LockState.Idle,
            HealthPct    = 1f,
            ShieldPct    = 1f
        };
    }

    // --------------------------------------------------------------------------------------------
    [UnityTest]
    public IEnumerator DummyAI_StationaryTarget_WithinToleranceAndLOS_FiresLaser()
    {
        // Ensure perfect conditions already satisfied from SetUp.
        yield return null; // let one frame pass so Awake/Start complete

        // Debug: Log test setup conditions
        Debug.Log($"[TEST] Starting DummyAI_StationaryTarget_WithinToleranceAndLOS_FiresLaser");
        Debug.Log($"[TEST] Shooter position: {shooterShip.transform.position}");
        Debug.Log($"[TEST] Target position: {targetShip.transform.position}");
        Debug.Log($"[TEST] Shooter forward (up): {shooterShip.transform.up}");
        
        Vector3 toTarget = targetShip.transform.position - shooterShip.transform.position;
        float actualDistance = toTarget.magnitude;
        float actualAngle = Vector3.Angle(shooterShip.transform.up, toTarget);
        Debug.Log($"[TEST] Actual distance: {actualDistance:F1}, angle: {actualAngle:F1}°");
        
        // Check AI configuration
        Debug.Log($"[TEST] AI difficulty: {aiCommander.difficulty}");
        
        // Use reflection to access private fields for debugging
        var aiType = typeof(AIShipInput);
        var fireDistanceField = aiType.GetField("fireDistance", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var fireAngleToleranceField = aiType.GetField("fireAngleTolerance", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var gunField = aiType.GetField("gun", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var lineOfSightMaskField = aiType.GetField("lineOfSightMask", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        float fireDistance = fireDistanceField != null ? (float)fireDistanceField.GetValue(aiCommander) : -1f;
        float fireAngleTolerance = fireAngleToleranceField != null ? (float)fireAngleToleranceField.GetValue(aiCommander) : -1f;
        LaserGun aiGun = gunField != null ? (LaserGun)gunField.GetValue(aiCommander) : null;
        LayerMask lineOfSightMask = lineOfSightMaskField != null ? (LayerMask)lineOfSightMaskField.GetValue(aiCommander) : -1;
        
        Debug.Log($"[TEST] AI fireDistance: {fireDistance}");
        Debug.Log($"[TEST] AI fireAngleTolerance: {fireAngleTolerance}");
        Debug.Log($"[TEST] AI has gun (from field): {aiGun != null}");
        Debug.Log($"[TEST] AI has gun (from component): {aiLaserGun != null}");
        Debug.Log($"[TEST] AI lineOfSightMask: {lineOfSightMask.value}");

        var state = BuildDefaultState(shooterShip);
        Debug.Log($"[TEST] Built state - Kinematics: pos={state.Kinematics.Pos}, vel={state.Kinematics.Vel}, angle={state.Kinematics.AngleDeg}°");
        Debug.Log($"[TEST] Built state - IsLaserReady={state.IsLaserReady}, HealthPct={state.HealthPct}, ShieldPct={state.ShieldPct}");
        
        bool success = aiCommander.TryGetCommand(state, out ShipCommand cmd);
        
        Debug.Log($"[TEST] TryGetCommand result: success={success}");
        Debug.Log($"[TEST] Command - PrimaryFire={cmd.PrimaryFire}, SecondaryFire={cmd.SecondaryFire}");
        Debug.Log($"[TEST] Command - Thrust={cmd.Thrust:F2}, Strafe={cmd.Strafe:F2}");
        Debug.Log($"[TEST] Command - RotateToTarget={cmd.RotateToTarget}, TargetAngle={cmd.TargetAngle:F1}°");

        Assert.IsTrue(success, "AI should generate a command under these conditions.");
        Assert.IsTrue(cmd.PrimaryFire, "AI should set PrimaryFire when target within range, angle and LOS.");
    }

    [UnityTest]
    public IEnumerator DummyAI_TargetTooFar_DoesNotFire()
    {
        // Position target beyond fire distance
        PositionForTest(shooterShip.transform, targetShip.transform, FireDistance + 10f, 0f);
        yield return null;

        var state = BuildDefaultState(shooterShip);
        aiCommander.SetNavigationTarget(targetShip.transform, false);
        aiCommander.TryGetCommand(state, out ShipCommand cmd);

        Assert.IsFalse(cmd.PrimaryFire, "AI should not fire when target is beyond fire distance.");
    }

    [UnityTest]
    public IEnumerator DummyAI_TargetOutsideAngleTolerance_DoesNotFire()
    {
        // Place target at 90° to the right within distance
        PositionForTest(shooterShip.transform, targetShip.transform, 10f, 90f);
        yield return null;

        var state = BuildDefaultState(shooterShip);
        aiCommander.SetNavigationTarget(targetShip.transform, false);
        aiCommander.TryGetCommand(state, out ShipCommand cmd);

        Assert.IsFalse(cmd.PrimaryFire, "AI should not fire when target bearing exceeds angle tolerance.");
    }

    [UnityTest]
    public IEnumerator DummyAI_NoLineOfSight_DoesNotFire()
    {
        // Create an obstacle between shooter and target
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.transform.position    = new Vector3(0f, 5f, 0f); // midway on the Y axis
        wall.transform.localScale  = new Vector3(10f, 1f, 10f);
        wall.layer = LayerMask.NameToLayer("Asteroid");

        yield return null;

        Debug.Log($"[TEST] Starting DummyAI_NoLineOfSight_DoesNotFire");
        Debug.Log($"[TEST] Shooter position: {shooterShip.transform.position}, Target position: {targetShip.transform.position}");
        Debug.Log($"[TEST] Obstacle position: {wall.transform.position}, Layer: {LayerMask.LayerToName(wall.layer)}");
        
        Vector3 toTarget = targetShip.transform.position - shooterShip.transform.position;
        float actualDistance = toTarget.magnitude;
        float actualAngle = Vector3.Angle(shooterShip.transform.up, toTarget);
        Debug.Log($"[TEST] Pre-check: Distance to target: {actualDistance:F1}, Angle: {actualAngle:F1}°");

        // Use reflection to check the line of sight mask
        var aiType = typeof(AIShipInput);
        var lineOfSightMaskField = aiType.GetField("lineOfSightMask", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        // --- FIX: Correctly set the mask using proper LayerMask (not just layer number) ---
        LayerMask correctMask = LayerMask.GetMask("Asteroid");
        lineOfSightMaskField?.SetValue(aiCommander, correctMask);
        Debug.Log($"[TEST] Set correct lineOfSightMask to: {correctMask.value} (was incorrectly {LayerMask.NameToLayer("Asteroid")})");
        
        // --- CLEAR LOS CACHE: Force fresh raycast by resetting cache frame ---
        var losFrameField = aiType.GetField("losFrame", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        losFrameField?.SetValue(aiCommander, -1);
        Debug.Log($"[TEST] Cleared LOS cache to force fresh raycast");
        // ---------------------------------------------------------------------------------

        LayerMask lineOfSightMask = lineOfSightMaskField != null ? (LayerMask)lineOfSightMaskField.GetValue(aiCommander) : -1;
        Debug.Log($"[TEST] AI lineOfSightMask value: {lineOfSightMask.value} (Layers: {LayerMask.LayerToName(0)}, {LayerMask.LayerToName(1)}, ...)");
        Debug.Log($"[TEST] Wall layer: {LayerMask.LayerToName(wall.layer)} ({wall.layer}), Shooter layer: {LayerMask.LayerToName(shooterShip.gameObject.layer)} ({shooterShip.gameObject.layer})");
        Debug.Log($"[TEST] Line of sight mask includes Wall layer: {(lineOfSightMask.value & (1 << wall.layer)) != 0}");

        // Manual raycast for sanity check
        bool isBlocked = Physics.Raycast(shooterShip.transform.position, toTarget.normalized, actualDistance, lineOfSightMask);
        Debug.Log($"[TEST] Manual raycast check for block: {isBlocked}");
        Assert.IsTrue(isBlocked, "Manual raycast should confirm that the wall is blocking line of sight.");

        var state = BuildDefaultState(shooterShip);
        bool success = aiCommander.TryGetCommand(state, out ShipCommand cmd);

        Debug.Log($"[TEST] TryGetCommand result: success={success}");
        Debug.Log($"[TEST] Command - PrimaryFire={cmd.PrimaryFire}");
        Assert.IsFalse(cmd.PrimaryFire, "AI should not fire when LOS is blocked by an obstacle.");

        Object.DestroyImmediate(wall);
    }

    [UnityTest]
    public IEnumerator DummyAI_DifficultyBelowThreshold_DoesNotFire()
    {
        aiCommander.difficulty = 0.3f; // below 0.5 threshold
        yield return null;

        var state = BuildDefaultState(shooterShip);
        aiCommander.TryGetCommand(state, out ShipCommand cmd);

        Assert.IsFalse(cmd.PrimaryFire, "AI with low difficulty should refrain from firing.");
    }

    [UnityTest]
    public IEnumerator DummyAI_TargetMoving_TracksAndFires()
    {
        // Position target ahead but slightly offset to test tracking
        PositionForTest(shooterShip.transform, targetShip.transform, 15f, 3f); // Small initial offset angle
        aiCommander.SetNavigationTarget(targetShip.transform, false);

        // Enable the Ship component to process commands
        shooterShip.enabled = true;

        int maxFrames = 100; // Allow more frames for tracking
        bool fired = false;
        float targetSpeed = 0.05f; // Very slow target movement
        
        for (int i = 0; i < maxFrames; i++)
        {
            // Move target slowly to the side
            targetShip.transform.position += new Vector3(targetSpeed, 0f, 0f);
            
            // Wait for physics update so ship can process commands and rotate
            yield return new WaitForFixedUpdate();
            
            // Check if the AI commanded fire in the last frame
            if (shooterShip.CurrentCommand.PrimaryFire)
            {
                fired = true;
                Debug.Log($"[TEST] AI fired on frame {i}");
                break;
            }
            
            // Log tracking progress every 10 frames
            if (i % 10 == 0)
            {
                Vector3 toTarget = targetShip.transform.position - shooterShip.transform.position;
                float angle = Vector3.Angle(shooterShip.transform.up, toTarget);
                Debug.Log($"[TEST] Frame {i}: Angle to target = {angle:F1}°, Distance = {toTarget.magnitude:F1}");
                Debug.Log($"[TEST] Ship rotation = {shooterShip.transform.rotation.eulerAngles}");
            }
        }

        Assert.IsTrue(fired, "AI should fire at target when it aligns within tolerance.");
    }

    // TODO: Helper methods
    // - CreateAIShip()
    // - CreateStaticPlayerShip()
    // - PositionShipsForTest()
    // - SetupObstacle()
    // - MonitorAICommands()
    // - VerifyLaserFired()
    // - CreateMovingTarget()
} 