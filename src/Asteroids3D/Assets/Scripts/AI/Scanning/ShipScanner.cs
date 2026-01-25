using System.Collections.Generic;
using System.Linq;
using AI.Scanning.Sensors;
using Ships;
using UnityEngine;
using Utils;

namespace AI.Scanning
{
    public struct ShipScanResult
    {
        public ShipId[] shipIds;
        public int count;
        public static ShipScanResult Empty => new() { shipIds = System.Array.Empty<ShipId>(), count = 0 };

        public IEnumerable<ShipId> ShipIds => shipIds.Take(count);

        public IEnumerable<ShipId> Friends(ShipId self, IShipRegistry registry) =>
            registry == null ? Enumerable.Empty<ShipId>() : ShipIds.Where(id => registry.IsFriendly(self, id));

        public IEnumerable<ShipId> Enemies(ShipId self, IShipRegistry registry) =>
            registry == null ? Enumerable.Empty<ShipId>() : ShipIds.Where(id => registry.IsHostile(self, id));

        public int FriendCount(ShipId self, IShipRegistry registry) => registry == null ? 0 : Friends(self, registry).Count();
        public int EnemyCount(ShipId self, IShipRegistry registry) => registry == null ? 0 : Enemies(self, registry).Count();

        public ShipId NearestEnemy(ShipId self, Vector3 pos, IShipRegistry registry)
        {
            if (registry == null) return ShipId.Invalid;
            var nearestId = ShipId.Invalid;
            var nearestDist = float.MaxValue;
            foreach (var id in Enemies(self, registry))
            {
                if (!registry.TryGetShip(id, out var ship)) continue;
                var dist = Vector3.Distance(pos, ship.transform.position);
                if (!(dist < nearestDist)) continue;
                nearestDist = dist;
                nearestId = id;
            }
            return nearestId;
        }
    }

    public class ShipScanner : IScanner<ShipScanResult>
    {
        private readonly ShipId selfId;
        private readonly IShipRegistry registry;
        private readonly SphereSensor sensor;
        private readonly ShipId[] shipIdBuffer;

        public ShipScanResult LastResult { get; private set; }

        public ShipScanner(Transform origin, float scanRadius, ShipId selfId, IShipRegistry registry, int bufferSize = 32)
        {
            this.selfId = selfId;
            this.registry = registry;
            sensor = new SphereSensor(origin, scanRadius, LayerIds.Mask(LayerIds.Ship), bufferSize);
            shipIdBuffer = new ShipId[bufferSize];
            LastResult = new ShipScanResult { shipIds = shipIdBuffer, count = 0 };
        }

        public ShipScanResult Scan()
        {
            if (!selfId.IsValid || registry == null) { LastResult = ShipScanResult.Empty; return LastResult; }
            var hitCount = sensor.Detect();
            var shipCount = 0;
            for (var i = 0; i < hitCount && shipCount < shipIdBuffer.Length; i++)
            {
                var col = sensor.Buffer[i];
                if (!col) continue;
                if (!registry.TryGetShipId(col, out var id)) continue;
                if (id != selfId) shipIdBuffer[shipCount++] = id;
            }
            LastResult = new ShipScanResult { shipIds = shipIdBuffer, count = shipCount };
            return LastResult;
        }
    }
}
