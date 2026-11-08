using System;
using System.Collections;
using System.Collections.Generic;
using Balance;
using Combat.Weapons.Mods;
using Loot;
using Objectives;
using Ships;
using Substrate.Sectors.Elements.Hunt;
using Substrate.Sectors.Elements.Placement;
using Substrate.Services.Objectives;
using UnityEngine;

namespace Substrate.Sectors.Elements.Stations
{
    /// <summary>
    /// A sector's weapon caches: the authored body plus copies of it, as many as the feel asks for. Once the
    /// sector's repair stations show, each cache moves on a tick to its own clear point away from the hero's
    /// start, spaced from the other caches and from those stations, and shows as a find until it goes dark.
    /// The hero's first fly-in claims it: the boost mod joins the run's mods, shown on the HUD, until the
    /// sector tears down, and the cache goes dark. The hunt's alarm darkens unclaimed caches; claimed boosts
    /// stay. A sector with no hunt keeps them live.
    /// The arc and its rulings: https://github.com/amindell11/astronomical-home/issues/1319
    /// </summary>
    public class WeaponCache : SectorModule
    {
        [Tooltip("Cache count.")]
        [Stat, SerializeField] private WeaponCacheFeelProfile feel;
        [Tooltip("The damage mod a claim adds to the run's mods until the sector ends; its amount is the boost.")]
        [Stat, SerializeField] private WeaponMod boost;
        [Tooltip("The fly-in trigger, on or under the body.")]
        [SerializeField] private ExtractionZone zone;
        [Tooltip("The model, zone and lit parts; copied per cache and hidden until placed.")]
        [SerializeField] private GameObject body;
        [Tooltip("The glow and the minimap marker, under the body; hidden when the cache goes dark.")]
        [SerializeField] private GameObject lit;
        [Tooltip("The sector's opening hunt, read for its alarm, which darkens every unclaimed cache; unset for none.")]
        [SerializeField] private SectorHunt hunt;
        [Tooltip("The sector's repair stations, read for their placed points so no cache lands beside one; unset for none.")]
        [SerializeField] private RepairStation stations;
        [Tooltip("The find icon its minimap marker and edge pointer show.")]
        [SerializeField] private Sprite icon;

        [Header("Placement")]
        [Tooltip("Plane distances from the hero's start the caches are placed between.")]
        [SerializeField, Min(0f)] private float distanceMin = 120f;
        [SerializeField, Min(0f)] private float distanceMax = 180f;
        [Tooltip("Least plane distance from a cache to another cache or a repair station.")]
        [SerializeField, Min(0f)] private float spacing = 60f;
        [Tooltip("A candidate point with an asteroid within this radius is passed over.")]
        [SerializeField, Min(0f)] private float clearanceRadius = 35f;
        [SerializeField] private LayerMask asteroidMask = 1 << 8;
        [Tooltip("Random points tried; the last one tried is used when none is clear.")]
        [SerializeField, Min(1)] private int placementAttempts = 8;

        private readonly List<Cache> caches = new();
        private readonly List<GameObject> copies = new();
        private readonly List<Grant> grants = new();
        private Ship hero;
        private IObjectiveService objectives;
        private int sectorIndex;
        private bool placed;

        /// <summary>The hero reached a cache for the first time: station kind, sector index.</summary>
        public event Action<StationKind, int> Arrived;

        /// <summary>The hero claimed a find, on its first arrival: station kind.</summary>
        public event Action<StationKind> FindClaimed;

        public StationKind Kind => StationKind.Cache;

        public IReadOnlyList<Cache> Caches => caches;

        public override IEnumerator Setup(SectorBuildContext ctx)
        {
            if (!feel || !boost || !zone || !body || !lit || !icon || ctx.Objectives == null
                || !zone.transform.IsChildOf(body.transform) || !lit.transform.IsChildOf(body.transform))
                throw new InvalidOperationException(
                    $"WeaponCache on '{name}' needs its feel profile, boost mod, icon, objective service, body, and a zone and lit parts under the body.");
            if (boost.Field != WeaponModField.Damage)
                throw new InvalidOperationException(
                    $"WeaponCache on '{name}' grants a damage boost, but its mod '{boost.name}' changes {boost.Field.Label()}.");

            hero = ctx.Hero;
            objectives = ctx.Objectives;
            sectorIndex = ctx.SectorIndex;
            placed = false;
            ClearCaches();
            body.SetActive(false);
            for (var i = 0; i < feel.cachesPerSector; i++)
            {
                var cache = i == 0 ? new Cache(body, zone, lit) : Copy();
                cache.Reset(hero);
                caches.Add(cache);
            }
            yield break;
        }

        public override IEnumerator Teardown(SectorBuildContext ctx)
        {
            ClearCaches();
            body.SetActive(false);
            hero = null;
            objectives = null;
            yield break;
        }

        private void Update()
        {
            if (!hero || !hero.gameObject.activeInHierarchy) return;
            Tick();
        }

        private void Tick()
        {
            if (!placed)
            {
                if (StationsPlaced()) Place();
                return;
            }

            var alarmed = hunt && hunt.Alarmed;
            foreach (var cache in caches)
            {
                if (cache.Spent) continue;
                if (alarmed)
                {
                    cache.GoDark();
                    continue;
                }
                if (cache.Zone.IsPlayerInZone) Claim(cache);
            }
        }

        // A station's body shows once its module has placed it, so the caches read final points.
        private bool StationsPlaced()
        {
            if (!stations) return true;
            foreach (var station in stations.Stations)
                if (!station.Body.activeSelf) return false;
            return true;
        }

        private void Place()
        {
            var start = GamePlane.WorldPointToPlane(hero.transform.position);
            var taken = new List<Vector3>();
            if (stations)
                foreach (var station in stations.Stations)
                    taken.Add(station.Position);
            foreach (var cache in caches)
            {
                var point = ClearPoint.Around(start, distanceMin, distanceMax, clearanceRadius, asteroidMask,
                    placementAttempts, taken, spacing);
                taken.Add(point);
                cache.Show(point, objectives.MarkFind(cache.Body.transform, icon));
            }
            placed = true;
        }

        private void Claim(Cache cache)
        {
            var mods = hero.Mods ?? throw new InvalidOperationException(
                $"WeaponCache on '{name}' boosts the hero's run mods, and hero '{hero.name}' has none bound.");
            cache.GoDark();
            mods.Add(boost);
            grants.Add(new Grant(mods, objectives.ShowBoost(boost.Effect)));
            Arrived?.Invoke(Kind, sectorIndex);
            FindClaimed?.Invoke(Kind);
        }

        private Cache Copy()
        {
            var copy = Instantiate(body, body.transform.parent);
            copies.Add(copy);
            return new Cache(copy, Counterpart(zone.transform, copy.transform).GetComponent<ExtractionZone>(),
                Counterpart(lit.transform, copy.transform).gameObject);
        }

        private Transform Counterpart(Transform part, Transform copy) =>
            part == body.transform ? copy : Counterpart(part.parent, copy).GetChild(part.GetSiblingIndex());

        private void ClearCaches()
        {
            foreach (var grant in grants)
            {
                grant.Mods.Remove(boost);
                grant.Shown.Close();
            }
            grants.Clear();
            foreach (var cache in caches)
            {
                cache.Zone.Disarm();
                cache.Unmark();
            }
            caches.Clear();
            foreach (var copy in copies)
            {
                copy.SetActive(false);
                Destroy(copy);
            }
            copies.Clear();
        }

        private readonly struct Grant
        {
            public readonly LullModStack Mods;
            public readonly SectorBoost Shown;

            public Grant(LullModStack mods, SectorBoost shown)
            {
                Mods = mods;
                Shown = shown;
            }
        }

        /// <summary>One placed cache of the sector's set, claimed at most once.</summary>
        public sealed class Cache
        {
            internal readonly GameObject Body;
            internal readonly ExtractionZone Zone;
            internal readonly GameObject Lit;
            private FindMarker find;

            internal Cache(GameObject body, ExtractionZone zone, GameObject lit)
            {
                Body = body;
                Zone = zone;
                Lit = lit;
            }

            /// <summary>Claimed, or darkened by the alarm.</summary>
            public bool Spent { get; private set; }

            public Vector3 Position => Body.transform.position;

            internal void Reset(Ship hero)
            {
                Spent = false;
                Zone.BindPlayer(hero ? hero.Body : null);
                Zone.Disarm();
                Body.SetActive(false);
                Lit.SetActive(true);
            }

            internal void Show(Vector3 position, FindMarker marker)
            {
                Body.transform.position = position;
                Body.SetActive(true);
                Zone.Arm();
                find = marker;
            }

            internal void GoDark()
            {
                Spent = true;
                Zone.Disarm();
                Lit.SetActive(false);
                Unmark();
            }

            internal void Unmark()
            {
                find?.Close();
                find = null;
            }
        }

#if UNITY_EDITOR
        internal void Configure(WeaponCacheFeelProfile feel, WeaponMod boost, float distance, SectorHunt hunt = null,
            float spacing = 0f, RepairStation stations = null)
        {
            this.feel = feel;
            this.boost = boost;
            this.hunt = hunt;
            this.spacing = spacing;
            this.stations = stations;
            distanceMin = distanceMax = distance;
        }
#endif
    }
}
