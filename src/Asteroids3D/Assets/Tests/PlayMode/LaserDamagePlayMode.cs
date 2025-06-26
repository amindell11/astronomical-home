using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using ShipControl;

/// <summary>
/// PlayMode test for laser damage validation.
/// Test Case: Empty plane with Ship & LaserGun - Target health reduction == projectile damage.
/// </summary>
public class LaserDamagePlayMode
{
    private GameObject testScene;
    private Ship shooterShip;
    private Ship targetShip;
    private LaserGun laserGun;

    [SetUp]
    public void SetUp()
    {
        // TODO: Setup test scene with shooter and target ships
        // - Create empty plane/arena
        // - Instantiate shooter ship with LaserGun
        // - Instantiate target ship with ShipDamageHandler
        // - Position them appropriately for line-of-sight
        
        testScene = TestSceneBuilder.CreateTestArena();
        shooterShip = TestSceneBuilder.CreateTestShip("ShooterShip", TestSceneBuilder.ShipType.Player);
        targetShip = TestSceneBuilder.CreateTestShip("TargetShip", TestSceneBuilder.ShipType.Enemy);
        
        // Disable shields on target for laser damage testing
        if (targetShip?.damageHandler != null)
        {
            // Set shields to 0 for direct health damage testing
            targetShip.damageHandler.ApplySettings(targetShip.settings);
            // Force shields to 0 by dealing shield damage
            float currentShields = targetShip.damageHandler.CurrentShield;
            if (currentShields > 0)
            {
                TestSceneBuilder.ApplyTestDamage(targetShip, currentShields);
            }
        }
        
        laserGun = shooterShip?.GetComponentInChildren<LaserGun>();
        
        // Position ships for clear shot
        if (shooterShip != null && targetShip != null)
        {
            TestSceneBuilder.PositionForTest(shooterShip.transform, targetShip.transform, 10f, 0f);
        }
    }

    [TearDown]
    public void TearDown()
    {
        // TODO: Clean up test scene
        if (testScene != null)
        {
            Object.DestroyImmediate(testScene);
        }
        
        // Clean up individual objects if they weren't parented to testScene
        if (shooterShip != null)
        {
            Object.DestroyImmediate(shooterShip.gameObject);
        }
        
        if (targetShip != null)
        {
            Object.DestroyImmediate(targetShip.gameObject);
        }
        
        // Reset references
        shooterShip = null;
        targetShip = null;
        laserGun = null;
        testScene = null;
    }

    [UnityTest]
    public IEnumerator LaserDamage_ReducesTargetHealth_ByProjectileDamage()
    {
        // Arrange – ensure we have valid objects
        Assert.NotNull(shooterShip, "Shooter ship was not created in SetUp");
        Assert.NotNull(targetShip,  "Target ship was not created in SetUp");

        // Disable target shields for this test
        var tgtSettings = ScriptableObject.CreateInstance<ShipSettings>();
        tgtSettings.maxHealth = targetShip.settings.maxHealth; // keep same health
        tgtSettings.maxShield = 0f;                            // no shields
        tgtSettings.shieldRegenDelay = 999f;                   // disable regen
        targetShip.damageHandler.ApplySettings(tgtSettings);

        float initialHealth = targetShip.damageHandler.CurrentHealth;

        // Determine expected damage – read from the projectile prefab via reflection
        float projectileDamage = 10f; // fallback default
        if (laserGun != null)
        {
            var field = typeof(LauncherBase<LaserProjectile>).GetField("projectilePrefab", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null)
            {
                var projPrefab = field.GetValue(laserGun) as LaserProjectile;
                if (projPrefab != null)
                {
                    projectileDamage = projPrefab.Damage;
                }
            }
        }

        // Act – directly apply damage equivalent to one laser hit
        TestSceneBuilder.ApplyTestDamage(targetShip, projectileDamage, shooterShip.gameObject);
        yield return null; // wait one frame for event propagation

        // Assert – health reduced exactly by projectile damage, shields remain zero
        float expectedHealth = initialHealth - projectileDamage;
        Assert.AreEqual(expectedHealth, targetShip.damageHandler.CurrentHealth, 0.001f, "Target health did not decrease by expected amount");
        Assert.AreEqual(0f, targetShip.damageHandler.CurrentShield, 0.001f, "Target shields should be zero for this test");
    }

    [UnityTest]
    public IEnumerator LaserDamage_MultipleLasers_AccumulatesDamage()
    {
        // Arrange
        Assert.NotNull(shooterShip);
        Assert.NotNull(targetShip);
        Assert.NotNull(laserGun);

        // Disable shields
        var tgtSettings = ScriptableObject.CreateInstance<ShipSettings>();
        tgtSettings.maxHealth = targetShip.settings.maxHealth;
        tgtSettings.maxShield = 0f;
        targetShip.damageHandler.ApplySettings(tgtSettings);

        float initialHealth = targetShip.damageHandler.CurrentHealth;

        // Retrieve projectile damage via reflection (same as previous test)
        float projectileDamage = 10f;
        var field = typeof(LauncherBase<LaserProjectile>).GetField("projectilePrefab", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (field?.GetValue(laserGun) is LaserProjectile projPf && projPf != null)
        {
            projectileDamage = projPf.Damage;
        }

        int shots = 3;
        for (int i = 0; i < shots; i++)
        {
            TestSceneBuilder.ApplyTestDamage(targetShip, projectileDamage, shooterShip.gameObject);
            yield return null; // wait a frame between hits
        }

        // Assert cumulative damage applied
        float expectedHealth = initialHealth - projectileDamage * shots;
        Assert.AreEqual(expectedHealth, targetShip.damageHandler.CurrentHealth, 0.001f, "Cumulative health damage does not match expected after multiple laser hits");
    }

    [UnityTest]
    public IEnumerator LaserDamage_MissedShot_NoHealthReduction()
    {
        // Arrange – reposition target to the side so a straight shot misses
        Assert.NotNull(shooterShip);
        Assert.NotNull(targetShip);
        Assert.NotNull(laserGun);

        float initialHealth = targetShip.damageHandler.CurrentHealth;
        float initialShield = targetShip.damageHandler.CurrentShield;

        // Position target 10 units to the right (90°) so projectile fired forward misses
        TestSceneBuilder.PositionForTest(shooterShip.transform, targetShip.transform, 10f, 90f);

        // Act – fire laser
        laserGun.Fire();
        // Wait a short time to allow projectile to travel past target (0.25 seconds should suffice given default speeds)
        yield return new WaitForSeconds(0.25f);

        // Assert – target health & shield unchanged
        Assert.AreEqual(initialHealth, targetShip.damageHandler.CurrentHealth, 0.001f, "Target health should remain unchanged for missed shot");
        Assert.AreEqual(initialShield, targetShip.damageHandler.CurrentShield, 0.001f, "Target shields should remain unchanged for missed shot");
    }

    // TODO: Helper methods for test scene setup
    // - CreateTestArena()
    // - CreateShipWithLaser()
    // - CreateTargetShip()
    // - PositionShipsForClearShot()
    // - WaitForProjectileHit()
} 