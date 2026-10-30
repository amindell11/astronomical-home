using System;
using Damage;
using Ships.Damage;
using Substrate.Sectors.Elements.Phases;
using Substrate.Services.Objectives;
using UI.PlayerState;
using UnityEngine;

namespace UI.Projection
{
    public sealed class CanopyPersonality : MonoBehaviour
    {
        private IDamageEvents damage;
        private IObjectiveService objectives;
        private CanopyProjection projection;
        private Func<bool> isVisible;
        private PhaseSpineModule phaseSource;
        private int phaseIndex = -1;
        private string step;
        private float hitAt = float.NegativeInfinity;
        private float momentAt = float.NegativeInfinity;
        private float lowHullAt = float.NegativeInfinity;

        public static CanopyPersonality Create(GameObject owner, IObjectiveService objectives,
            CanopyProjection projection, Func<bool> isVisible)
        {
            var personality = owner.AddComponent<CanopyPersonality>();
            personality.objectives = objectives;
            personality.projection = projection;
            personality.isVisible = isVisible;
            personality.step = objectives.SpineStep;
            objectives.OnSpineStepChanged += personality.OnStepChanged;
            return personality;
        }

        public void Initialize(IDamageEvents source)
        {
            if (ReferenceEquals(damage, source)) return;
            if (damage != null) damage.OnDamaged -= OnDamaged;
            damage = source;
            if (damage != null) damage.OnDamaged += OnDamaged;
            hitAt = lowHullAt = float.NegativeInfinity;
        }

        public void EnterSector(PhaseSpineModule source)
        {
            if (ReferenceEquals(phaseSource, source)) return;
            phaseSource = source;
            phaseIndex = -1;
            momentAt = float.NegativeInfinity;
        }

        private bool CanDisplay => projection.EffectsEnabled && isVisible();

        public CanopyFrame Observe()
        {
            var now = Time.unscaledTime;
            if (phaseSource)
            {
                var phase = phaseSource.Phase;
                if (!phase.InLull && !phase.Done && phase.Index != phaseIndex)
                {
                    phaseIndex = phase.Index;
                    if (CanDisplay) momentAt = now;
                }
            }
            if (!CanDisplay)
            {
                hitAt = momentAt = lowHullAt = float.NegativeInfinity;
                return CanopyFrame.Idle;
            }
            var low = damage != null && damage.Health.Pct > 0f && damage.Health.Pct <= StatusBarUI.LowThreshold;
            if (!low) lowHullAt = float.NegativeInfinity;
            else if (float.IsNegativeInfinity(lowHullAt)) lowHullAt = now;
            var hitAge = now - hitAt;
            if (low) hitAge = Mathf.Min(hitAge, LowHullHitAge(now - lowHullAt));
            var transitionAge = now - momentAt;
            return new CanopyFrame(CanopyStateBlend.Select(low, transitionAge < projection.Profile.bigMomentSeconds),
                hitAge < .7f ? hitAge : -1f, transitionAge < .35f ? transitionAge : -1f);
        }

        internal static float LowHullHitAge(float elapsed)
        {
            var cycle = elapsed % 19f;
            var offset = cycle >= 17.9f ? 17.9f : cycle >= 13.27f ? 13.27f : cycle >= 8.64f ? 8.64f :
                cycle >= 7.1f ? 7.1f : cycle >= 3.78f ? 3.78f : cycle >= 1.23f ? 1.23f : 0f;
            return cycle - offset;
        }

        private void OnDamaged(AppliedDamage _) { if (CanDisplay) hitAt = Time.unscaledTime; }

        private void OnStepChanged(string next)
        {
            if (next != step && next == PhaseSpineModule.StepExtract && CanDisplay)
                momentAt = Time.unscaledTime;
            step = next;
        }

        private void OnDestroy()
        {
            if (damage != null) damage.OnDamaged -= OnDamaged;
            if (objectives != null) objectives.OnSpineStepChanged -= OnStepChanged;
        }
    }

    public readonly struct CanopyFrame
    {
        public static CanopyFrame Idle => new(CanopyState.Base, -1f, -1f);
        public readonly CanopyState State;
        public readonly float HitAge;
        public readonly float TransitionAge;

        public CanopyFrame(CanopyState state, float hitAge, float transitionAge)
        {
            State = state;
            HitAge = hitAge;
            TransitionAge = transitionAge;
        }
    }
}
