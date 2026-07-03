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
    /// Spawns a single ship at this spawner's transform pose. For one-off procedural content
    /// where no hand-placed instance exists (use adopt content when the instance should be the
    /// authored object itself).
    /// </summary>
    public class SingleSpawner : SectorSpawner
    {
        [SerializeField] private Ship template;
        [SerializeField] private Commander commander;
        [SerializeField] private int team;
        [SerializeField] private bool startActive = true;

        public override IEnumerator Build(SectorBuildContext ctx)
        {
            var spawned = new List<Ship>();
            Spawned = spawned;

            if (!template)
                yield break;

            var ship = ctx.Services.UnitService.SpawnShip(
                template, commander, team,
                transform.position, transform.rotation == Quaternion.identity ? GamePlane.Rotation : transform.rotation);

            if (ship)
            {
                spawned.Add(ship);
                if (!startActive) ship.gameObject.SetActive(false);
            }
        }

        protected override void OnDrawGizmos()
        {
#if UNITY_EDITOR
            var markerRadius = template ? template.transform.localScale.x : 1f;
            Gizmos.color = new Color(0.2f, 0.7f, 1f, 0.9f);
            Gizmos.DrawWireSphere(transform.position, markerRadius);
            Gizmos.DrawLine(transform.position, transform.position + transform.forward * markerRadius * 3f);
            Handles.Label(transform.position + transform.up * 2f, $"{name} (team {team})");
#endif
        }
    }
}
