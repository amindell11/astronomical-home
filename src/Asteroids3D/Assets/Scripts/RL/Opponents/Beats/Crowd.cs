using AI;
using Ships;
using Ships.Registry;
using UnityEngine;

namespace RL.Opponents.Beats
{
    /// <summary>The allies in a pilot's ship scan that are attacking its target, read off their own brains through the registry. Allies on other brains report nothing.</summary>
    internal readonly struct Crowd
    {
        public readonly int attackers;
        /// <summary>The attackers' summed offsets from the target.</summary>
        public readonly Vector2 offset;

        private Crowd(int attackers, Vector2 offset)
        {
            this.attackers = attackers;
            this.offset = offset;
        }

        internal static Crowd Read(Scout scout, ShipId self, Ship target)
        {
            var scan = scout.ShipScan;
            var registry = scout.Registry;
            if (!scan.HasValue || registry == null) return default;

            var targetPos = target.Kinematics.pos;
            var attackers = 0;
            var offset = Vector2.zero;
            var ids = scan.Value;
            for (var i = 0; i < ids.count; i++)
            {
                var id = ids.shipIds[i];
                if (!registry.IsFriendly(self, id) || !registry.TryGetShip(id, out var ship) || !ship) continue;
                var commander = ship.Commander as AICommander;
                if (!commander) continue;
                var ally = commander.Brain as BeatBrain;
                if (!ally || !ally.Attacking || ally.TargetId != target.Id) continue;
                attackers++;
                offset += ship.Kinematics.pos - targetPos;
            }
            return new Crowd(attackers, offset);
        }
    }
}
