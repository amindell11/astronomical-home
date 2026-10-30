using System;
using System.Collections;
using System.Collections.Generic;
using Damage;
using NUnit.Framework;
using Objectives;
using Ships.Damage;
using Ships.Registry;
using Substrate.Sectors.Elements;
using Substrate.Sectors.Elements.Phases;
using Substrate.Services.Objectives;
using Tests.PlayMode.Common;
using UI.PlayerState;
using UI.Projection;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Tests.PlayMode.Rendering.Projection
{
    [Category("UI")]
    [Category("RequiresGraphics")]
    public sealed class CanopyEventsPlayModeTests
    {
        private sealed class DamageSource : IDamageEvents
        {
            public event Action<AppliedDamage> OnDamaged;
            public event Action<ShipId, DamageInfo> OnDeath { add { } remove { } }
            public Resource Health { get; } = new(100f);
            public RegenResource Shield { get; } = new(100f, 0f, 999f);
            public void Hit() => OnDamaged?.Invoke(default);
        }

        private sealed class Step : ObjectiveState
        {
            private readonly ObjectiveType type;
            public bool Complete;
            public Step(ObjectiveType type) => this.type = type;
            public override ObjectiveType StateType => type;
            public override bool IsComplete => Complete;
            public override void Tick(float deltaTime) { }
        }

        private GameObject root;
        private CanopyProjection projection;
        private CanopyProjectionProfile profile;
        private CanopyPersonality personality;
        private ObjectiveService objectives;
        private DamageSource damage;
        private bool visible;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Canopy events");
            var camera = Child("Scene camera").AddComponent<Camera>();
            camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
            projection = Object.Instantiate(TestAssets.Load<GameObject>("Assets/Prefabs/Cameras/UI Cam.prefab"),
                root.transform).GetComponent<CanopyProjection>();
            profile = Object.Instantiate(projection.Profile);
            profile.bigMomentSeconds = .15f;
            objectives = Child("Objectives").AddComponent<ObjectiveService>();
            visible = true;
            personality = CanopyPersonality.Create(root, objectives, projection, () => visible);
            damage = new DamageSource();
            personality.Initialize(damage);
            projection.Initialize(camera, profile, personality.Observe);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(profile);
            Time.timeScale = 1f;
        }

        private GameObject Child(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            return go;
        }

        private (PhaseSpineModule, WaveDirector) Phase()
        {
            var director = Child("Director").AddComponent<WaveDirector>();
            director.Configure(null, new[] { new SectorPhase { seconds = 40f }, new SectorPhase { seconds = 40f } }, 5f);
            director.BeginSchedule(0f);
            var module = Child("Phase").AddComponent<PhaseSpineModule>();
            module.Bind(director, null);
            return (module, director);
        }

        private Step InstallSpine()
        {
            var survive = new Step(ObjectiveType.Survive);
            objectives.SetSpineObjective(new MissionDefinition(PhaseSpineModule.StepSurvive,
                new Dictionary<string, string> { [PhaseSpineModule.StepSurvive] = PhaseSpineModule.StepExtract }),
                new Dictionary<string, Func<ObjectiveState>>
                {
                    [PhaseSpineModule.StepSurvive] = () => survive,
                    [PhaseSpineModule.StepExtract] = () => new Step(ObjectiveType.ExtractionChallenge)
                }, root.transform);
            return survive;
        }

        [UnityTest]
        public IEnumerator HitsHullAndRebinding_UseUnscaledClocks_AndHiddenOrOffEventsDoNotReplay()
        {
            Time.timeScale = 0f;
            damage.Hit();
            Assert.That(personality.Observe().HitAge, Is.EqualTo(0f).Within(.001f));
            yield return new WaitForSecondsRealtime(.08f);
            var age = personality.Observe().HitAge;
            personality.Initialize(damage);
            Assert.That(personality.Observe().HitAge, Is.GreaterThanOrEqualTo(age));
            damage.Hit();
            Assert.That(personality.Observe().HitAge, Is.EqualTo(0f).Within(.001f));
            damage.Health.SetFraction(StatusBarUI.LowThreshold);
            Assert.That(personality.Observe().State, Is.EqualTo(CanopyState.LowHull));
            damage.Health.SetFraction(0f);
            Assert.That(personality.Observe().State, Is.EqualTo(CanopyState.Base));
            var previous = damage;
            damage = new DamageSource();
            personality.Initialize(damage);
            previous.Hit();
            Assert.That(personality.Observe().HitAge, Is.EqualTo(-1f));
            damage.Hit();
            visible = false;
            Assert.That(personality.Observe().HitAge, Is.EqualTo(-1f));
            damage.Hit();
            visible = true;
            Assert.That(personality.Observe().HitAge, Is.EqualTo(-1f));
            profile.personalityEnabled = false;
            damage.Hit();
            Assert.That(personality.Observe().HitAge, Is.EqualTo(-1f));
            profile.personalityEnabled = true;
            Assert.That(personality.Observe().HitAge, Is.EqualTo(-1f));
            damage.Hit();
            yield return null;
            Assert.That(projection.HitAge, Is.GreaterThanOrEqualTo(0f));
        }

        [UnityTest]
        public IEnumerator Moments_StartAtLivePhasesAndExtraction_PreservePriorityAndSourceIdentity()
        {
            damage.Health.SetFraction(.2f);
            var (phase, director) = Phase();
            personality.EnterSector(phase);
            Assert.That(personality.Observe().State, Is.EqualTo(CanopyState.BigMoment));
            yield return new WaitForSecondsRealtime(.17f);
            personality.Initialize(damage);
            personality.EnterSector(phase);
            Assert.That(personality.Observe().State, Is.EqualTo(CanopyState.LowHull));
            director.Advance(41f);
            Assert.That(personality.Observe().State, Is.EqualTo(CanopyState.LowHull), "A lull names the next phase");
            director.Advance(45f);
            Assert.That(personality.Observe().State, Is.EqualTo(CanopyState.BigMoment));
            personality.EnterSector(null);
            Assert.That(personality.Observe().TransitionAge, Is.EqualTo(-1f));
            visible = false;
            var (replacement, _) = Phase();
            personality.EnterSector(replacement);
            personality.Observe();
            visible = true;
            Assert.That(personality.Observe().State, Is.EqualTo(CanopyState.LowHull), "Hidden phase starts do not replay");
            var (next, _) = Phase();
            personality.EnterSector(next);
            Assert.That(personality.Observe().State, Is.EqualTo(CanopyState.BigMoment), "Source replacement starts its own phase zero");
            personality.EnterSector(null);
            var survive = InstallSpine();
            Assert.That(personality.Observe().State, Is.EqualTo(CanopyState.LowHull));
            survive.Complete = true;
            objectives.Tick(0f);
            Assert.That(personality.Observe().State, Is.EqualTo(CanopyState.BigMoment));
            yield return new WaitForSecondsRealtime(.17f);
            personality.Initialize(damage);
            Assert.That(personality.Observe().State, Is.EqualTo(CanopyState.LowHull));
            visible = false;
            survive = InstallSpine();
            survive.Complete = true;
            objectives.Tick(0f);
            personality.Observe();
            visible = true;
            Assert.That(personality.Observe().TransitionAge, Is.EqualTo(-1f));
        }

        [UnityTest]
        public IEnumerator ExtractionAppearance_MultipliesExistingFade_AndUsesResolvedWorkingProfile()
        {
            profile.baseSettings.flicker = 1f;
            projection.ForceMode = CanopyForceMode.Base;
            var canvas = Child("HUD").AddComponent<Canvas>();
            var readout = ObjectiveStatusReadout.Create(canvas.transform,
                TestAssets.Load<Font>("Assets/Visuals/Ui/Fonts/IBMPlexSans.ttf"),
                TestAssets.Load<Font>("Assets/Visuals/Ui/Fonts/Michroma.ttf"),
                TestAssets.Load<Font>("Assets/Visuals/Ui/Fonts/DSEG14Classic.ttf"),
                TestAssets.Load<Material>("Assets/Visuals/Ui/Projection/CanopyText.mat"), projection, () => visible);
            readout.Initialize(objectives);
            var survive = InstallSpine();
            survive.Complete = true;
            objectives.Tick(0f);
            var announcement = readout.transform.Find("Extraction announcement").GetComponent<UnityEngine.UI.Text>();
            var appearance = readout.GetComponentInChildren<InstrumentAppear>().GetComponent<CanvasGroup>();
            var sawDip = false;
            var end = Time.unscaledTime + .5f;
            while (Time.unscaledTime < end)
            {
                yield return null;
                if (appearance.alpha >= 1f) continue;
                sawDip = true;
                Assert.That(appearance.alpha, Is.EqualTo(.83f).Within(.001f));
                Assert.That(announcement.color.a * announcement.canvasRenderer.GetInheritedAlpha(),
                    Is.EqualTo(announcement.color.a * appearance.alpha).Within(.001f));
                Assert.That(announcement.color.a * appearance.alpha, Is.LessThan(announcement.color.a));
            }
            Assert.That(sawDip, Is.True);
            Assert.That(announcement.color.a, Is.EqualTo(1f));
            projection.PersonalityEnabled = false;
            yield return null;
            Assert.That(appearance.alpha, Is.EqualTo(1f));
            Assert.That(announcement.color.a, Is.EqualTo(1f));
        }
    }
}
