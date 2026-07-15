using System.Collections;
using Objectives;
using UnityEngine;

namespace Game.Sectors
{
    public class ExtractionChallengeRule : ActivationRule
    {
        [SerializeField] private ExtractionZone extractionZone;

        public override IEnumerator Setup(SectorBuildContext ctx)
        {
            if (!extractionZone)
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
            return base.Teardown(ctx);
        }

        protected override void OnFired() => extractionZone.Arm();

#if UNITY_EDITOR
        internal void Bind(ExtractionZone zone) => extractionZone = zone;
#endif
    }
}
