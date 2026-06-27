using System;
using System.Collections;
using Game.Bootstrap;
using Game.Services;
using Ships;
using UnityEngine;

namespace Game.Sectors
{
    public abstract class Sector : MonoBehaviour, ISector
    {
        public event Action<SectorResult> OnSectorComplete;

        protected IGameServices Services { get; private set; }
        protected SectorSettings Config { get; private set; }
        protected bool IsSetUp { get; private set; }

        public void Initialize(IGameServices services, SectorSettings config)
        {
            Services = services ?? throw new ArgumentNullException(nameof(services));
            Config = config ?? throw new ArgumentNullException(nameof(config));
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
