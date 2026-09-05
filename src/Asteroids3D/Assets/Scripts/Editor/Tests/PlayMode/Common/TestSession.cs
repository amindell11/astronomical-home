using System;
using Damage;
using Game.Sectors;
using Game.Services;
using Game.Sessions;
using Player;
using Ships;
using UnityEngine;

namespace Tests.PlayMode.Common
{
    /// <summary>Composition root for host-less session tests: adds the two services a session requires to <paramref name="root"/> and constructs the session the way <c>GameSessionHost</c> does, with no host above it.</summary>
    public static class TestSession
    {
        public static Session Create(GameObject root, SessionProfile profile, SessionRig rig = null,
            Action<SectorResult> onSectorComplete = null, Action<ShipId, DamageInfo> onPlayerDeath = null)
        {
            var units = root.AddComponent<UnitService>();
            var objectives = root.AddComponent<ObjectiveService>();
            return new Session(profile, root.transform, units, objectives, rig, onSectorComplete, onPlayerDeath);
        }
    }
}
