using System;
using System.Collections;
using Game.Bootstrap;
using Game.Services;
using Ships;
using UnityEngine;

namespace Game.Sectors
{
    public abstract class SectorManager : MonoBehaviour, ISectorManager
    {
        public event Action<SectorResult> OnSectorComplete;

        protected IGameServices Services { get; private set; }
        protected SectorConfigSO Config { get; private set; }
        protected bool IsSetUp { get; private set; }
        protected ShipRespawnRunner RespawnRunner { get; private set; }
        public virtual Ship PresentationShip => null;

        public void Initialize(IGameServices services, SectorConfigSO config, ShipRespawnRunner respawnRunner = null)
        {
            Services = services ?? throw new ArgumentNullException(nameof(services));
            Config = config ?? throw new ArgumentNullException(nameof(config));
            RespawnRunner = respawnRunner;
        }

        public IEnumerator Setup()
        {
            yield return OnSetup();
            IsSetUp = true;
        }

        public IEnumerator Teardown()
        {
            IsSetUp = false;
            yield return OnTeardown();
        }

        protected void CompleteSector(SectorResult result)
        {
            OnSectorComplete?.Invoke(result);
        }

        /// <summary>Override to implement sector-specific setup (scene load, spawning, objectives).</summary>
        protected abstract IEnumerator OnSetup();

        /// <summary>Override to implement sector-specific teardown (destroy objects, unload scene).</summary>
        protected abstract IEnumerator OnTeardown();
    }
}
