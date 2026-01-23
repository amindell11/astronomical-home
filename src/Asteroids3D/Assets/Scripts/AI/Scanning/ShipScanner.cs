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
        public Ship[] ships;
        public int count;
        public static ShipScanResult Empty => new() { ships = System.Array.Empty<Ship>(), count = 0 };
        
        public IEnumerable<Ship> Ships => ships.Take(count);
        public IEnumerable<Ship> Friends(Ship self) => Ships.Where(self.IsFriendly);
        public IEnumerable<Ship> Enemies(Ship self) => Ships.Where(self.IsHostile);
        public int FriendCount(Ship self) => Friends(self).Count();
        public int EnemyCount(Ship self) => Enemies(self).Count();
        public Ship NearestEnemy(Ship self, Vector3 pos) => Enemies(self).OrderBy(e => Vector3.Distance(pos, e.transform.position)).FirstOrDefault();
    }

    public class ShipScanner : IScanner<ShipScanResult>
    {
        private readonly Ship self;
        private readonly SphereSensor sensor;
        private readonly Ship[] shipBuffer;

        public ShipScanResult LastResult { get; private set; }

        public ShipScanner(Transform origin, float scanRadius, int bufferSize = 32)
        {
            self = origin.GetComponent<Ship>();
            sensor = new SphereSensor(origin, scanRadius, LayerIds.Mask(LayerIds.Ship), bufferSize);
            shipBuffer = new Ship[bufferSize];
            LastResult = new ShipScanResult { ships = shipBuffer, count = 0 };
        }

        public ShipScanResult Scan()
        {
            if (!self) { LastResult = ShipScanResult.Empty; return LastResult; }
            var hitCount = sensor.Detect();
            var shipCount = 0;
            for (var i = 0; i < hitCount && shipCount < shipBuffer.Length; i++)
            {
                var col = sensor.Buffer[i];
                var ship = col ? col.attachedRigidbody?.GetComponent<Ship>() : null;
                if (ship && ship != self) shipBuffer[shipCount++] = ship;
            }
            LastResult = new ShipScanResult { ships = shipBuffer, count = shipCount };
            return LastResult;
        }
    }
}
