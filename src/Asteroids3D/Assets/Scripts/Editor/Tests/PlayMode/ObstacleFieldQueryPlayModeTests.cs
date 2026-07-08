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
        // this GameObject). Wrapped so a cleanup hiccup can't skip base.TearDown()'s GamePlane.Reset(),
        // which would otherwise cascade into the next fixture's SetUp ("already configured").
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
}

} // namespace Tests.PlayMode
#endif
