using System;
using System.Collections;
using System.Collections.Generic;

namespace Game.Preview
{
    /// <summary>
    /// One HUD moment as a preview subject that plays from its start instead of scrubbing. Composing
    /// stages its world through <see cref="StagedMoments"/>: Startup starts a run as the game does,
    /// Extraction reloads a fixture whose spine opens extraction on the player, and Damage and Low hull
    /// play in a quiet fixture, respawning the player fresh when that fixture is already loaded. Their
    /// hit lands on the first frame the transport moves, so a restart re-composes the same moment.
    /// </summary>
    public sealed class HudReplaySubject : IPreviewSubject
    {
        private readonly PreviewEntry moment;
        private readonly int sector;
        private readonly PreviewSpan timeline;
        private PreviewWorld world;
        private bool triggered;

        public HudReplaySubject(PreviewEntry moment, int sector)
        {
            this.moment = moment;
            this.sector = sector;
            timeline = moment switch
            {
                PreviewEntry.Startup => new PreviewSpan("Startup", 8f, false),
                PreviewEntry.LowHull => new PreviewSpan("Low hull", 10f, false),
                PreviewEntry.Damage => new PreviewSpan("Damage", 2.5f, false),
                PreviewEntry.Extraction => new PreviewSpan("Extraction", 5f, false),
                _ => throw new ArgumentOutOfRangeException(nameof(moment), moment, "Not a HUD moment.")
            };
            Fixtures = new[] { timeline.Name };
        }

        public string Name => timeline.Name;
        public bool Scrubs => false;
        public IReadOnlyList<string> Fixtures { get; }
        public int Fixture => 0;
        public int SpanCount => 1;

        public void SelectFixture(int index)
        {
            if (index != 0) throw new ArgumentOutOfRangeException(nameof(index));
        }

        public PreviewSpan Span(int index) => index == 0 ? timeline : throw new ArgumentOutOfRangeException(nameof(index));

        public IEnumerator Compose(PreviewWorld world)
        {
            this.world = world;
            if (moment == PreviewEntry.Startup)
                yield return StagedMoments.StartRun(world.Session, world.Rig, sector);
            else if (moment == PreviewEntry.Extraction || world.LoadedSector != sector)
                yield return StagedMoments.ReloadSector(world.Session, world.Rig, sector);
            else
                world.Session.Units.RespawnShip(world.Rig.Player.Id, world.Session.ActiveSector.StartPoint, 0f);
            world.LoadedSector = sector;
        }

        public void Show(int span, float seconds, float holdSeconds)
        {
            if (triggered || seconds <= 0f) return;
            triggered = true;
            var player = world.Rig.Player;
            if (moment == PreviewEntry.Damage)
                StagedMoments.Hit(player, player.Damage.Shield.CurrentValue + .25f * player.Damage.Health.MaxValue);
            else if (moment == PreviewEntry.LowHull)
                StagedMoments.BringHullTo(player, .2f);
        }

        public void Dispose()
        {
            if (moment == PreviewEntry.Startup) world.Rig.EnterSector(null);
        }
    }
}
