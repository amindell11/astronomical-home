using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// PlayMode tests that exercise the asteroid spawning pipeline – initial population,
/// run-time fragmentation, and volume-based density control. These tests rely on the
/// <c>AsteroidController</c> prefab which bundles an <see cref="AsteroidSpawner"/>,
/// <see cref="AsteroidFieldManager"/> (open-world variant) and <see cref="AsteroidFragnetics"/>.
///
/// The goal is to surface regression bugs in the interplay between these systems so
/// that crashes or silent failures in production can be reproduced and diagnosed in CI.
/// </summary>
public class AsteroidSpawningPlayMode
{
    private GameObject testScene;
    private AsteroidSpawner spawner;
    private AsteroidFieldManager fieldManager;

    // We spawn our own camera so that AsteroidFieldManager has a valid anchor.
    private Camera mainCamera;

    [SetUp]
    public void SetUp()
    {
        // Create a basic scene root so everything sits under a single parent for tidier cleanup.
        testScene = new GameObject("AsteroidSpawningTestScene");

        // ------------------------------------------------------------
        // 1. Ensure a Main Camera exists – required by AsteroidFieldManager
        // ------------------------------------------------------------
        mainCamera = new GameObject("MainCamera").AddComponent<Camera>();
        mainCamera.tag = "MainCamera"; // Enables Camera.main lookup
        mainCamera.transform.SetParent(testScene.transform);
        mainCamera.transform.position = Vector3.zero;
        mainCamera.transform.rotation = Quaternion.Euler(90f, 0f, 0f); // Top-down so +Y is forward in project convention

        // ------------------------------------------------------------
        // 2. Instantiate the controller prefab which contains spawner, fragmenter & field manager
        // ------------------------------------------------------------
#if UNITY_EDITOR
        const string prefabPath = "Assets/Prefabs/AsteroidController.prefab";
        GameObject controllerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        Assert.IsNotNull(controllerPrefab, $"Failed to load prefab at {prefabPath}. Ensure the asset exists and path is correct.");
        GameObject controllerInstance = Object.Instantiate(controllerPrefab, testScene.transform);
#else
        Assert.Fail("AsteroidSpawningPlayMode requires AssetDatabase which is only available in the editor.");
#endif
        // Grab references to the baked-in components for convenience
        spawner = Object.FindObjectOfType<AsteroidSpawner>();
        fieldManager = Object.FindObjectOfType<AsteroidFieldManager>();

        Assert.IsNotNull(spawner, "AsteroidSpawner component not found after prefab instantiation.");
        Assert.IsNotNull(fieldManager, "AsteroidFieldManager component not found after prefab instantiation.");
    }

    [TearDown]
    public void TearDown()
    {
        if (testScene)
        {
            Object.DestroyImmediate(testScene);
        }
        spawner = null;
        fieldManager = null;
        mainCamera = null;
    }

    // --------------------------------------------------------------------------------------------
    // Test 1 – Initial spawn behaviour
    // --------------------------------------------------------------------------------------------
    [UnityTest]
    public IEnumerator InitialSpawn_CreatesAsteroids_WithinConfiguredRadius()
    {
        // Wait one frame so that Awake/Start logic in the field manager executes.
        yield return null;

        // After Start(), the field manager should have populated the field.
        Assert.Greater(spawner.ActiveAsteroidCount, 0, "Field manager failed to spawn any asteroids on Start().");

        // Validate that all asteroids are within the configured annulus (minSpawnDistance .. maxSpawnDistance)
        float minSpawn = GetPrivateField<float>(typeof(BaseFieldManager), fieldManager, "minSpawnDistance");
        float maxSpawn = GetPrivateField<float>(typeof(BaseFieldManager), fieldManager, "maxSpawnDistance");
        Transform anchor = fieldManager.transform; // AsteroidFieldManager anchors to camera, but positions are planar so using its own transform is fine.

        foreach (Asteroid a in Object.FindObjectsOfType<Asteroid>())
        {
            float dist = Vector3.Distance(GamePlane.ProjectOntoPlane(a.transform.position), GamePlane.ProjectOntoPlane(anchor.position));
            Assert.IsTrue(dist >= minSpawn - 0.1f && dist <= maxSpawn + 0.1f, $"Asteroid {a.name} spawned at {dist:F2}, outside configured range ({minSpawn}..{maxSpawn}).");
        }
    }

    // --------------------------------------------------------------------------------------------
    // Test 2 – Fragmentation spawns child asteroids via the spawner
    // --------------------------------------------------------------------------------------------
    [UnityTest]
    public IEnumerator Fragmentation_DestroyingAsteroid_CreatesFragments()
    {
        // Ensure at least one asteroid exists
        yield return null;
        Asteroid target = Object.FindObjectOfType<Asteroid>();
        Assert.IsNotNull(target, "No asteroid available to test fragmentation.");

        int countBefore = spawner.ActiveAsteroidCount;

        // Apply lethal damage – more than the asteroid's current health so that it fractures.
        float overkill = target.MaxHealth * 2f;
        target.TakeDamage(overkill, 10f, Vector3.forward * 5f, target.transform.position, null);

        // Wait a few frames so the fragmentation coroutine runs and placeholder fragments spawn
        yield return null; // first frame – placeholder spawn
        yield return null; // second frame – physics correction pass

        int countAfter = spawner.ActiveAsteroidCount;
        Assert.Greater(countAfter, countBefore, "Fragmentation did not increase active asteroid count as expected.");
    }

    // --------------------------------------------------------------------------------------------
    // Test 3 – Density control: when density below target, additional asteroids spawn on update
    // --------------------------------------------------------------------------------------------
    [UnityTest]
    public IEnumerator DensityControl_BelowTarget_SpawnsAdditionalAsteroids()
    {
        // Lower the target density so that after we clear the field the manager must spawn new asteroids.
        fieldManager.TargetDensity = 0.05f;

        // 1. Clear the field entirely.
        spawner.ReleaseAllAsteroids();
        Assert.AreEqual(0, spawner.ActiveAsteroidCount, "ReleaseAllAsteroids() did not clear active set.");

        // 2. Wait long enough for at least one density check tick (AsteroidFieldManager uses densityCheckInterval).
        float checkInterval = GetPrivateField<float>(typeof(AsteroidFieldManager), fieldManager, "densityCheckInterval");
        yield return new WaitForSeconds(checkInterval * 1.2f);

        // 3. Verify that some asteroids have been respawned.
        Assert.Greater(spawner.ActiveAsteroidCount, 0, "Density control logic failed to respawn asteroids when below target density.");
    }

    // --------------------------------------------------------------------------------------------
    // Test 4 – End-to-end volume accounting through fragmentation & culling
    [UnityTest]
    public IEnumerator TotalVolumeAccounting_FragmentThenCull_MatchesSceneSum()
    {
        // Ensure initial population is ready
        yield return null;

        // Helper local function to sum volumes of ALL live, active asteroids in scene
        float SceneVolume()
        {
            float v = 0f;
            foreach (Asteroid a in Object.FindObjectsOfType<Asteroid>())
            {
                if (a != null && a.gameObject.activeInHierarchy)
                    v += a.CurrentVolume;
            }
            return v;
        }

        // ---------- Phase A: baseline check ----------
        float baselineSpawnerVol = spawner.TotalActiveVolume;
        float baselineSceneVol   = SceneVolume();
        Assert.AreEqual(baselineSceneVol, baselineSpawnerVol, baselineSceneVol * 0.05f + 0.01f,
            "Spawner TotalActiveVolume mismatch at baseline.");

        // ---------- Phase B: trigger fragmentation ----------
        Asteroid target = Object.FindObjectOfType<Asteroid>();
        Assert.IsNotNull(target, "No asteroid to fragment in volume accounting test.");
        target.TakeDamage(target.MaxHealth * 2f, 5f, Vector3.left * 10f, target.transform.position, null);

        // Allow coroutine + physics to spawn fragments and update accounting
        yield return null; // placeholder spawn
        yield return null; // physics correction

        float afterFragSpawnerVol = spawner.TotalActiveVolume;
        float afterFragSceneVol   = SceneVolume();
        Assert.AreEqual(afterFragSceneVol, afterFragSpawnerVol, afterFragSceneVol * 0.05f + 0.01f,
            "TotalActiveVolume mismatch after fragmentation.");

        // ---------- Phase C: cull / manually release all asteroids ----------
        spawner.ReleaseAllAsteroids();
        yield return null;

        float afterCullSpawnerVol = spawner.TotalActiveVolume;
        float afterCullSceneVol   = SceneVolume();
        Assert.AreEqual(0f, afterCullSpawnerVol, 0.001f, "TotalActiveVolume should be zero after releasing all asteroids.");
        Assert.AreEqual(0f, afterCullSceneVol, 0.001f, "Scene should contain no active asteroids after releasing all.");

        // ---------- Phase D: wait for density controller to repopulate ----------
        float checkInterval = GetPrivateField<float>(typeof(AsteroidFieldManager), fieldManager, "densityCheckInterval");
        yield return new WaitForSeconds(checkInterval * 1.5f);

        float finalSpawnerVol = spawner.TotalActiveVolume;
        float finalSceneVol   = SceneVolume();
        Assert.Greater(finalSpawnerVol, 0f, "Field manager failed to spawn asteroids after culling.");
        Assert.AreEqual(finalSceneVol, finalSpawnerVol, finalSceneVol * 0.05f + 0.01f,
            "TotalActiveVolume mismatch after density-driven respawn.");
    }

    // --------------------------------------------------------------------------------------------
    // Utility – reflection helper for accessing private serialized fields without duplicating test-only subclasses
    // --------------------------------------------------------------------------------------------
    private static T GetPrivateField<T>(System.Type type, object instance, string fieldName)
    {
        var field = type.GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.IsNotNull(field, $"Field '{fieldName}' not found via reflection on {type}.");
        return (T)field.GetValue(instance);
    }

    /// <summary>
    /// Cross-checks internal book-keeping of <see cref="AsteroidSpawner"/> against the actual
    /// scene state.  Also verifies that every entry in the private <c>activeAsteroids</c>
    /// set maps to a live, enabled <see cref="Asteroid"/> component and that summed
    /// volumes match <c>TotalActiveVolume</c>.
    /// </summary>
    private void ValidateSpawnerIntegrity(float relTol = 0.02f, float absTol = 0.02f)
    {
        var registry = AsteroidRegistry.Instance;
        Assert.IsNotNull(registry, "AsteroidRegistry instance not present.");

        // 1. Count consistency
        Assert.AreEqual(spawner.ActiveAsteroidCount, registry.ActiveCount, "ActiveAsteroidCount does not match registry count.");

        // 2. Every entry valid & enabled, gather volume
        float sum = 0f;
        foreach (Asteroid a in registry.ActiveAsteroids)
        {
            Assert.IsNotNull(a, "Null Asteroid reference in registry.");
            Assert.IsTrue(a.gameObject.activeInHierarchy, $"Asteroid {a.name} registered but inactive in hierarchy.");
            sum += a.CurrentVolume;
        }

        // 3. Compare summed volume vs spawner counter
        float allowed = Mathf.Max(absTol, sum * relTol);
        Assert.AreEqual(sum, spawner.TotalActiveVolume, allowed,
            $"TotalActiveVolume mismatch: summed={sum:F3} tracker={spawner.TotalActiveVolume:F3}");

        // 4. Scene cross-check – ensure no stray asteroids excluded from registry
        var sceneAsteroids = Object.FindObjectsOfType<Asteroid>();
        Assert.AreEqual(sceneAsteroids.Length, registry.ActiveCount, "Number of Asteroid components in scene differs from registry active set.");
    }

    // --------------------------------------------------------------------------------------------
    // Test 5 – Rigorous fragmentation volume conservation and integrity checks
    // --------------------------------------------------------------------------------------------
    [UnityTest]
    public IEnumerator FragmentationVolumeConservation_IntegrityChecks()
    {
        // Ensure population & initial integrity
        yield return null;
        ValidateSpawnerIntegrity();

        // Grab a sizeable asteroid (choose largest by volume) to fragment
        Asteroid[] asts = Object.FindObjectsOfType<Asteroid>();
        Assert.IsNotEmpty(asts);
        Asteroid target = asts.OrderByDescending(a => a.CurrentVolume).First();

        float parentVol = target.CurrentVolume;
        float spawnerVolBefore = spawner.TotalActiveVolume;

        // Fragment the asteroid hard
        target.TakeDamage(target.MaxHealth * 2f, 10f, Vector3.right * 15f, target.transform.position, null);
        // Wait enough frames for fragmentation pipeline to finish (placeholder + correction)
        yield return null;
        yield return null;
        yield return null;
        yield return null;
        yield return null;

        // Integrity after fragmentation
        ValidateSpawnerIntegrity();

        float spawnerVolAfter = spawner.TotalActiveVolume;
        // Extract massLossFactor from AsteroidFragnetics
        float mLoss = GetPrivateField<float>(typeof(AsteroidFragnetics), AsteroidFragnetics.Instance, "massLossFactor");
        // Expected new spawner volume = original total - parentVol + parentVol * mLoss
        float expected = spawnerVolBefore - parentVol + parentVol * mLoss;
        float relTol = 0.05f;
        Assert.AreEqual(expected, spawnerVolAfter, Mathf.Max(0.05f, expected * relTol),
            "Post-fragmentation TotalActiveVolume deviates from expectation based on massLossFactor.");
    }

    // --------------------------------------------------------------------------------------------
    // Test 6 – Multiple cycles of fragment & respawn maintain consistent state
    // --------------------------------------------------------------------------------------------
    [UnityTest]
    public IEnumerator RepeatedCycles_MaintainIntegrity()
    {
        yield return null;
        const int cycles = 3;
        for (int i = 0; i < cycles; i++)
        {
            ValidateSpawnerIntegrity();

            // --- Fragment a random asteroid if any exist
            var ast = Object.FindObjectsOfType<Asteroid>().FirstOrDefault();
            if (ast != null)
            {
                ast.TakeDamage(ast.MaxHealth * 2f, 5f, Vector3.up * 8f, ast.transform.position, null);
                yield return null; yield return null; // allow frag pipeline
            }

            ValidateSpawnerIntegrity();

            // --- Cull everything
            spawner.ReleaseAllAsteroids();
            yield return null;
            ValidateSpawnerIntegrity(); // should be empty (vol=0)

            // --- Wait for density respawn and validate again
            float checkInterval = GetPrivateField<float>(typeof(AsteroidFieldManager), fieldManager, "densityCheckInterval");
            yield return new WaitForSeconds(checkInterval * 1.5f);
            ValidateSpawnerIntegrity();
        }
    }

    // --------------------------------------------------------------------------------------------
    // Test 7 – Parent asteroid must be removed from spawner set after fragmentation
    // --------------------------------------------------------------------------------------------
    [UnityTest]
    public IEnumerator ParentRemoval_AfterFragmentation()
    {
        yield return null;
        ValidateSpawnerIntegrity();

        // Pick a target
        Asteroid parent = Object.FindObjectsOfType<Asteroid>().First();
        Assert.IsNotNull(parent);
        GameObject parentGO = parent.gameObject;

        // Fragment it
        parent.TakeDamage(parent.MaxHealth * 2f, 5f, Vector3.back * 5f, parent.transform.position, null);
        
        // Wait up to half a second for cleanup (fragment coroutine & callback)
        float timeout = 0.5f;
        float start = Time.time;
        while (Time.time - start < timeout && parentGO != null && parentGO.activeInHierarchy)
        {
            yield return null;
        }

        // Parent should now be inactive or destroyed, and NOT present in the registry
        var registry = AsteroidRegistry.Instance;
        Assert.IsNotNull(registry, "AsteroidRegistry instance not present.");
        Assert.IsFalse(registry.ActiveAsteroids.Contains(parent), "Parent asteroid still present in registry after fragmentation cleanup window.");
    }

    // --------------------------------------------------------------------------------------------
    // Test 8 – Run for 5 seconds real-time, fragmenting random asteroids, ensure no net volume drift
    // --------------------------------------------------------------------------------------------
    [UnityTest]
    public IEnumerator VolumeDrift_LongRun()
    {
        // Stabilise initial state
        yield return null;
        ValidateSpawnerIntegrity();

        float startVol = spawner.TotalActiveVolume;
        float startTime = Time.time;
        float duration = 5f;
        System.Random rnd = new System.Random();
        
        // Track volume changes
        float lastCheckVol = startVol;
        float checkInterval = 0.5f;
        float lastCheckTime = startTime;

        while (Time.time - startTime < duration)
        {
            // Periodically validate integrity and log volume changes
            if (Time.time - lastCheckTime > checkInterval)
            {
                float currentVol = spawner.TotalActiveVolume;
                float deltaVol = currentVol - lastCheckVol;
                if (Mathf.Abs(deltaVol) > 0.1f)
                {
                    Debug.Log($"[VolumeDrift] Time: {Time.time - startTime:F2}s | Volume: {currentVol:F2} | Delta: {deltaVol:+0.00;-0.00} | Asteroids: {spawner.ActiveAsteroidCount}");
                }
                ValidateSpawnerIntegrity();
                lastCheckVol = currentVol;
                lastCheckTime = Time.time;
            }
            
            // Occasionally fragment a random asteroid
            if (rnd.NextDouble() < 0.2)
            {
                var astArray = Object.FindObjectsOfType<Asteroid>();
                if (astArray.Length > 0)
                {
                    Asteroid pick = astArray[rnd.Next(astArray.Length)];
                    Debug.Log($"[VolumeDrift] Fragmenting asteroid with volume {pick.CurrentVolume:F2}");
                    pick.TakeDamage(pick.MaxHealth * 2f, 5f, Vector3.right * 3f, pick.transform.position, null);
                }
            }

            // Occasionally clear field to trigger respawn
            if (rnd.NextDouble() < 0.05)
            {
                Debug.Log($"[VolumeDrift] Clearing field, current volume: {spawner.TotalActiveVolume:F2}");
                spawner.ReleaseAllAsteroids();
            }

            yield return null;
        }

        ValidateSpawnerIntegrity();
        float endVol = spawner.TotalActiveVolume;
        float drift = Mathf.Abs(endVol - startVol);
        Assert.Less(drift, startVol * 0.3f + 1f, $"Net volume drifted by {drift:F2} over long-run stress test (start {startVol:F2} → end {endVol:F2}).");
    }
} 