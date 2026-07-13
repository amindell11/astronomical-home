using System.Collections;
using UnityEngine;
using World;

namespace Game.Services
{
    public interface IEnvironmentService
    {
        WorldRoot World { get; }
        Transform WorldFollowerTransform { get; }

        /// <summary>
        /// Make <paramref name="localeSceneName"/> the active (lighting) scene, loading it additively
        /// first and unloading any previously-applied locale. No-op when the name is empty (inherit
        /// boot lighting) or already the active locale.
        /// </summary>
        IEnumerator ApplyLocaleAsync(string localeSceneName);

        /// <summary>Restore the boot scene as active and unload the applied locale, if any.</summary>
        IEnumerator RestoreBootEnvironmentAsync();

        void SpawnWorld(WorldRoot prefab);
        void AdoptWorld(WorldRoot existing);
        void Clear();
    }
}
