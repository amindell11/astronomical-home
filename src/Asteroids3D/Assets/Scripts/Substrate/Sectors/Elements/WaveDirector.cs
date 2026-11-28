using Balance;
using System;
using System.Collections;
using System.Collections.Generic;
using AI;
using AI.Pilots;
using AI.Plays;
using AI.Scanning;
using AI.Tactics;
using Combat.Weapons;
using Ships;
using Ships.Command;
using Ships.Registry;
using Substrate.Sectors.Elements.Elites;
using Substrate.Sectors.Elements.Hunt;
using Substrate.Sectors.Elements.Phases;
using Substrate.Sectors.Elements.Placement;
using Substrate.Sectors.Factions;
using Substrate.Services.Units;
using Substrate.Sessions;
using UnityEngine;

namespace Substrate.Sectors.Elements
{
    /// <summary>
    /// Sends a rival leading minions on each sector phase's wave-every beat while live rivals sit under the
    /// row's rival cap. With the rival cap full, the beat's minions reinforce the live rival with the fewest
    /// followers instead, nearest first on a tie, at its free places, so minions never arrive without a
    /// rival. Minions are trimmed to the minion cap, which counts the hunt's live patrols. The rival takes
    /// the arc's centre slot, or arrives concealed ahead of the hero when its temper ambushes and that point
    /// is clear, and the minions take the slots either side, each following the rival at its place.
    /// A rival whose temper lists plays gets a play call its wave joins.
    /// Every wave ship hunts the hero by sight. A beat with no room is dropped. A phase opens with one.
    /// A rival that dies with its minions still flying raises RivalFell. When the faction's minion temper
    /// regroups, each minion that jumps out is held and arrives with the next rival's wave, on top of its
    /// drawn minions and still under the minion cap; a reinforcement never brings one, and the held count
    /// ends with the sector.
    /// Nothing arrives in a lull. With an elite, a closing lull follows the last phase; without one, the
    /// done table keeps the last phase's waves. It begins its elite, summons it on the tick the table is
    /// done, and ends it at teardown. Nothing arrives for the elite's grace; after it, while the elite
    /// lives, each beat of the last phase reinforces the elite instead of a rival.
    /// The opening lull is held for the hunt. Ships arrive facing the hero,
    /// each on its rank's kit, pilot, scale and the faction skin. Dead products and products that ask to
    /// jump despawn on the tick, which idles while the hero is inactive. Nothing spawns in Build, since
    /// the asteroid field lays out at its Start. The base class's "produce exactly once" is about the
    /// activation token, not lifetime.
    /// </summary>
    public class WaveDirector : SectorSpawner, ISectorRowReader, IWaveSignal
    {
        [Tooltip("The sector's faction. Each wave is its rival leading minions, on that rank's kit, pilot and scale.")]
        [SerializeField] private SectorFaction faction;
        [SerializeField] private int team = 1;

        [Header("Sector phases")]
        [Tooltip("Seconds between one phase's end and the next phase's opening, with nothing spawning.")]
        [Stat, SerializeField, Min(0f)] private float lullSeconds = 5f;
        [Tooltip("Seconds from the sector's start to phase one's opening, with nothing spawning.")]
        [Stat, SerializeField, Min(0f)] private float openingLullSeconds = 5f;
        [Tooltip("The sector's opening hunt. Set, the opening lull lasts until the hunt releases; unset, the seconds above.")]
        [SerializeField] private SectorHunt hunt;
        [Tooltip("The sector's elite; set, a lull follows the last phase, and when the table is done the director summons it and sends no more waves.")]
        [SerializeField] private EliteEncounter elite;
        [Tooltip("Seconds into the elite's fight before the last phase's beat starts sending it minions, up to the minion cap.")]
        [Stat, SerializeField, Min(0f)] private float eliteGraceSeconds = 30f;

        [Header("Placement")]
        [Tooltip("Plane distance from the hero; author it past the camera's widest view so ships arrive off screen.")]
        [SerializeField] private float spawnRadius = 55f;
        [Tooltip("Distance along the ring between ships of one wave.")]
        [SerializeField, Min(0f)] private float groupSpread = 8f;
        [Tooltip("A candidate point overlapping an asteroid within this radius is rejected.")]
        [SerializeField] private float clearanceRadius = 6f;
        [Tooltip("Random angles tried per spawn; none clear skips this tick.")]
        [SerializeField, Min(1)] private int placementAttempts = 4;
        [Tooltip("Plane distance ahead of the hero's nose at which an ambushing rival arrives concealed.")]
        [SerializeField] private float ambushSpawnAhead = 25f;

        private readonly List<Ship> products = new();
        private readonly HashSet<Ship> rivals = new();
        private readonly Dictionary<Ship, PlayCall> calls = new();
        private readonly System.Random playDraw = new();
        private readonly HashSet<Ship> jumped = new();
        private readonly List<Action> jumpHandlers = new();
        private readonly List<Ship> leaders = new();
        private readonly List<int> places = new();
        private SectorPhase[] phases = Array.Empty<SectorPhase>();
        private int rivalCap;
        private int minionCap;
        private IUnitService units;
        private IObstacleField field;
        private Ship hero;
        private SectorPhaseSchedule schedule;
        private float startTime;
        private bool started;
        private bool huntReleased;
        private bool rivalAmbushes;
        private bool minionsRegroup;
        private int regrouping;
        private int openedPhase;
        private int sectorIndex;
        private int? pending;
        private float lastWave;
        private float eliteSince;

        public event Action<int, Ship> WaveSent;

        public event Action<int> RivalFell;

        /// <summary>Where the phase table stands as of the last tick.</summary>
        public SectorPhaseMark Phase { get; private set; }

        public bool EarnedLull { get; private set; }

        public float NextWaveSeconds { get; private set; }

        public bool WaveAhead { get; private set; }

        /// <summary>True when a lull follows this sector's last phase, before its elite.</summary>
        public bool ClosingLull { get; private set; }

        public IEliteStatus Elite => elite ? elite : null;

        /// <summary>In a lull after a phase, scheduled or earned, or the graceSeconds after one ends.</summary>
        public bool InLullWindow(float graceSeconds)
        {
            if (EarnedLull) return true;
            if (Phase.InOpeningLull) return false;
            if (Phase.InLull) return true;
            if (Phase.Done) return ClosingLull && Phase.ElapsedSeconds < graceSeconds;
            return Phase.Index > 0 && Phase.ElapsedSeconds < graceSeconds;
        }

        public int PhaseCount => phases.Length;

        /// <summary>A phase whose minion range runs backwards, or a rank missing its kit, pilot or scale, fails at session start.</summary>
        public void CheckRow(SectorEntry row)
        {
            if (row.phases.Length == 0)
                throw new InvalidOperationException(
                    $"WaveDirector on '{name}' reads the row's phases, and the row has none.");
            if (row.rivalCap < 1)
                throw new InvalidOperationException(
                    $"WaveDirector on '{name}' reads the row's rival cap, and the row asks for {row.rivalCap}.");
            if (row.minionCap < 0)
                throw new InvalidOperationException(
                    $"WaveDirector on '{name}' reads the row's minion cap, and the row asks for {row.minionCap}.");
            for (var i = 0; i < row.phases.Length; i++)
            {
                var phase = row.phases[i];
                if (!phase.sendsWaves) continue;
                if (phase.fewestMinions < 0 || phase.fewestMinions > phase.mostMinions)
                    throw new InvalidOperationException(
                        $"WaveDirector on '{name}' phase {i} asks for {phase.fewestMinions}..{phase.mostMinions} minions.");
            }

            if (elite) elite.CheckRow(row);
            if (!faction)
                throw new InvalidOperationException($"WaveDirector on '{name}' needs its faction.");
            if (!row.faction)
                throw new InvalidOperationException($"WaveDirector on '{name}' flies the row's faction, and the row names none.");
            _ = row.faction.Rival;
            _ = row.faction.Minion;
        }

        protected override IEnumerator Produce(SectorBuildContext ctx)
        {
            CheckRow(ctx.Row);
            sectorIndex = ctx.SectorIndex;
            var rivalTemper = Temper(ctx.Row.faction.Rival.pilot);
            rivalAmbushes = rivalTemper && rivalTemper.ambushes;
            var minionTemper = Temper(ctx.Row.faction.Minion.pilot);
            minionsRegroup = minionTemper && minionTemper.regroupsWithoutLeader;
            regrouping = 0;
            if (elite) elite.Begin(ctx);
            products.Clear();
            jumped.Clear();
            jumpHandlers.Clear();
            leaders.Clear();
            places.Clear();
            rivals.Clear();
            calls.Clear();
            Spawned = products;
            phases = ctx.Row.phases;
            rivalCap = ctx.Row.rivalCap;
            minionCap = ctx.Row.minionCap;

            if (!ctx.Hero)
            {
                Debug.LogError($"WaveDirector on '{name}' has no hero — director is inert.", this);
                yield break;
            }

            BuildSchedule();

            units = ctx.Units;
            field = ctx.Field;
            hero = ctx.Hero;
            started = false;
            Phase = schedule.At(0f);
        }

        protected override IEnumerator OnTeardown(SectorBuildContext ctx)
        {
            if (elite) elite.End();
            hero = null;
            for (var i = 0; i < products.Count; i++)
                if (products[i]) products[i].JumpRequested -= jumpHandlers[i];
            jumpHandlers.Clear();
            leaders.Clear();
            places.Clear();
            jumped.Clear();
            regrouping = 0;
            var teardown = base.OnTeardown(ctx);
            while (teardown.MoveNext()) yield return teardown.Current;
            products.Clear();
            rivals.Clear();
            calls.Clear();
            units = null;
            field = null;
        }

        private void Update()
        {
            if (!hero || !hero.gameObject.activeInHierarchy) return;
            Tick(Time.time);
        }

        internal void Tick(float now)
        {
            if (!started)
            {
                started = true;
                BeginSchedule(now);
            }

            DespawnDead();
            HoldForHunt(now);
            Advance(now);
            SummonElite(now);
            var minions = WaveDue(now);
            if (minions != null && Send(minions.Value))
                Arrived(now);
            Observe(now, Alive);
        }

        private bool Send(int drawn)
        {
            if (WavesOver) return TryReinforce(MinionRoom(drawn, AliveMinions), _ => elite.Ship);
            if (!RivalRoom(rivals.Count)) return TryReinforce(MinionRoom(drawn, AliveMinions), LeastEscorted);
            var sent = MinionRoom(drawn + regrouping, AliveMinions);
            var rival = TrySend(sent);
            if (!rival) return false;
            regrouping = Rejoined(regrouping, sent);
            WaveSent?.Invoke(sectorIndex, rival);
            return true;
        }

        internal int Alive => products.Count + (hunt ? hunt.LivePatrols : 0);

        private int AliveMinions => Alive - rivals.Count;

        internal bool RivalRoom(int aliveRivals) => aliveRivals < rivalCap;

        internal int MinionRoom(int minions, int aliveMinions) => Mathf.Clamp(minionCap - aliveMinions, 0, minions);

        /// <summary>Held minions fill a sent wave's minion places first; the rest wait for a later wave.</summary>
        internal static int Rejoined(int regrouping, int sent) => regrouping - Mathf.Min(regrouping, sent);

        /// <summary>True for a rival of this director's waves until the tick after its death despawns it.</summary>
        public bool IsRival(ShipId ship)
        {
            foreach (var rival in rivals)
                if (rival.Id == ship) return true;
            return false;
        }

        /// <summary>The free place nearest the leader: -1, +1, -2, +2 and on.</summary>
        internal static int FreePlace(ICollection<int> taken)
        {
            for (var step = 1; ; step++)
            {
                if (!taken.Contains(-step)) return -step;
                if (!taken.Contains(step)) return step;
            }
        }

        private void BuildSchedule()
        {
            var closing = elite ? lullSeconds : 0f;
            ClosingLull = closing > 0f;
            schedule = new SectorPhaseSchedule(phases, lullSeconds, openingLullSeconds, closing);
        }

        internal void BeginSchedule(float now)
        {
            startTime = now;
            openedPhase = -1;
            huntReleased = false;
            pending = null;
            lastWave = now;
            Phase = schedule.At(0f);
            EarnedLull = false;
            WaveAhead = false;
            NextWaveSeconds = 0f;
        }

        /// <summary>Holds the opening lull at its start until the hunt releases, then ends it, once.</summary>
        internal void HoldForHunt(float now)
        {
            if (!hunt || huntReleased || !Phase.InOpeningLull) return;
            huntReleased = hunt.Released;
            startTime = huntReleased ? now - openingLullSeconds : now;
        }

        internal void Advance(float now) => Phase = schedule.At(now - startTime);

        private void SummonElite(float now)
        {
            if (!WavesOver || elite.Summoned) return;
            elite.Summon();
            eliteSince = now;
        }

        /// <summary>True once, on the first tick of each phase; a lull or the done table opens nothing.</summary>
        internal bool OpensPhase()
        {
            if (Phase.Done || Phase.InLull || Phase.Index == openedPhase) return false;
            openedPhase = Phase.Index;
            return true;
        }

        private bool WavesOver => elite && Phase.Done;

        private bool EliteCallsMinions(float now) => elite.Present && now >= eliteSince + eliteGraceSeconds;

        internal int? WaveDue(float now)
        {
            if (Phase.InLull || (WavesOver && !EliteCallsMinions(now)))
                return null;

            var phase = phases[Phase.Index];
            if (OpensPhase())
                pending = Draw(phase);
            else if (pending == null && now >= lastWave + phase.waveEvery)
                pending = Draw(phase);
            return pending;
        }

        internal void Arrived(float now)
        {
            pending = null;
            lastWave = now;
        }

        internal void Observe(float now, int alive)
        {
            var phase = phases[Phase.Index];
            var untilWave = lastWave + phase.waveEvery - now;
            var inPhase = !Phase.InLull && !Phase.Done;
            var waveLeft = phase.sendsWaves && (pending != null || untilWave < Phase.SecondsLeft);
            var waveFits = inPhase && waveLeft;

            EarnedLull = inPhase && openedPhase == Phase.Index && alive == 0 && !waveLeft;
            if (waveFits)
                NextWaveSeconds = pending != null ? 0f : Mathf.Max(0f, untilWave);
            else
                NextWaveSeconds = !Phase.Closing && (Phase.InLull || Phase.Index < phases.Length - 1)
                    ? Phase.CountdownSeconds
                    : 0f;
            WaveAhead = waveFits || LaterPhaseSendsWaves();
        }

        private bool LaterPhaseSendsWaves()
        {
            if (Phase.Done || Phase.Closing) return false;
            var from = Phase.InLull ? Phase.Index : Phase.Index + 1;
            for (var i = from; i < phases.Length; i++)
                if (phases[i].sendsWaves) return true;
            return false;
        }

        private static int? Draw(SectorPhase phase) =>
            phase.sendsWaves
                ? UnityEngine.Random.Range(phase.fewestMinions, phase.mostMinions + 1)
                : null;

        /// <summary>Reads only serialized data and the row, so a prefab asset answers too.</summary>
        public SectorThreat Threat(SectorEntry row)
        {
            var ships = new List<Ship>();
            AddDistinct(ships, row.faction.Rival.ship);
            AddDistinct(ships, row.faction.Minion.ship);
            AddDistinct(ships, row.faction.EliteShip);

            var weapons = new List<WeaponComponent>();
            AddDistinct(weapons, row.faction.Rival.kit);
            AddDistinct(weapons, row.faction.Minion.kit);
            return new SectorThreat(ships, weapons);
        }

        private static void AddDistinct<T>(List<T> list, T item) where T : UnityEngine.Object
        {
            if (item && !list.Contains(item)) list.Add(item);
        }

        private void DespawnDead()
        {
            var rivalFell = false;
            for (var i = products.Count - 1; i >= 0; i--)
            {
                var ship = products[i];
                var flying = Flying(ship);
                if (flying && !jumped.Contains(ship)) continue;
                if (flying && minionsRegroup && !rivals.Contains(ship)) regrouping++;
                if (!flying && rivals.Contains(ship) && HasFlyingMinions(ship)) rivalFell = true;
                if (ship) ship.JumpRequested -= jumpHandlers[i];
                jumpHandlers.RemoveAt(i);
                leaders.RemoveAt(i);
                places.RemoveAt(i);
                jumped.Remove(ship);
                if (rivals.Remove(ship)) calls.Remove(ship);
                if (ship) units.DespawnShip(ship);
                products.RemoveAt(i);
            }

            if (rivalFell) RivalFell?.Invoke(sectorIndex);
        }

        private static bool Flying(Ship ship) => ship && ship.gameObject.activeInHierarchy;

        private bool HasFlyingMinions(Ship rival)
        {
            for (var i = 0; i < products.Count; i++)
                if (ReferenceEquals(leaders[i], rival) && Flying(products[i]) && !jumped.Contains(products[i]))
                    return true;
            return false;
        }

        private void Track(Ship ship)
        {
            Action handler = () => jumped.Add(ship);
            ship.JumpRequested += handler;
            jumpHandlers.Add(handler);
            leaders.Add(null);
            places.Add(0);
            products.Add(ship);
        }

        private Ship TrySend(int minions)
        {
            var center = GamePlane.WorldPointToPlane(hero.transform.position);
            var count = 1 + minions;
            var slots = ClearSlots(center, count);
            if (slots == null) return null;

            var centreIndex = (count - 1) / 2;
            var rivalRank = faction.Faction.Rival;
            var minionRank = faction.Faction.Minion;
            Ship rival = null;
            if (rivalAmbushes)
            {
                var ahead = center + hero.Kinematics.Forward * ambushSpawnAhead;
                if (ClearPoint.IsClear(field, ahead, clearanceRadius))
                {
                    rival = Spawn(rivalRank, ahead, center, asRival: true);
                    rival.Keystone.ActivateOnArrival(rival.Kinematics);
                }
            }

            var spawned = new Ship[count];
            for (var i = 0; i < count; i++)
            {
                if (i != centreIndex) spawned[i] = Spawn(minionRank, slots[i], center);
                else spawned[i] = rival ? rival : Spawn(rivalRank, slots[i], center, asRival: true);
            }
            rival = spawned[centreIndex];

            for (var i = 0; i < count; i++)
            {
                if (i != centreIndex)
                    Follow(spawned[i], rival, i - centreIndex);
                ((AICommander)spawned[i].Commander).HuntBySight(hero);
            }

            var temper = Temper(rivalRank.pilot);
            if (temper && temper.plays.Length > 0)
            {
                var call = new PlayCall(rival, temper.plays, playDraw);
                calls.Add(rival, call);
                ((AICommander)rival.Commander).JoinCall(call);
                var joining = new List<int>(count - 1);
                for (var i = 0; i < count; i++)
                    if (i != centreIndex) joining.Add(i);
                joining.Sort((a, b) =>
                {
                    var placeA = a - centreIndex;
                    var placeB = b - centreIndex;
                    var byDistance = Math.Abs(placeA).CompareTo(Math.Abs(placeB));
                    return byDistance != 0 ? byDistance : placeA.CompareTo(placeB);
                });
                foreach (var i in joining)
                {
                    var minion = spawned[i];
                    ((AICommander)minion.Commander).JoinCall(call);
                    call.Join(minion);
                }
            }

            return rival;
        }

        private bool TryReinforce(int minions, Func<Vector2, Ship> leaderFor)
        {
            if (minions == 0) return true;
            var center = GamePlane.WorldPointToPlane(hero.transform.position);
            var slots = ClearSlots(center, minions);
            if (slots == null) return false;

            var leader = leaderFor(slots[(minions - 1) / 2]);
            PlayCall call = null;
            if (leader) calls.TryGetValue(leader, out call);
            foreach (var slot in slots)
            {
                var minion = Spawn(faction.Faction.Minion, slot, center);
                Follow(minion, leader, FreePlace(PlacesAround(leader)));
                ((AICommander)minion.Commander).HuntBySight(hero);
                if (call == null) continue;
                ((AICommander)minion.Commander).JoinCall(call);
                call.Join(minion);
            }
            return true;
        }

        private Vector2[] ClearSlots(Vector2 center, int count)
        {
            for (var attempt = 0; attempt < placementAttempts; attempt++)
            {
                var slots = Slots(center, UnityEngine.Random.Range(0f, 2f * Mathf.PI), count);
                if (!Array.Exists(slots, slot => !ClearPoint.IsClear(field, slot, clearanceRadius)))
                    return slots;
            }
            return null;
        }

        private Ship LeastEscorted(Vector2 arrival)
        {
            Ship least = null;
            var fewest = int.MaxValue;
            var nearest = float.PositiveInfinity;
            foreach (var rival in rivals)
            {
                var followers = PlacesAround(rival).Count;
                var distance = Vector2.Distance(rival.Kinematics.pos, arrival);
                if (followers > fewest || (followers == fewest && distance >= nearest)) continue;
                least = rival;
                fewest = followers;
                nearest = distance;
            }
            return least;
        }

        private List<int> PlacesAround(Ship rival)
        {
            var taken = new List<int>();
            for (var i = 0; i < products.Count; i++)
                if (ReferenceEquals(leaders[i], rival)) taken.Add(places[i]);
            return taken;
        }

        private void Follow(Ship minion, Ship rival, int place)
        {
            var index = products.IndexOf(minion);
            leaders[index] = rival;
            places[index] = place;
            ((AICommander)minion.Commander).FollowLeader(rival, place);
        }

        private Ship Spawn(FactionRank rank, Vector2 planePoint, Vector2 center, bool asRival = false)
        {
            var outward = (planePoint - center).normalized;
            var facingHero = GamePlane.PlanePose(GamePlane.Normal, GamePlane.PlaneDirToWorld(-outward));
            var ship = units.SpawnShip(rank.ship, rank.pilot, team, GamePlane.PlanePointToWorld(planePoint),
                facingHero, field, rank.Loadout, rank.scale);
            // Counted before dressing, so a dressing throw cannot leave a live ship outside the caps.
            Track(ship);
            if (asRival) rivals.Add(ship);
            faction.Faction.Dress(ship, rank, faction.Locale);
            return ship;
        }

        private static TacticsSettings Temper(Commander pilot)
        {
            var brain = pilot.GetComponent<ArchetypeBrain>();
            return brain && brain.AuthoredTactics ? brain.AuthoredTactics : null;
        }

        internal Vector2[] Slots(Vector2 center, float bearing, int count)
        {
            var slots = new Vector2[count];
            var step = groupSpread / spawnRadius;
            for (var i = 0; i < count; i++)
            {
                var angle = bearing + (i - (count - 1) / 2f) * step;
                slots[i] = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * spawnRadius;
            }
            return slots;
        }

#if UNITY_EDITOR
        internal void Configure(SectorPhase[] phases, float lullSeconds = 5f, float spawnRadius = 55f,
            SectorFaction faction = null, float openingLullSeconds = 0f, SectorHunt hunt = null,
            EliteEncounter elite = null, float groupSpread = 8f, int rivalCap = 1, int minionCap = 4,
            float eliteGraceSeconds = 30f)
        {
            this.hunt = hunt;
            this.elite = elite;
            this.eliteGraceSeconds = eliteGraceSeconds;
            this.faction = faction;
            this.phases = phases;
            this.rivalCap = rivalCap;
            this.minionCap = minionCap;
            this.lullSeconds = lullSeconds;
            this.openingLullSeconds = openingLullSeconds;
            this.spawnRadius = spawnRadius;
            this.groupSpread = groupSpread;
            BuildSchedule();
        }
#endif
    }

    /// <summary>What a sector can send: its ships and the weapons they can carry, each listed once.</summary>
    public readonly struct SectorThreat
    {
        public IReadOnlyList<Ship> Ships { get; }
        public IReadOnlyList<WeaponComponent> Weapons { get; }

        public SectorThreat(IReadOnlyList<Ship> ships, IReadOnlyList<WeaponComponent> weapons)
        {
            Ships = ships;
            Weapons = weapons;
        }
    }
}
