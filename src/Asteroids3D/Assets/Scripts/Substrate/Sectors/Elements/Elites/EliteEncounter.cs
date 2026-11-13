using System;
using System.Collections.Generic;
using AI.Scanning;
using Damage;
using Loot;
using Ships;
using Ships.Command;
using Ships.Loadout;
using Ships.Registry;
using Substrate.Sectors.Factions;
using Substrate.Services.Units;
using Substrate.Sessions;
using UnityEngine;

namespace Substrate.Sectors.Elements.Elites
{
    /// <summary>
    /// A sector's elite: one scaled, upgraded ship summoned once the sector's phases run out, whose
    /// death opens extraction. The sector's <see cref="Phases.PhaseSpineModule"/> owns it and drives
    /// it (Begin, Summon, End), so it is neither a spawner nor a module in the sector's manifest. As the
    /// elite's hull falls through its profile's escalation steps, it calls escorts and stacks heavier
    /// fire. Its ships come from the sector's faction: the elite flies the signature ship in the elite
    /// skin, escorts the common ship. Its profile is the run row's elite, read at <see cref="Begin"/>.
    /// <see cref="Announced"/> and <see cref="Beaten"/> carry data only, with no display strings.
    /// The arc and its rulings: https://github.com/amindell11/astronomical-home/issues/1318
    /// </summary>
    public sealed class EliteEncounter : MonoBehaviour, IEliteStatus, ISectorRowReader
    {
        private const float EscortDistanceInEliteRadii = 3f;

        [Header("Ships")]
        [Tooltip("The sector's faction. The elite flies its signature ship in the elite skin; escorts fly its common " +
                 "ship with the faction's kit.")]
        [SerializeField] private SectorFaction faction;
        [Tooltip("The elite's pilot.")]
        [SerializeField] private Commander pilot;
        [Tooltip("The escorts' pilot.")]
        [SerializeField] private Commander escortPilot;
        [SerializeField] private int team = 1;

        [Header("Placement")]
        [Tooltip("Plane distance from the hero the elite arrives at.")]
        [SerializeField] private float spawnRadius = 55f;
        [Tooltip("A candidate point with an asteroid within this radius, times the elite's size, is passed over. " +
                 "Escorts use it unscaled.")]
        [SerializeField] private float clearanceRadius = 6f;
        [Tooltip("Random angles tried for a clear point; the last one tried is used when none is clear.")]
        [SerializeField, Min(1)] private int placementAttempts = 8;

        private readonly List<Ship> escorts = new();
        private EliteProfile profile;
        private IUnitService units;
        private IObstacleField field;
        private Ship hero;
        private Ship elite;
        private LullModStack stack;
        private EliteMoment moment;
        private int sectorIndex;
        private int nextStep;
        private bool summoned;

        public event Action<EliteMoment> Announced;
        public event Action<EliteMoment> Beaten;

        public bool IsBeaten { get; private set; }

        public string Name => profile.DisplayName;
        public bool Present => elite && elite.gameObject.activeInHierarchy;
        public float HullPct => Present ? elite.HealthPct : 0f;
        public float ShieldPct => Present ? elite.ShieldPct : 0f;

        public void CheckRow(SectorEntry row)
        {
            if (!row.elite)
                throw new InvalidOperationException($"EliteEncounter on '{name}' reads the row's elite, and the row names none.");
        }

        public void Begin(SectorBuildContext ctx)
        {
            profile = ctx.Row.elite;
            if (!profile || !faction || !pilot || !escortPilot)
                throw new InvalidOperationException(
                    $"EliteEncounter on '{name}' needs the row's elite, a sector faction, and elite and escort pilots wired.");
            try
            {
                profile.Validate();
            }
            catch (InvalidOperationException invalid)
            {
                throw new InvalidOperationException($"EliteEncounter on '{name}': {invalid.Message}", invalid);
            }

            ResetState();
            units = ctx.Units;
            field = ctx.Field;
            hero = ctx.Hero;
            sectorIndex = ctx.SectorIndex;
        }

        public void Summon()
        {
            if (summoned)
                throw new InvalidOperationException($"EliteEncounter on '{name}' already summoned its elite this sector.");
            if (!hero)
                throw new InvalidOperationException($"EliteEncounter on '{name}' places its elite around the hero, and the sector has none.");
            summoned = true;

            var center = GamePlane.WorldPointToPlane(hero.transform.position);
            var position = ClearPoint(center, spawnRadius, clearanceRadius * profile.Scale.Size);
            stack = new LullModStack();
            foreach (var mod in profile.Mods) stack.Add(mod);
            var ship = faction.Faction.EliteShip;
            elite = units.SpawnShip(ship, pilot, team, position, FacingHero(position), field,
                new ShipLoadout(ship, profile.Primary, profile.Secondary), profile.Scale, stack);
            faction.Faction.Dress(ship, elite, elite: true);
            elite.Damage.OnDeath += OnEliteDeath;
            moment = new EliteMoment(sectorIndex, faction.Faction.id, elite.Id);
            Announced?.Invoke(moment);
        }

        public void End()
        {
            if (elite)
            {
                elite.Damage.OnDeath -= OnEliteDeath;
                units.DespawnShip(elite);
            }
            foreach (var ship in escorts) units.DespawnShip(ship);
            ResetState();
            units = null;
            field = null;
            hero = null;
        }

        private void ResetState()
        {
            escorts.Clear();
            elite = null;
            stack = null;
            nextStep = 0;
            summoned = false;
            IsBeaten = false;
        }

        private void Update()
        {
            if (!Present) return;
            var steps = profile.Escalation;
            while (nextStep < steps.Length && elite.HealthPct <= steps[nextStep].hull)
                Escalate(steps[nextStep++]);
        }

        private void Escalate(EscalationStep step)
        {
            var center = GamePlane.WorldPointToPlane(elite.transform.position);
            var distance = EscortDistanceInEliteRadii * elite.ShipRadius;
            var common = faction.Faction.common.ship;
            for (var i = 0; i < step.escorts; i++)
            {
                var position = ClearPoint(center, distance, clearanceRadius);
                var escort = units.SpawnShip(common, escortPilot, team, position, FacingHero(position), field,
                    new ShipLoadout(common, faction.Faction.KitFor(common), null));
                faction.Faction.Dress(common, escort, elite: false);
                escorts.Add(escort);
            }
            if (step.fireMods == null) return;
            foreach (var mod in step.fireMods) stack.Add(mod);
        }

        private void OnEliteDeath(ShipId victim, DamageInfo killingBlow)
        {
            IsBeaten = true;
            Beaten?.Invoke(moment);
        }

        private Vector3 ClearPoint(Vector2 center, float distance, float clearance)
        {
            var position = Vector3.zero;
            for (var attempt = 0; attempt < placementAttempts; attempt++)
            {
                var angle = UnityEngine.Random.Range(0f, 2f * Mathf.PI);
                var outward = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                var planePoint = center + outward * distance;
                position = GamePlane.PlanePointToWorld(planePoint);
                if (Placement.ClearPoint.IsClear(field, planePoint, clearance)) break;
            }
            return position;
        }

        private Quaternion FacingHero(Vector3 position)
        {
            var toHero = GamePlane.WorldPointToPlane(hero.transform.position) - GamePlane.WorldPointToPlane(position);
            return GamePlane.PlanePose(GamePlane.Normal, GamePlane.PlaneDirToWorld(toHero.normalized));
        }

#if UNITY_EDITOR
        internal void Configure(SectorFaction faction, Commander pilot, Commander escortPilot)
        {
            this.faction = faction;
            this.pilot = pilot;
            this.escortPilot = escortPilot;
        }
#endif
    }
}
