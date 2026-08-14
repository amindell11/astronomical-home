#if UNITY_EDITOR
using System.Collections;
using Tests.PlayMode.Common;
using UnityEngine;

namespace Tests.PlayMode.Scenarios
{
    /// <summary>Committed sample scenario (runner + render smoke, and the living doc for authoring new ones): two policy-pilot ships skirmish for a few seconds, filmed through the Game View with every native gizmo on.</summary>
    public sealed class TwoShipSkirmishScenario : CaptureScenario
    {
        private const float SimSeconds = 8f;

        public override IEnumerator Run()
        {
            var (a, _) = SpawnCombatShip(new Vector2(-12f, 0f), rotDeg: -90f, team: 0);
            var (b, _) = SpawnCombatShip(new Vector2(12f, 0f), rotDeg: 90f, team: 1);
            Film(a, b);

            var steps = Mathf.CeilToInt(SimSeconds / Time.fixedDeltaTime);
            for (var i = 0; i < steps && a && b; i++)
            {
                yield return new WaitForFixedUpdate();
                FilmStep();
            }
        }
    }
}
#endif
