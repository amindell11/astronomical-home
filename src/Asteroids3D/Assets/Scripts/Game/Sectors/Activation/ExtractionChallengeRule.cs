using System.Collections;
using Objectives;
using Ships;
using UnityEngine;

namespace Game.Sectors
{
    /// <summary>Thin rule on the extraction gate fixture: on fire, binds the chaser as the zone's blocker and activates it.</summary>
    public class ExtractionChallengeRule : ActivationRule
    {
        [SerializeField] private ExtractionZone extractionZone;
        [SerializeField] private Ship chaser;

        private Rigidbody playerBody;

        public override IEnumerator Setup(SectorBuildContext ctx)
        {
            playerBody = ctx.Player ? ctx.Player.Body : null;
            return base.Setup(ctx);
        }

        public override IEnumerator Teardown(SectorBuildContext ctx)
        {
            if (chaser) chaser.gameObject.SetActive(false);
            return base.Teardown(ctx);
        }

        protected override void OnFired()
        {
            if (extractionZone) extractionZone.Initialize(playerBody, chaser ? chaser.transform : null);
            if (chaser) chaser.gameObject.SetActive(true);
        }

#if UNITY_EDITOR
        /// <summary>Test/editor seam mirroring the serialized references.</summary>
        internal void Bind(ExtractionZone zone, Ship chaserShip)
        {
            extractionZone = zone;
            chaser = chaserShip;
        }
#endif
    }
}
