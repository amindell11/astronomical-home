#if UNITY_EDITOR
using System.Collections;
using AI.Scanning;
using Asteroids.Fields;
using Game;
using NUnit.Framework;
using Tests.PlayMode.Common;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.PlayMode
{

/// <summary>
/// Verifies the deterministic-field obstacle query (B2): a live <see cref="UpdatingAsteroidField"/>
/// reports its spawned asteroids inside a fixed AABB, and a destroyed asteroid drops out of the
/// results on the next query.
/// </summary>
[Category("AI")]
public class ObstacleFieldQueryPlayModeTests : PlayModeWorldFixture
{
    private const string FieldPrefabPath = "Assets/Prefabs/Asteroid/AsteroidController.prefab";

    private GameObject fieldGo;
    private GameObject anchorGo;

    [TearDown]
    public override void TearDown()
    {
        // Destroying fieldGo also destroys the active asteroids (they parent under the spawner on
        // this GameObject). Wrapped so a cleanup hiccup can't skip base.TearDown().
        try
        {
            if (fieldGo) Object.DestroyImmediate(fieldGo);
            if (anchorGo) Object.DestroyImmediate(anchorGo);
        }
        finally
        {
            base.TearDown();
        }
    }

    [UnityTest]
    public IEnumerator QueryObstacles_ReturnsLiveAsteroids_AndDropsDestroyed()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FieldPrefabPath);
        Assert.IsNotNull(prefab, $"Field prefab not found at {FieldPrefabPath}");

        fieldGo = Object.Instantiate(prefab, Vector3.zero, Quaternion.identity);
        fieldGo.name = "ObstacleFieldQuery_Field";
        var field = fieldGo.GetComponent<AsteroidField>() as UpdatingAsteroidField;
        Assert.IsNotNull(field, "Field prefab is not an UpdatingAsteroidField");

        // Anchor the field's streaming at the origin. No player-start clearing is declared, so the
        // dense BigField packs asteroids right through the query centre.
        anchorGo = new GameObject("FieldAnchor");
        anchorGo.transform.position = Vector3.zero;
        field.SetAnchor(anchorGo.transform);

        // The initial fill is synchronous in the field's Start; give it a couple of fixed steps.
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();

        var buffer = new DetectedObstacle[256];
        var center = GamePlane.WorldPointToPlane(Vector3.zero);
        const float halfExtent = 40f;

        var firstCount = field.QueryObstacles(center, halfExtent, buffer);
        Assert.Greater(firstCount, 0, "Query should report live asteroids in the dense field");

        // Destroy one reported asteroid; the query must drop it (destroyed objects are never reported).
        var victim = buffer[0].collider;
        Assert.IsNotNull(victim, "Reported obstacle should carry the asteroid collider");
        Object.DestroyImmediate(victim.gameObject);

        var secondCount = field.QueryObstacles(center, halfExtent, buffer);
        Assert.Less(secondCount, firstCount, "Destroyed asteroid must not be reported by the next query");
    }

    private const string HarnessFieldPrefabPath = "Assets/Prefabs/Asteroid/HarnessAsteroidField.prefab";

    private UpdatingAsteroidField SpawnHarnessField()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HarnessFieldPrefabPath);
        Assert.IsNotNull(prefab, $"Field prefab not found at {HarnessFieldPrefabPath}");
        fieldGo = Object.Instantiate(prefab, Vector3.zero, Quaternion.identity);
        var field = fieldGo.GetComponent<UpdatingAsteroidField>();
        field.SetAnchor(null);
        return field;
    }

    private static int CountAll(UpdatingAsteroidField field)
    {
        var buffer = new DetectedObstacle[2048];
        var count = field.QueryObstacles(Vector2.zero, 200f, buffer);
        Assert.Less(count, buffer.Length, "Count buffer saturated");
        return count;
    }

    [UnityTest]
    public IEnumerator DensityScale_ScalesGeneratedCount_AndRadiusStaysBounded()
    {
        var field = SpawnHarnessField();
        field.SetDensityScale(0.5f);
        yield return new WaitForFixedUpdate();

        var sparseCount = CountAll(field);
        Assert.Greater(sparseCount, 0, "Half-density field should still generate asteroids");

        field.SetDensityScale(2f);
        field.RebuildField();
        var denseCount = CountAll(field);
        Assert.Greater(denseCount, 2 * sparseCount,
            "4x the density multiplier must produce a decisively denser field");
    }

    [UnityTest]
    public IEnumerator HostExclusionVolumes_CarveClearings_AtRebuild()
    {
        var field = SpawnHarnessField();
        yield return new WaitForFixedUpdate();

        var baselineCount = CountAll(field);
        Assert.Greater(baselineCount, 0);

        var clearingA = new Vector2(30f, 0f);
        var clearingB = new Vector2(-40f, 25f);
        const float radius = 25f;
        field.SetExclusionVolumes(new[]
        {
            new Asteroids.Fields.Core.ExclusionVolume { Center = clearingA, Radius = radius },
            new Asteroids.Fields.Core.ExclusionVolume { Center = clearingB, Radius = radius },
        });
        field.RebuildField();

        var buffer = new DetectedObstacle[2048];
        var count = field.QueryObstacles(Vector2.zero, 200f, buffer);
        Assert.Greater(count, 0);
        Assert.Less(count, baselineCount, "Carving two wide clearings must remove asteroids");
        // Homes are culled inside the circle; the sub-step of ambient drift since the rebuild motivates the small slack.
        for (var i = 0; i < count; i++)
        {
            Assert.Greater((buffer[i].position - clearingA).magnitude, radius - 0.5f,
                "No asteroid home may survive inside a host-passed exclusion volume");
            Assert.Greater((buffer[i].position - clearingB).magnitude, radius - 0.5f,
                "No asteroid home may survive inside a host-passed exclusion volume");
        }

        // Cleared volumes restore the untouched layout: the API is inert at its default.
        field.SetExclusionVolumes(null);
        field.RebuildField();
        Assert.AreEqual(baselineCount, CountAll(field),
            "Rebuilding with no host volumes must reproduce the unmodified layout");
    }
}

} // namespace Tests.PlayMode
#endif
