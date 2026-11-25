using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Ships;
using Substrate;

namespace Game.Preview
{
    /// <summary>
    /// One enemy's death as a preview subject that plays from its start. Composing stages the player in
    /// the quiet fixture as Damage does, then spawns the picked hangar ship pilotless on the enemy team,
    /// ahead of the player and facing it. Half a second in, the victim dies through
    /// <see cref="StagedMoments.Kill"/>, so the breakup is the ship's own; a restart re-composes the same pick.
    /// </summary>
    public sealed class EnemyDeathSubject : IPreviewSubject
    {
        private const float Distance = 20f;
        private const float KillSeconds = .5f;
        private static readonly PreviewSpan Timeline = new("Enemy death", 4f, false);
        private readonly Ship[] ships;
        private readonly int sector;
        private PreviewWorld world;
        private Ship victim;
        private bool killed;

        public EnemyDeathSubject(Ship[] ships, int sector, int fixture)
        {
            this.ships = ships;
            this.sector = sector;
            Fixtures = ships.Select(ship => ship.name).ToArray();
            SelectFixture(fixture);
        }

        public string Name => Timeline.Name;
        public bool Scrubs => false;
        public IReadOnlyList<string> Fixtures { get; }
        public int Fixture { get; private set; }
        public int SpanCount => 1;

        public void SelectFixture(int index)
        {
            if (index < 0 || index >= ships.Length) throw new ArgumentOutOfRangeException(nameof(index));
            Fixture = index;
        }

        public PreviewSpan Span(int index) => index == 0 ? Timeline : throw new ArgumentOutOfRangeException(nameof(index));

        public IEnumerator Compose(PreviewWorld world)
        {
            this.world = world;
            if (world.LoadedSector != sector)
                yield return StagedMoments.ReloadSector(world.Session, world.Rig, sector);
            else
                world.Session.Units.RespawnShip(world.Rig.Player.Id, world.Session.ActiveSector.StartPoint, 0f);
            world.LoadedSector = sector;
            var player = world.Rig.Player.transform;
            var heading = GamePlane.WorldDirOnPlane(player.up);
            victim = world.Session.Units.SpawnShip(ships[Fixture], null, 1,
                GamePlane.WorldPointOnPlane(player.position + heading * Distance),
                GamePlane.PlanePose(GamePlane.Normal, -heading), null);
            // The victim's visual rig binds in Start, before any kill can land.
            yield return null;
        }

        public void Show(int span, float seconds, float holdSeconds)
        {
            if (killed || seconds < KillSeconds) return;
            killed = true;
            StagedMoments.Kill(victim);
        }

        public void Dispose() => world.Session.Units.DespawnShip(victim);
    }
}
