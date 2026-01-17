using AI.Context;
using Ships;
using UnityEngine;
using Utils;

namespace AI.Computers
{
    public class ShipScanner
    {
        private readonly Ship ship;
        private readonly ShipInfo shipInfo;
        private readonly float scanRadius;

        private ScanResult cachedScan;
        private int scanFrame = -1;

        public ShipScanner(Ship ship, ShipInfo shipInfo, float scanRadius)
        {
            this.ship = ship;
            this.shipInfo = shipInfo;
            this.scanRadius = scanRadius;
        }

        public ScanResult LastScan => GetCachedScan();

        private ScanResult GetCachedScan()
        {
            var frame = Time.frameCount;
            if (scanFrame == frame) return cachedScan;
            cachedScan = ScanNearby();
            scanFrame = frame;
            return cachedScan;
        }

        public ScanResult ScanNearby(Ship excludeFromThreat = null)
        {
            var result = new ScanResult { nearestThreatDistance = float.MaxValue };
            if (!ship) return result;

            var selfPos = shipInfo.Pos3D;
            var colliders = PhysicsBuffers.GetColliderBuffer();
            var hitCount = Physics.OverlapSphereNonAlloc(selfPos, scanRadius, colliders, LayerIds.Mask(LayerIds.Ship));

            var nearestDistance = float.MaxValue;

            for (var i = 0; i < hitCount; i++)
            {
                var col = colliders[i];
                if (!col) continue;

                var otherShip = col.attachedRigidbody?.GetComponent<Ship>();
                if (!otherShip || otherShip == ship) continue;

                var distance = Vector3.Distance(selfPos, otherShip.transform.position);

                if (ship.IsFriendly(otherShip))
                    result.friendCount++;
                else
                {
                    result.enemyCount++;

                    if (distance < nearestDistance)
                    {
                        nearestDistance = distance;
                        result.nearestEnemy = otherShip;
                    }

                    if (otherShip != excludeFromThreat && distance < result.nearestThreatDistance)
                        result.nearestThreatDistance = distance;
                }
            }

            if (!excludeFromThreat || result.nearestEnemy != excludeFromThreat) return result;
            var enemyDist = Vector3.Distance(selfPos, excludeFromThreat.transform.position);
            if (enemyDist < result.nearestThreatDistance)
                result.nearestThreatDistance = enemyDist;

            return result;
        }

        public Ship FindNearestEnemy()
        {
            return ScanNearby().nearestEnemy;
        }

        public struct ScanResult
        {
            public int enemyCount;
            public int friendCount;
            public float nearestThreatDistance;
            public Ship nearestEnemy;
        }
    }
}
