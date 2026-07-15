using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Sectors
{
    /// <summary>Mirrors the player's in/out occupancy of its trigger collider to its inside output as a level.</summary>
    [RequireComponent(typeof(Collider))]
    public class TriggerVolume : SectorModule, ISignalSource
    {
        public const string OutputInside = "inside";

        private readonly RigidbodyOccupancy occupancy = new();
        private SectorEventBus bus;
        private Rigidbody playerBody;

        public IEnumerable<SignalOutput> Outputs
        {
            get { yield return new SignalOutput(OutputInside, SignalKind.Level); }
        }

        public void Configure(Rigidbody player) => playerBody = player;

        public override IEnumerator Setup(SectorBuildContext ctx)
        {
            if (!gameObject.activeInHierarchy)
            {
                Debug.LogError($"{GetType().Name} on '{name}' needs trigger messages but its GameObject is inactive — inert.", this);
                yield break;
            }

            if (ctx.Player) playerBody = ctx.Player.Body;
            bus = ctx.Bus;
            // Trigger events can precede Setup (player parked in the volume at build) — push the buffered level.
            Publish();
        }

        public override IEnumerator Teardown(SectorBuildContext ctx)
        {
            bus?.Set(new SignalRef(this, OutputInside), false);
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

        private void Publish() => bus?.Set(new SignalRef(this, OutputInside), occupancy.Contains(playerBody));
    }
}
