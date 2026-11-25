using System;
using System.Collections.Generic;
using Cameras;
using Cameras.Shake;
using Damage;
using Ships.Damage;
using Substrate.Sectors.Elements.Phases;
using Substrate.Services.Objectives;
using UI.PlayerState;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UI.Projection
{
    public sealed class CanopyPersonality : MonoBehaviour
    {
        private IDamageEvents damage;
        private ShakeChannel shake;
        private readonly Dictionary<Object, CameraShakeProfile> raised = new();
        private IObjectiveService objectives;
        private CanopyProjection projection;
        private Func<bool> isVisible;
        private PhaseSpineModule phaseSource;
        private int phaseIndex = -1;
        private string step;
        private float hitAt = float.NegativeInfinity;
        private float hitSize;
        private float momentAt = float.NegativeInfinity;
        private float lowHullAt = float.NegativeInfinity;
        private float glitchAt = float.NegativeInfinity;
        private float glitchStrength;

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

        public void Initialize(IDamageEvents source, ShakeChannel shake)
        {
            BindShake(shake);
            if (ReferenceEquals(damage, source)) return;
            if (damage != null) damage.OnDamaged -= OnDamaged;
            damage = source;
            if (damage != null) damage.OnDamaged += OnDamaged;
            hitAt = lowHullAt = glitchAt = float.NegativeInfinity;
            hitSize = glitchStrength = 0f;
        }

        private void BindShake(ShakeChannel next)
        {
            if (ReferenceEquals(shake, next)) return;
            if (shake != null)
            {
                shake.Raised -= OnShakeRaised;
                shake.Released -= OnShakeReleased;
            }
            shake = next;
            shake.Raised += OnShakeRaised;
            shake.Released += OnShakeReleased;
            raised.Clear();
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
                hitAt = momentAt = lowHullAt = glitchAt = float.NegativeInfinity;
                hitSize = glitchStrength = 0f;
                return CanopyFrame.Idle;
            }
            var low = damage != null && damage.Health.Pct > 0f && damage.Health.Pct <= StatusBarUI.LowThreshold;
            if (!low) lowHullAt = float.NegativeInfinity;
            else if (float.IsNegativeInfinity(lowHullAt)) lowHullAt = now;
            var hitAge = now - hitAt;
            var pulse = low ? LowHullHitAge(now - lowHullAt) : float.PositiveInfinity;
            var size = pulse < hitAge ? 1f : hitSize;
            hitAge = Mathf.Min(hitAge, pulse);
            var transitionAge = now - momentAt;
            var glitchAge = now - glitchAt;
            return new CanopyFrame(CanopyStateBlend.Select(low, transitionAge < projection.Profile.bigMomentSeconds),
                hitAge < .7f ? hitAge : -1f, size * CameraSettings.ShakeScale,
                transitionAge < .35f ? transitionAge : -1f,
                glitchAge < .35f ? glitchAge : -1f, glitchStrength * CameraSettings.ShakeScale);
        }

        internal static float LowHullHitAge(float elapsed)
        {
            var cycle = elapsed % 19f;
            var offset = cycle >= 17.9f ? 17.9f : cycle >= 13.27f ? 13.27f : cycle >= 8.64f ? 8.64f :
                cycle >= 7.1f ? 7.1f : cycle >= 3.78f ? 3.78f : cycle >= 1.23f ? 1.23f : 0f;
            return cycle - offset;
        }

        private void OnDamaged(AppliedDamage hit)
        {
            if (!CanDisplay) return;
            hitAt = Time.unscaledTime;
            hitSize = hit.HitSize(damage.Health.MaxValue);
        }

        private void OnShakeRaised(Object source, CameraShakeProfile profile, Vector2 _)
        {
            raised[source] = profile;
            Glitch(profile.glitch);
        }

        private void OnShakeReleased(Object source)
        {
            if (raised.Remove(source, out var profile)) Glitch(profile.releaseGlitch);
        }

        private void Glitch(float strength)
        {
            if (strength <= 0f || !CanDisplay) return;
            glitchAt = Time.unscaledTime;
            glitchStrength = strength;
        }

        private void OnStepChanged(string next)
        {
            if (next != step && next == PhaseSpineModule.StepExtract && CanDisplay)
                momentAt = Time.unscaledTime;
            step = next;
        }

        private void OnDestroy()
        {
            if (damage != null) damage.OnDamaged -= OnDamaged;
            if (shake != null)
            {
                shake.Raised -= OnShakeRaised;
                shake.Released -= OnShakeReleased;
            }
            if (objectives != null) objectives.OnSpineStepChanged -= OnStepChanged;
        }
    }

    public readonly struct CanopyFrame
    {
        public static CanopyFrame Idle => new(CanopyState.Base, -1f, 0f, -1f, -1f, 0f);
        public readonly CanopyState State;
        public readonly float HitAge;
        public readonly float HitSize;
        public readonly float TransitionAge;
        public readonly float GlitchAge;
        public readonly float GlitchStrength;

        public CanopyFrame(CanopyState state, float hitAge, float hitSize, float transitionAge,
            float glitchAge, float glitchStrength)
        {
            State = state;
            HitAge = hitAge;
            HitSize = hitSize;
            TransitionAge = transitionAge;
            GlitchAge = glitchAge;
            GlitchStrength = glitchStrength;
        }
    }
}
