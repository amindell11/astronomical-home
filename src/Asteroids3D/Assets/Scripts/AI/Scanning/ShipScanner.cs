using System.Collections.Generic;
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

        public readonly ShipId NearestEnemy(ShipId self, Vector3 pos, IShipRegistry registry)
        {
            if (registry == null) return ShipId.Invalid;
            var nearestId = ShipId.Invalid;
            var nearestDist = float.MaxValue;
            for (var i = 0; i < count; i++)
            {
                var id = shipIds[i];
                if (!registry.IsHostile(self, id)) continue;
                if (!registry.TryGetShip(id, out var ship)) continue;
                var dist = Vector3.Distance(pos, ship.transform.position);
                if (!(dist < nearestDist)) continue;
                nearestDist = dist;
                nearestId = id;
            }
            return nearestId;
        }

        public readonly int FriendCount(ShipId self, IShipRegistry registry)
        {
            if (registry == null) return 0;

            var friendCount = 0;
            for (var i = 0; i < count; i++)
            {
                if (registry.IsFriendly(self, shipIds[i]))
                    friendCount++;
            }

            return friendCount;
        }

        public readonly int EnemyCount(ShipId self, IShipRegistry registry)
        {
            if (registry == null) return 0;

            var enemyCount = 0;
            for (var i = 0; i < count; i++)
            {
                if (registry.IsHostile(self, shipIds[i]))
                    enemyCount++;
            }

            return enemyCount;
        }
    }

    public class ShipScanner
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
