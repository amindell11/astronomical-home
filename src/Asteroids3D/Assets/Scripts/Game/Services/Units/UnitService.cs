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
        public IShipRegistry Registry => ActiveRegistry;
        public ShipRegistry ActiveRegistry { get; } = new();

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

            ActiveRegistry.ActiveShips.Add(ship);
            spawnedShips.Add(ship);
            OnShipSpawned?.Invoke(ship);
            return ship;
        }

        public Ship AdoptShip(Ship ship)
        {
            if (!ship)
                return null;

            // Detach from the sector so lifetime/Clear() matches a spawned ship.
            ship.transform.SetParent(null, true);

            // Use the pilot authored as a child of the ship, if present.
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
            // Producer-owned teardown: destroy one service-owned ship (a spawner product or an adopted
            // ship) without touching the rest. The session player is never passed here, so it survives
            // a sector restart. Also drop any queued revive so it can't fire on the destroyed ship.
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
            // Clear is the episode-reset boundary: restart the agent index so the next episode
            // re-derives the same per-agent decision seeds (replay without reconstructing the service).
            nextAgentIndex = 0;
            // Do NOT call ActiveRegistry.Dispose() here — that unsubscribes the OnAdd/OnRemove
            // callbacks, which permanently breaks the registry for subsequent runs.
            // Ships are already fully unregistered via ActiveShips.Remove() above.
        }

        private void OnDestroy()
        {
            ActiveRegistry.Dispose();
        }

        private int NextDecisionSeed(int team) => DeriveDecisionSeed(team, nextAgentIndex++);

        /// <summary>
        /// Stable per-agent decision seed derived from the deterministic spawn order, so a
        /// reconstructed episode replays identically (unlike a <c>GetInstanceID</c>-derived seed).
        /// Distinct per ship, nonzero.
        /// </summary>
        private static int DeriveDecisionSeed(int team, int agentIndex)
        {
            const int arenaBaseSeed = 0; // 0 until S1b supplies per-arena seeds
            return new SeedScope(arenaBaseSeed).Derive((uint)team).Derive((uint)agentIndex).ToSeed();
        }

        /// <summary>Idempotent world-state wiring; see <see cref="IUnitService.WireShipDependencies"/>.</summary>
        public void WireShipDependencies(Ship ship)
        {
            if (!ship) return;
            ship.Targeting?.SetRegistry(ActiveRegistry);
            if (ship.Commander is AICommander aiCommander)
                aiCommander.SetRegistry(ActiveRegistry);
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
