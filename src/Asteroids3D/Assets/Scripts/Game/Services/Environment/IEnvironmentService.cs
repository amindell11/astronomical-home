using System.Collections;
using UnityEngine;
using World;

namespace Game.Services
{
    public interface IEnvironmentService
    {
        WorldRoot World { get; }
        Transform WorldFollowerTransform { get; }

        /// <summary>Make the named scene the active (lighting) scene, additively loading it and unloading the prior locale; no-op when empty (inherit boot lighting) or already applied.</summary>
        IEnumerator ApplyLocaleAsync(string localeSceneName);

        /// <summary>Restore the boot scene as active and unload the applied locale, if any.</summary>
        IEnumerator RestoreBootEnvironmentAsync();

        void SpawnWorld(WorldRoot prefab);
        void AdoptWorld(WorldRoot existing);
        void Clear();
    }
}
