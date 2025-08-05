using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// PlayMode test for shield regeneration validation.
/// Test Case: Ship with full shield, apply damage - Health untouched until shield <= 0; regen after shieldRegenDelay.
/// </summary>
public class ShieldRegenerationPlayMode
{
    private GameObject testScene;
    private Ship testShip;
    private DamageHandler damageHandler;
    private Settings settings;
    private GameObject referencePlane;

    private const float RegenDelay = 0.2f;      // seconds
    private const float RegenRate  = 200f;       // shield per second (fast for tests)
    private const float MaxShield  = 100f;
    private const float MaxHealth  = 100f;

    [SetUp]
    public void SetUp()
    {
        TestSceneBuilder.EnableDebugRendering();

        LogAssert.ignoreFailingMessages = true;

        // --- Minimal reference plane so GamePlane utilities work correctly ---
        referencePlane = GameObject.CreatePrimitive(PrimitiveType.Plane);
        referencePlane.name = "ReferencePlane";
        referencePlane.tag  = "ReferencePlane";
        // Ensure normal is +Z (plane.forward)
        referencePlane.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
        Object.DestroyImmediate(referencePlane.GetComponent<Collider>()); // not needed
        GamePlane.SetReferencePlane(referencePlane.transform);

        // --- Create a simple ship with damage handler only (movement not required) ---
        testScene = new GameObject("ShieldRegenTestScene");
        GameObject shipGO = new GameObject("TestShip");
        shipGO.transform.SetParent(testScene.transform);

        // Rigidbody is required by ShipMovement (added automatically)
        shipGO.AddComponent<Rigidbody>();

        // Required components due to attributes
        var movement = shipGO.AddComponent<Movement>();
        movement.PopulateSettings(ScriptableObject.CreateInstance<Settings>());
        movement.enabled = false; // not needed for shield regen tests
        var handler  = shipGO.AddComponent<DamageHandler>();

        // Set tunable values on the handler directly.
        handler.maxHealth        = MaxHealth;
        handler.maxShield        = MaxShield;
        handler.startingLives    = 1;
        handler.shieldRegenDelay = RegenDelay;
        handler.shieldRegenRate  = RegenRate;

        // Reset internal state with the configured numbers.
        handler.ResetDamageState();

        testShip      = shipGO.GetComponent<Ship>(); // may be null – not required for these tests
        damageHandler = handler;
    }

    [TearDown]
    public void TearDown()
    {
        if (testScene != null)
        {
            Object.DestroyImmediate(testScene);
        }
        if (referencePlane != null)
        {
            Object.DestroyImmediate(referencePlane);
        }

        testScene = null;
        testShip  = null;
        damageHandler = null;
    }

    // -------------------------------------------------- Tests --------------------------------------------------

    [UnityTest]
    public IEnumerator ShieldDamage_HealthUntouched_UntilShieldsDepleted()
    {
        // Arrange
        float initialHealth = damageHandler.CurrentHealth;
        float initialShield = damageHandler.CurrentShield;
        float dmg = 30f; // less than shield

        // Act
        damageHandler.TakeDamage(dmg, 1f, Vector3.zero, Vector3.zero, null);
        yield return null; // wait a frame so events process

        // Assert
        Assert.AreEqual(initialHealth, damageHandler.CurrentHealth, 0.01f, "Health should remain unchanged while shields absorb damage.");
        Assert.AreEqual(initialShield - dmg, damageHandler.CurrentShield, 0.01f, "Shield should decrease exactly by damage amount.");
    }

    [UnityTest]
    public IEnumerator ShieldDamage_ExceedsShield_OnlyShieldTakesDamage()
    {
        // Arrange – reset shields to full
        damageHandler.ResetDamageState();
        float initialHealth = damageHandler.CurrentHealth;
        float dmg = MaxShield + 20f; // greater than full shield

        // Act
        damageHandler.TakeDamage(dmg, 1f, Vector3.zero, Vector3.zero, null);
        yield return null;

        // Assert – single-hit rule: excess does NOT spill to health
        Assert.AreEqual(0f, damageHandler.CurrentShield, 0.01f, "Shield should be depleted to zero.");
        Assert.AreEqual(initialHealth, damageHandler.CurrentHealth, 0.01f, "Health should stay intact when single hit depletes shields.");
    }

    [UnityTest]
    public IEnumerator ShieldRegeneration_AfterDelay_RestoresShields()
    {
        // Arrange – partial shield damage
        damageHandler.ResetDamageState();
        float dmg = 40f;
        damageHandler.TakeDamage(dmg, 1f, Vector3.zero, Vector3.zero, null);
        float damagedShield = damageHandler.CurrentShield; // should be MaxShield - dmg

        // Act – wait half of regen delay and confirm no regen yet
        yield return new WaitForSeconds(RegenDelay * 0.5f);
        Assert.AreEqual(damagedShield, damageHandler.CurrentShield, 0.01f, "Shield should not regenerate before regenDelay elapses.");

        // Wait past the regen delay so regen starts
        yield return new WaitForSeconds(RegenDelay * 0.75f);

        // Assert – some regeneration occurred
        Assert.Greater(damageHandler.CurrentShield, damagedShield + 0.1f, "Shield should begin regenerating after regenDelay.");
    }

    [UnityTest]
    public IEnumerator ShieldRegeneration_InterruptedByDamage_RestartsDelay()
    {
        // Arrange – initial damage
        damageHandler.ResetDamageState();
        float firstHit = 30f;
        damageHandler.TakeDamage(firstHit, 1f, Vector3.zero, Vector3.zero, null);
        float shieldAfterFirst = damageHandler.CurrentShield;

        // Wait half the delay
        yield return new WaitForSeconds(RegenDelay * 0.5f);

        // Apply second hit – this should reset the timer
        float secondHit = 10f;
        damageHandler.TakeDamage(secondHit, 1f, Vector3.zero, Vector3.zero, null);
        float shieldAfterSecond = damageHandler.CurrentShield;
        Assert.AreEqual(shieldAfterFirst - secondHit, shieldAfterSecond, 0.01f, "Shield should reflect second damage application.");

        // Wait just under full delay – regen should NOT have started yet
        yield return new WaitForSeconds(RegenDelay * 0.8f);
        Assert.AreEqual(shieldAfterSecond, damageHandler.CurrentShield, 0.01f, "Shield regen should have been postponed by the second hit.");

        // Wait beyond delay to allow regen
        yield return new WaitForSeconds(RegenDelay * 0.4f);
        Assert.Greater(damageHandler.CurrentShield, shieldAfterSecond + 0.1f, "Shield should regenerate after updated regenDelay.");
    }

    [UnityTest]
    public IEnumerator ShieldRegeneration_FullyDepleted_RegeneratesCompletely()
    {
        // Arrange – deplete shields fully
        damageHandler.ResetDamageState();
        damageHandler.TakeDamage(MaxShield, 1f, Vector3.zero, Vector3.zero, null);
        Assert.AreEqual(0f, damageHandler.CurrentShield, 0.01f);

        // Calculate time needed to fully regen
        float timeToFull = RegenDelay + MaxShield / RegenRate + 0.1f; // extra padding
        yield return new WaitForSeconds(timeToFull);

        // Assert – back to full
        Assert.AreEqual(MaxShield, damageHandler.CurrentShield, 1f, "Shield should fully regenerate to max value.");
    }

    // TODO: Helper methods
    // - CreateShipWithShields()
    // - ApplyDamageToShip()
    // - WaitForShieldRegenDelay()
    // - MonitorShieldRegeneration()
    // - VerifyShieldRegenRate()
} 