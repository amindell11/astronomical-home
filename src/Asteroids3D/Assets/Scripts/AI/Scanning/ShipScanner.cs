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
    }

    public class ShipScanner : IScanner<ShipScanResult>
    {
        private readonly Ship self;
        private readonly Transform origin;
        private readonly SphereSensor sensor;
        private readonly Ship[] shipBuffer;
        private ShipScanResult lastResult;

        public ShipScanResult LastResult => lastResult;

        public ShipScanner(Ship ship, float scanRadius, int bufferSize = 32)
        {
            self = ship;
            origin = ship.transform;
            sensor = new SphereSensor(origin, scanRadius, LayerIds.Mask(LayerIds.Ship), bufferSize);
            shipBuffer = new Ship[bufferSize];
            lastResult = new ShipScanResult { ships = shipBuffer, count = 0 };
        }

        public ShipScanResult Scan()
        {
            if (!self) { lastResult = ShipScanResult.Empty; return lastResult; }
            var hitCount = sensor.Detect();
            var shipCount = 0;
            for (var i = 0; i < hitCount && shipCount < shipBuffer.Length; i++)
            {
                var col = sensor.Buffer[i];
                var ship = col ? col.attachedRigidbody?.GetComponent<Ship>() : null;
                if (ship && ship != self) shipBuffer[shipCount++] = ship;
            }
            lastResult = new ShipScanResult { ships = shipBuffer, count = shipCount };
            return lastResult;
        }

        public IEnumerable<Ship> Ships => lastResult.ships.Take(lastResult.count);
        public IEnumerable<Ship> Friends => Ships.Where(s => self.IsFriendly(s));
        public IEnumerable<Ship> Enemies => Ships.Where(s => !self.IsFriendly(s));
        public int FriendCount => Friends.Count();
        public int EnemyCount => Enemies.Count();
        public Ship NearestEnemy => Enemies.OrderBy(e => Vector3.Distance(origin.position, e.transform.position)).FirstOrDefault();
        public float NearestThreatDistance(Ship exclude = null) => Enemies.Where(e => e != exclude).Select(e => Vector3.Distance(origin.position, e.transform.position)).DefaultIfEmpty(float.MaxValue).Min();
    }
}
