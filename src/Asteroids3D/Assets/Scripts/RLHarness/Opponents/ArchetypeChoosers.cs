using System;
using AI;
using Ships;
using UnityEngine;

namespace Game.RLHarness
{
    /// <summary>The one archetype → configured chooser mapping, shared by <see cref="OpponentRoster"/>'s per-episode draw and the editor-authored <see cref="LiveArchetypeChooser"/>. Aggressor and Kiter share the hold-range law and differ only in the range they are handed.</summary>
    public static class ArchetypeChoosers
    {
        public static IIntentChooser Create(OpponentArchetype archetype, in OpponentDraw shape, Ship target,
            float projectileSpeed, int jukeSeed, Vector2 borderCenter, float borderRadius,
            ArchetypeDrive drive = ArchetypeDrive.Production)
        {
            switch (archetype)
            {
                case OpponentArchetype.Aggressor:
                case OpponentArchetype.Kiter:
                    var holdRange = new HoldRangeFireChooser();
                    holdRange.Configure(target, shape.desiredRange, shape.speedFraction, projectileSpeed,
                        borderCenter, borderRadius, drive);
                    return holdRange;
                case OpponentArchetype.Evader:
                    var evader = new EvaderChooser();
                    evader.Configure(target, shape.speedFraction, shape.jukePeriod, jukeSeed,
                        borderCenter, borderRadius, drive);
                    return evader;
                case OpponentArchetype.Orbiter:
                    var orbiter = new OrbiterChooser();
                    orbiter.Configure(target, shape.orbitRadius, shape.orbitDirection, shape.speedFraction,
                        projectileSpeed, borderCenter, borderRadius, drive);
                    return orbiter;
                case OpponentArchetype.Dummy:
                    return new DummyChooser();
                default:
                    throw new ArgumentOutOfRangeException(nameof(archetype), archetype, null);
            }
        }
    }
}
