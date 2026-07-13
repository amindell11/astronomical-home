using System.Collections;
using UnityEngine;

namespace Game.Sectors
{
    /// <summary>Mirrors the player's in/out occupancy of its trigger collider to a named bus signal.</summary>
    [RequireComponent(typeof(Collider))]
    public class TriggerVolume : SectorModule
    {
        [SerializeField] private string signalToken;

        private readonly RigidbodyOccupancy occupancy = new();
        private SectorEventBus bus;
        private Rigidbody playerBody;

        public void Configure(string token, Rigidbody player = null)
        {
            signalToken = token;
            if (player) playerBody = player;
        }

        public override IEnumerator Setup(SectorBuildContext ctx)
        {
            if (string.IsNullOrWhiteSpace(signalToken))
            {
                Debug.LogError($"TriggerVolume on '{name}' has a blank signal token — volume is inert.", this);
                yield break;
            }

            if (ctx.Player) playerBody = ctx.Player.Body;
            bus = ctx.Bus;
            // Trigger events can precede Setup (player parked in the volume at build) — push the buffered level.
            bus?.Set(signalToken, occupancy.Contains(playerBody));
        }

        public override IEnumerator Teardown(SectorBuildContext ctx)
        {
            bus?.Set(signalToken, false);
            bus = null;
            yield break;
        }

        private void OnTriggerEnter(Collider other)
        {
            occupancy.Enter(other.attachedRigidbody);
            Publish();
        }

        private void OnTriggerExit(Collider other)
        {
            occupancy.Exit(other.attachedRigidbody);
            Publish();
        }

        private void Publish() => bus?.Set(signalToken, occupancy.Contains(playerBody));
    }
}
