using System.Collections;
using Objectives;
using Ships;
using UnityEngine;

namespace Game.Sectors
{
    public class ExtractionChallengeRule : ActivationRule
    {
        [SerializeField] private ExtractionZone extractionZone;
        [SerializeField] private Ship chaser;

        public override IEnumerator Setup(SectorBuildContext ctx)
        {
            if (!extractionZone || !chaser)
            {
                Debug.LogError($"ExtractionChallengeRule on '{name}' is missing a fixture reference — rule is inert.", this);
                yield break;
            }

            var setup = base.Setup(ctx);
            while (setup.MoveNext()) yield return setup.Current;
        }

        public override IEnumerator Teardown(SectorBuildContext ctx)
        {
            if (extractionZone) extractionZone.Disarm();
            if (chaser) chaser.gameObject.SetActive(false);
            return base.Teardown(ctx);
        }

        protected override void OnFired()
        {
            extractionZone.Arm(chaser.transform);
            chaser.gameObject.SetActive(true);
        }

#if UNITY_EDITOR
        internal void Bind(ExtractionZone zone, Ship chaserShip)
        {
            extractionZone = zone;
            chaser = chaserShip;
        }
#endif
    }
}
