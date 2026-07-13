using System.Collections;
using Player;
using UnityEngine;

namespace Game.Sectors
{
    /// <summary>Mirrors the player's in/out occupancy of its trigger collider to a named bus signal.</summary>
    [RequireComponent(typeof(Collider))]
    public class TriggerVolume : SectorModule
    {
        [SerializeField] private string signalToken;

        private SectorEventBus bus;
        private bool playerInside;

        public void Configure(string token) => signalToken = token;

        public override IEnumerator Setup(SectorBuildContext ctx)
        {
            if (string.IsNullOrWhiteSpace(signalToken))
            {
                Debug.LogError($"TriggerVolume on '{name}' has a blank signal token — volume is inert.", this);
                yield break;
            }

            bus = ctx.Bus;
            // Trigger events can precede Setup (player parked in the volume at build) — push the buffered level.
            bus?.Set(signalToken, playerInside);
        }

        public override IEnumerator Teardown(SectorBuildContext ctx)
        {
            bus?.Set(signalToken, false);
            bus = null;
            yield break;
        }

        private void OnTriggerEnter(Collider other) => UpdateLevel(other, true);

        private void OnTriggerExit(Collider other) => UpdateLevel(other, false);

        private void UpdateLevel(Collider other, bool inside)
        {
            if (!other.GetComponentInParent<PlayerMarker>()) return;
            playerInside = inside;
            bus?.Set(signalToken, inside);
        }
    }
}
