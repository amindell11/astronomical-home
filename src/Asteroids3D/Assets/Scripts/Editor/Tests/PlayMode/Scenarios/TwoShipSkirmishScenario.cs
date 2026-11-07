#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using Substrate.Sectors;
using Substrate.Services.Locales;
using Substrate.Sessions;
using Tests.PlayMode.Common;
using Tests.PlayMode.Recording;
using UnityEditor;
using UnityEngine;

namespace Tests.PlayMode.Scenarios
{
    /// <summary>Committed sample scenario (runner + render smoke, and the living doc for authoring new ones): two policy-pilot ships skirmish for a few seconds inside TuningSector's asteroid field, filmed through the Game View with every native gizmo on.</summary>
    public sealed class TwoShipSkirmishScenario : CaptureScenario
    {
        private const float SimSeconds = 8f;
        private const string SectorPrefabPath = "Assets/Prefabs/Sectors/TuningSector.prefab";
        private const string LocalePath = "Assets/Prefabs/Locales/IllustratedBlue.prefab";

        public override SectorEntry SectorEntry
        {
            get
            {
                var prefab = AssetDatabase.LoadAssetAtPath<Sector>(SectorPrefabPath);
                Assert.IsNotNull(prefab, $"Sector prefab missing at {SectorPrefabPath}");
                var locale = AssetDatabase.LoadAssetAtPath<LocaleSky>(LocalePath);
                Assert.IsNotNull(locale, $"Sector locale missing at {LocalePath}");
                return new SectorEntry { prefab = prefab, locale = locale };
            }
        }

        public override IEnumerator Run()
        {
            // Inside the start point's rock-free clearing, so neither ship spawns into an asteroid.
            var (a, _) = SpawnCombatShip(new Vector2(-6f, 0f), rotDeg: -90f, team: 0);
            var (b, _) = SpawnCombatShip(new Vector2(6f, 0f), rotDeg: 90f, team: 1);
            yield return Film(a, b);

            var steps = Mathf.CeilToInt(SimSeconds / Time.fixedDeltaTime);
            for (var i = 0; i < steps && a && b; i++)
            {
                yield return new WaitForFixedUpdate();
            }
        }
    }
}
#endif
