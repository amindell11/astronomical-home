using Substrate.Sessions;
using UnityEngine;
using Substrate.Services.Units;
using Substrate.Services.Objectives;
using Game;

namespace Tests.PlayMode.Common
{
    /// <summary>Composition root for host-less session tests: adds the two services a session requires to <paramref name="root"/> and constructs the session the way <c>GameSessionHost</c> does, with no host above it.</summary>
    public static class TestSession
    {
        public static Session Create(GameObject root, SessionProfile profile)
        {
            var units = root.AddComponent<UnitService>();
            var objectives = root.AddComponent<ObjectiveService>();
            return new Session(profile, root.transform, units, objectives);
        }
    }
}
