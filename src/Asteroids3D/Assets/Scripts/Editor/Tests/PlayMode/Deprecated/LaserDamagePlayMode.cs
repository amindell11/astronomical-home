using System.Collections;
using NUnit.Framework;
using Ships;
using UnityEngine;
using UnityEngine.TestTools;
using Weapons;

/// <summary>
/// PlayMode test for laser damage validation.
/// Test Case: Target health reduction matches projectile damage.
/// </summary>
public class LaserDamagePlayMode
{
    /*
    private TestServices services;
    private LaserGun laserGun;

    [SetUp]
    public void SetUp()
    {
        var config = TestConfig.Load();
        Assert.IsNotNull(config, "TestConfig.asset not found at Assets/Tests/PlayMode/TestConfig.asset");

        services = config.CreateServices(separation: 10f);
        laserGun = services.Player?.GetComponentInChildren<LaserGun>();

        TestSceneBuilder.PositionForTest(services.Player.transform, services.Enemy.transform, 10f, 0f);
    }

    [TearDown]
    public void TearDown()
    {
        services?.Dispose();
        services = null;
        laserGun = null;
    }

    // TODO: DEPRECATED - Reimplement as needed after GamePlane refactor
    // [UnityTest]
    // public IEnumerator LaserDamage_ReducesTargetHealth_ByProjectileDamage()
    // {
    //     Assert.NotNull(services.Player, "Player ship was not created");
    //     Assert.NotNull(services.Enemy, "Enemy ship was not created");

    //     // Disable target shields for direct health damage testing
    //     var noShieldSettings = ScriptableObject.CreateInstance<Settings>();
    //     noShieldSettings.maxHealth = services.Enemy.settings.maxHealth;
    //     noShieldSettings.maxShield = 0f;
    //     noShieldSettings.shieldRegenDelay = 999f;
    //     services.Enemy.Damage.PopulateSettings(noShieldSettings);
    //     services.Enemy.Damage.ResetDamageState();

    //     var initialHealth = services.Enemy.Damage.Health.CurrentValue;
    //     var projectileDamage = GetProjectileDamage();

    //     ApplyDamage(services.Enemy, projectileDamage, services.Player.gameObject);
    //     yield return null;

    //     var expectedHealth = initialHealth - projectileDamage;
    //     Assert.AreEqual(expectedHealth, services.Enemy.Damage.Health.CurrentValue, 0.001f,
    //         "Target health did not decrease by expected amount");
    //     Assert.AreEqual(0f, services.Enemy.Damage.Shield.CurrentValue, 0.001f,
    //         "Target shields should be zero for this test");
    // }

    // TODO: DEPRECATED - Reimplement as needed after GamePlane refactor
    // [UnityTest]
    // public IEnumerator LaserDamage_MultipleLasers_AccumulatesDamage()
    // {
    //     Assert.NotNull(services.Player);
    //     Assert.NotNull(services.Enemy);
    //     Assert.NotNull(laserGun);

    //     var noShieldSettings = ScriptableObject.CreateInstance<Settings>();
    //     noShieldSettings.maxHealth = services.Enemy.settings.maxHealth;
    //     noShieldSettings.maxShield = 0f;
    //     services.Enemy.Damage.PopulateSettings(noShieldSettings);
    //     services.Enemy.Damage.ResetDamageState();

    //     var initialHealth = services.Enemy.Damage.Health.CurrentValue;
    //     var projectileDamage = GetProjectileDamage();

    //     const int shots = 3;
    //     for (var i = 0; i < shots; i++)
    //     {
    //         ApplyDamage(services.Enemy, projectileDamage, services.Player.gameObject);
    //         yield return null;
    //     }

    //     var expectedHealth = initialHealth - projectileDamage * shots;
    //     Assert.AreEqual(expectedHealth, services.Enemy.Damage.Health.CurrentValue, 0.001f,
    //         "Cumulative damage does not match expected after multiple hits");
    // }

    // TODO: DEPRECATED - Reimplement as needed after GamePlane refactor
    // [UnityTest]
    // public IEnumerator LaserDamage_MissedShot_NoHealthReduction()
    // {
    //     Assert.NotNull(services.Player);
    //     Assert.NotNull(services.Enemy);
    //     Assert.NotNull(laserGun);

    //     var initialHealth = services.Enemy.Damage.Health.CurrentValue;
    //     var initialShield = services.Enemy.Damage.Shield.CurrentValue;

    //     // Position target 90° to the side so a forward shot misses
    //     TestSceneBuilder.PositionForTest(services.Player.transform, services.Enemy.transform, 10f, 90f);

    //     laserGun.Fire();
    //     yield return new WaitForSeconds(0.25f);

    //     Assert.AreEqual(initialHealth, services.Enemy.Damage.Health.CurrentValue, 0.001f,
    //         "Target health should remain unchanged for missed shot");
    //     Assert.AreEqual(initialShield, services.Enemy.Damage.Shield.CurrentValue, 0.001f,
    //         "Target shields should remain unchanged for missed shot");
    // }
    //TODO reflection to get projectile damage
    private float GetProjectileDamage()
    {
        if (!laserGun) return -1f;

        var field = typeof(LauncherBase<LaserProjectile>).GetField(
            "projectilePrefab",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (field?.GetValue(laserGun) is LaserProjectile prefab)
            return prefab.Damage;

        return 10f;
    }

    private static void ApplyDamage(Ship target, float damage, GameObject source)
    {
        target.Damage?.TakeDamage(damage, 1f, Vector3.zero, target.transform.position, source);
    }*/
}
