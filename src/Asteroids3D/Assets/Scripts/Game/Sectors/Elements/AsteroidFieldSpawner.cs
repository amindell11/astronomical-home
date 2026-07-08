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
            if (field is Asteroids.Fields.UpdatingAsteroidField updating)
            {
                // Anchor policy: this sector streams around the player when one is alive.
                // Unity lifetime check, NOT `?.` — the context can hold a destroyed ship
                // (player died before a rebuild), and the null-conditional would pass it
                // through to .transform and throw MissingReferenceException.
                updating.SetAnchor(ctx.Player ? ctx.Player.transform : null);
                // Static authored start — declared even in spectator/headless
                // runs so the layout is identical regardless of who is flying.
                if (ctx.Sector) updating.SetPlayerStart(ctx.Sector.PlayerStart);

                // Publish the field as the session's obstacle source so AI ships query
                // live asteroids directly (deterministic) instead of physics-scanning.
                AI.Scanning.ObstacleFields.Register(updating);
            }
            yield break;
        }

        public override IEnumerator Teardown(SectorBuildContext ctx)
        {
            if (field is AI.Scanning.IObstacleField of)
                AI.Scanning.ObstacleFields.Unregister(of);
            if (field) field.DespawnAll();
            yield break;
        }
    }
}
