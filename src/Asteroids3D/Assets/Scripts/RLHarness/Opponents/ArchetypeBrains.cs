using System;
using AI;
using Ships;
using UnityEngine;

namespace Game.RLHarness
{
    /// <summary>The one archetype → configured brain mapping, shared by <see cref="OpponentRoster"/>'s per-episode draw and the editor-authored <see cref="LiveArchetypeBrain"/>. Aggressor and Kiter share the hold-range law and differ only in the range they are handed. Takes the host object rather than the commander because the live path parks its archetype on a child, where no commander will find it.</summary>
    public static class ArchetypeBrains
    {
        public static Brain Attach(GameObject host, OpponentArchetype archetype, in OpponentDraw shape, Ship target,
            int jukeSeed, Vector2 borderCenter, float borderRadius,
            ArchetypeDrive drive = ArchetypeDrive.Production)
        {
            switch (archetype)
            {
                case OpponentArchetype.Aggressor:
                case OpponentArchetype.Kiter:
                    var holdRange = host.AddComponent<HoldRangeFireBrain>();
                    holdRange.Configure(target, shape.desiredRange, shape.speedFraction,
                        borderCenter, borderRadius, drive);
                    return holdRange;
                case OpponentArchetype.Evader:
                    var evader = host.AddComponent<EvaderBrain>();
                    evader.Configure(target, shape.speedFraction, shape.jukePeriod, jukeSeed,
                        borderCenter, borderRadius, drive);
                    return evader;
                case OpponentArchetype.Orbiter:
                    var orbiter = host.AddComponent<OrbiterBrain>();
                    orbiter.Configure(target, shape.orbitRadius, shape.orbitDirection, shape.speedFraction,
                        borderCenter, borderRadius, drive);
                    return orbiter;
                case OpponentArchetype.Dummy:
                    return host.AddComponent<DummyBrain>();
                default:
                    throw new ArgumentOutOfRangeException(nameof(archetype), archetype, null);
            }
        }
    }
}
