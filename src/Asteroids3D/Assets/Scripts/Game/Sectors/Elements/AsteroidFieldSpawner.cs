using System.Collections;
using UnityEngine;

namespace Game.Sectors
{
    /// <summary>
    /// Adapter that bridges the sector spawner lifecycle to an <see cref="Asteroids.Fields.AsteroidField"/>
    /// sibling component. The field cannot itself derive <see cref="SectorSpawner"/> (it already derives
    /// <see cref="Asteroids.Fields.AsteroidField"/> : MonoBehaviour), so this component sits alongside it
    /// on the same hand-placed field element and forwards Build/Teardown.
    /// <para>
    /// Ordering hazard: the field element is a hand-placed child of the sector, and the sector is
    /// instantiated under an inactive holder during <c>Setup</c>, so the field's own <c>Awake</c> has not
    /// run yet when <see cref="Build"/> executes. <see cref="Build"/> therefore only stashes the player
    /// anchor (safe pre-Awake); the world-anchor / culling-boundary wiring happens in the field's own
    /// <c>Start</c>, which runs after its <c>Awake</c>.
    /// </para>
    /// </summary>
    public class AsteroidFieldSpawner : SectorSpawner
    {
        [SerializeField] private Asteroids.Fields.AsteroidField field;

        public override IEnumerator Build(SectorBuildContext ctx)
        {
            if (!field) field = GetComponent<Asteroids.Fields.AsteroidField>();
            (field as Asteroids.Fields.UpdatingAsteroidField)?.SetPlayer(ctx.Player?.transform);
            yield break;
        }

        public override IEnumerator Teardown(SectorBuildContext ctx)
        {
            if (field) field.DespawnAll();
            yield break;
        }
    }
}
