using System;
using System.Collections.Generic;
using AI;
using Ships;
using Ships.Command;
using UnityEngine;
using ShipFactory = Ships.Factory;

namespace Game.Services
{
    public class UnitService : MonoBehaviour, IUnitService
    {
        private struct PendingRespawn
        {
            public ShipId ship;
            public Vector2 pos;
            public float rotation;
            public float respawnTime;
        }

        private readonly List<Ship> spawnedShips = new();
        private readonly List<PendingRespawn> pendingRespawns = new();
        private int nextAgentIndex;
        private ArenaContext arena;
        public IShipRegistry Registry => ActiveRegistry;
        public ShipRegistry ActiveRegistry { get; } = new();

        /// <summary>Assign the arena handle wired into each ship; one-shot so a stray re-compose can't swap the arena out from under live ships.</summary>
        public void SetArena(ArenaContext context)
        {
            if (arena != null && !ReferenceEquals(arena, context))
                throw new InvalidOperationException("UnitService arena is already set to a different ArenaContext.");
            arena = context;
        }

        public event Action<Ship> OnShipSpawned;

        public Ship SpawnShip(
            Ship template,
            Commander commander,
            int team,
            Vector3 position,
            Quaternion rotation)
        {
            if (!template)
                throw new ArgumentNullException(nameof(template));

            var ship = ShipFactory.CreateShip(
                template, commander, team, NextDecisionSeed(team),
                position, rotation,
                postInitialize: WireShipDependencies);

            ship.transform.SetParent(transform, true);
            ActiveRegistry.ActiveShips.Add(ship);
            spawnedShips.Add(ship);
            OnShipSpawned?.Invoke(ship);
            return ship;
        }

        public Ship AdoptShip(Ship ship)
        {
            if (!ship)
                return null;

            // Re-home from the sector to the arena root so lifetime/Clear() matches a spawned ship.
            ship.transform.SetParent(transform, true);

            var commander = ship.GetComponentInChildren<Commander>(true);
            if (commander)
                ship.AdoptCommander(commander);

            ship.Initialize(ship.teamNumber, NextDecisionSeed(ship.teamNumber));

            ActiveRegistry.ActiveShips.Add(ship);
            spawnedShips.Add(ship);
            WireShipDependencies(ship);
            OnShipSpawned?.Invoke(ship);
            return ship;
        }

        public void DespawnShip(Ship ship)
        {
            // The session player is never passed here, so it survives a sector restart.
            if (!ship) return;
            ActiveRegistry.ActiveShips.Remove(ship);
            spawnedShips.Remove(ship);
            pendingRespawns.RemoveAll(p => p.ship == ship.Id);
            UnityEngine.Object.Destroy(ship.gameObject);
        }

        public void Clear()
        {
            for (var i = 0; i < spawnedShips.Count; i++)
            {
                var ship = spawnedShips[i];
                if (!ship) continue;
                ActiveRegistry.ActiveShips.Remove(ship);
                UnityEngine.Object.Destroy(ship.gameObject);
            }

            spawnedShips.Clear();
            pendingRespawns.Clear();
            // Restart the agent index at the episode-reset boundary so the next episode re-derives the same decision seeds.
            nextAgentIndex = 0;
            // Never Dispose ActiveRegistry here — that unsubscribes OnAdd/OnRemove and permanently breaks the registry for later runs.
        }

        private void OnDestroy()
        {
            ActiveRegistry.Dispose();
        }

        private int NextDecisionSeed(int team) => DeriveDecisionSeed(team, nextAgentIndex++);

        /// <summary>Per-agent decision seed derived from the deterministic spawn order (not <c>GetInstanceID</c>) so a reconstructed episode replays identically; distinct per ship, nonzero.</summary>
        private static int DeriveDecisionSeed(int team, int agentIndex)
        {
            const int arenaBaseSeed = 0; // 0 until S1b supplies per-arena seeds
            return new SeedScope(arenaBaseSeed).Derive((uint)team).Derive((uint)agentIndex).ToSeed();
        }

        /// <summary>Idempotent world-state wiring; see <see cref="IUnitService.WireShipDependencies"/>.</summary>
        public void WireShipDependencies(Ship ship)
        {
            if (!ship) return;
            if (arena == null)
                throw new InvalidOperationException("UnitService.SetArena must be called before wiring ships.");
            ship.Targeting?.SetRegistry(arena.Registry);
            if (ship.Commander is AICommander aiCommander)
                aiCommander.SetArena(arena);
        }

        public void RespawnShip(ShipId id, Vector2 pos, float rotation)
        {
            if(!ActiveRegistry.TryGetShip(id, out var ship)) return;
            ship.transform.position = GamePlane.PlanePointToWorld(pos);
            ship.transform.rotation = GamePlane.Rotation * Quaternion.AngleAxis(rotation, Vector3.forward);
            ship.ResetShip();
        }

        public void CancelPendingRespawns()
        {
            pendingRespawns.Clear();
        }

        public void WaitAndRespawnShip(ShipId ship, Vector2 pos, float rotation, float delay)
        {
            pendingRespawns.Add(new PendingRespawn
            {
                ship = ship,
                pos = pos,
                rotation = rotation,
                respawnTime = Time.time + delay
            });
        }

        private void Update()
        {
            for (var i = pendingRespawns.Count - 1; i >= 0; i--)
            {
                if (Time.time < pendingRespawns[i].respawnTime) continue;

                var pending = pendingRespawns[i];
                pendingRespawns.RemoveAt(i);
                RespawnShip(pending.ship, pending.pos, pending.rotation);
            }
        }
    }
}
