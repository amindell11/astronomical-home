using System.Collections;
using UnityEngine;

namespace Game.Sectors
{
    /// <summary>Mirrors the player's in/out occupancy of its trigger collider to its inside port as a level.</summary>
    [RequireComponent(typeof(Collider))]
    public class TriggerVolume : SectorModule
    {
        [SerializeField] private SignalPort inside;

        private readonly RigidbodyOccupancy occupancy = new();
        private SectorEventBus bus;
        private Rigidbody playerBody;

        public SignalPort InsidePort => inside;

        public void Configure(SignalPort inside, Rigidbody player = null)
        {
            this.inside = inside;
            if (player) playerBody = player;
        }

        public override IEnumerator Setup(SectorBuildContext ctx)
        {
            if (!SignalPortGuards.ValidPortRef(this, inside, "inside", ctx.Sector)) yield break;
            if (!gameObject.activeInHierarchy)
            {
                Debug.LogError($"{GetType().Name} on '{name}' needs trigger messages but its GameObject is inactive — inert.", this);
                yield break;
            }

            if (ctx.Player) playerBody = ctx.Player.Body;
            bus = ctx.Bus;
            // Trigger events can precede Setup (player parked in the volume at build) — push the buffered level.
            bus?.Set(inside, occupancy.Contains(playerBody));
        }

        public override IEnumerator Teardown(SectorBuildContext ctx)
        {
            bus?.Set(inside, false);
            bus = null;
            yield break;
        }

        private void OnTriggerEnter(Collider other)
        {
            occupancy.Enter(other);
            Publish();
        }

        private void OnTriggerExit(Collider other)
        {
            occupancy.Exit(other);
            Publish();
        }

        // Occupancy pruning of dead colliders only happens on read — republish each physics step so the bus follows it.
        private void FixedUpdate() => Publish();

        private void Publish() => bus?.Set(inside, occupancy.Contains(playerBody));
    }
}
