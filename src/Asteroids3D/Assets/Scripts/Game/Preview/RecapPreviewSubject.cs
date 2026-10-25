using System;
using System.Collections.Generic;
using Cameras;
using Damage;
using Ships.Registry;
using Substrate;
using Substrate.Services.Units;
using UI.Recap;
using UI.Screens;
using UnityEngine;

namespace Game.Preview
{
    /// <summary>
    /// The run recap as a preview subject: a fixture, or any context a scenario builds, builds the real
    /// screen at rest, and showing an instant applies that state through the same presenter the game
    /// uses. A defeat shown from its first span stages the player's death at the context's death
    /// point, so the explosion is the ship's own; the sequence scrubs, the explosion plays through.
    /// </summary>
    public sealed class RecapPreviewSubject : IPreviewSubject
    {
        private readonly Transform uiRoot;
        private readonly PlayerRig rig;
        private readonly IUnitService units;
        private readonly ObserverCam observer;
        private RecapFeelProfile feel;
        private readonly Action<bool> freezeSimulation;
        private RecapOutcomeSequence sequence;
        private RunRecapScreen screen;
        private RecapPresenter presenter;
        private int shownSpan = -1;

        public RecapPreviewSubject(Transform uiRoot, PlayerRig rig, IUnitService units, ObserverCam observer,
            RecapFeelProfile feel, Action<bool> freezeSimulation)
        {
            this.uiRoot = uiRoot;
            this.rig = rig;
            this.units = units;
            this.observer = observer;
            this.feel = feel ? feel : throw new ArgumentNullException(nameof(feel));
            this.freezeSimulation = freezeSimulation;
            SelectFixture(0);
        }

        public string Name => "Run recap";
        public IReadOnlyList<string> Fixtures => RecapFixtures.Names;
        public int Fixture { get; private set; }
        public int SpanCount => sequence.SpanCount;
        public ScriptableObject Knobs
        {
            get => feel;
            set
            {
                feel = value as RecapFeelProfile;
                if (!feel) throw new ArgumentException("Run recap requires a RecapFeelProfile.", nameof(value));
                SelectFixture(Fixture);
            }
        }
        public RecapOutcomeContext Context => sequence.Context;

        public bool Busy => shownSpan == 0 && sequence.Context.Outcome == RecapOutcome.Defeat
                                           && rig.Player.VisualRig.DeathEffectsPlaying;

        public PreviewSpan Span(int index)
        {
            var span = sequence.Span(index);
            return new PreviewSpan(span.Phase.ToString(), span.Seconds,
                span.Hold is RecapHold.Press or RecapHold.Continue);
        }

        public void SelectFixture(int index)
        {
            Fixture = index;
            Select(RecapFixtures.Build(index, PlayerPoint()));
        }

        /// <summary>Shows any context at rest: the screen is rebuilt, the world restored, the player alive.</summary>
        public void Select(RecapOutcomeContext context)
        {
            if (screen) UnityEngine.Object.Destroy(screen.gameObject);
            presenter?.Restore();
            freezeSimulation(false);
            if (!rig.Player.gameObject.activeInHierarchy) units.RespawnShip(rig.Player.Id, PlayerPoint(), 0f);
            sequence = new RecapOutcomeSequence(feel, context);
            screen = RunRecapScreen.Create(uiRoot, context);
            presenter = new RecapPresenter(screen, rig.Overlay, units.ActiveRegistry.ActiveShips, observer);
            shownSpan = -1;
        }

        public void Show(int span, float seconds)
        {
            if (span == 0 && shownSpan != 0 && sequence.Context.Outcome == RecapOutcome.Defeat) StageDeath();
            shownSpan = span;
            var state = sequence.StateAt(span, seconds);
            presenter.Apply(state);
            freezeSimulation(state.SimulationFrozen);
        }

        // The ship's own death at the death point, through the real damage path.
        private void StageDeath()
        {
            var player = rig.Player;
            units.RespawnShip(player.Id, sequence.Context.DeathPoint, 0f);
            player.Damage.SetInvulnerability(0f);
            var blow = new DamageInfo(99999f, DamageKind.Laser, ShipId.Invalid, 0f, Vector3.zero, Vector3.zero);
            for (var i = 0; i < 8 && player.Damage.Health.CurrentValue > 0f; i++)
                player.Damage.TakeDamage(blow);
        }

        private Vector2 PlayerPoint() => GamePlane.WorldPointToPlane(rig.Player.transform.position);

        public void Dispose()
        {
            presenter?.Restore();
            freezeSimulation(false);
            if (screen) UnityEngine.Object.Destroy(screen.gameObject);
        }
    }
}
