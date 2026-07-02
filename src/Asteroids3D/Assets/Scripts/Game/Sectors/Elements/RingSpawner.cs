#if UNITY_EDITOR
using UnityEditor;
#endif
using System.Collections;
using System.Collections.Generic;
using Ships;
using Ships.Command;
using UnityEngine;

namespace Game.Sectors
{
    /// <summary>
    /// Spawns a parametric ring of ships centred on this spawner's transform. Mirrors the ring
    /// math used by the old <c>ArenaSector.SpawnTeam</c> but uses a full 2π circle (no side-sign),
    /// so the spawner is self-contained: author one per team and place it where the team should
    /// gather.
    /// </summary>
    public class RingSpawner : SectorSpawner
    {
        [SerializeField] private Ship template;
        [SerializeField] private Commander commander;
        [SerializeField] private ShipSettings settings;
        [SerializeField] private int team;
        [SerializeField] private int count = 4;
        [SerializeField] private float radius = 30f;

        [Tooltip("Optional respawn rule applied to every ship this spawner produces (origin None = no respawn).")]
        [SerializeField] private RespawnPolicy respawn;

        /// <summary>Configures the ring's template and layout at runtime (spawner setup / tuning).</summary>
        public void Configure(Ship template, Commander commander, ShipSettings settings,
                              int count, float radius = 30f, int team = 0)
        {
            this.template = template;
            this.commander = commander;
            this.settings = settings;
            this.count = count;
            this.radius = radius;
            this.team = team;
        }

        public override IEnumerator Build(SectorBuildContext ctx)
        {
            var spawned = new List<Ship>();
            Spawned = spawned;

            if (!template || count <= 0)
                yield break;

            var center = GamePlane.WorldPointToPlane(transform.position);
            for (var i = 0; i < count; i++)
            {
                var angle = 2f * Mathf.PI * (i + 0.5f) / count;
                var offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                var ship = ctx.Services.UnitService.SpawnShip(
                    template, commander, settings, team,
                    GamePlane.PlanePointToWorld(center + offset), GamePlane.Rotation);
                if (ship) spawned.Add(ship);
            }

            // Producer-owned respawn: each product is wired here (additive — default policy is a no-op).
            foreach (var ship in spawned)
                Respawn.Wire(ship, respawn, ctx.Services);
        }

        protected override void OnDrawGizmos()
        {
#if UNITY_EDITOR
            Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.5f);
            var segments = Mathf.Max(count * 4, 32);
            var prev = transform.position + transform.right * radius;
            for (var i = 1; i <= segments; i++)
            {
                var a = 2f * Mathf.PI * i / segments;
                var next = transform.position
                           + transform.right * (Mathf.Cos(a) * radius)
                           + transform.forward * (Mathf.Sin(a) * radius);
                Gizmos.DrawLine(prev, next);
                prev = next;
            }

            var markerRadius = settings ? settings.shipRadius : 1f;
            Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.9f);
            for (var i = 0; i < count; i++)
            {
                var a = 2f * Mathf.PI * (i + 0.5f) / count;
                var pt = transform.position
                         + transform.right * (Mathf.Cos(a) * radius)
                         + transform.forward * (Mathf.Sin(a) * radius);
                Gizmos.DrawWireSphere(pt, markerRadius);
            }

            Handles.Label(transform.position + transform.up * 2f, $"{name} ×{count} (team {team})");
#endif
        }
    }
}
