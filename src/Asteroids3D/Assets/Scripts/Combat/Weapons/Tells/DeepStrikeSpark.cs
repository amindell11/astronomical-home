using System;
using Combat.Weapons.Arsenal;
using Substrate.Presentation;
using UnityEngine;
using Utils;

namespace Combat.Weapons.Tells
{
    [RequireComponent(typeof(Railguns))]
    public sealed class DeepStrikeSpark : MonoBehaviour, IPresentationPart
    {
        [Tooltip("Hit spark thrown, enlarged, where a Deep strike beam first strikes.")]
        [SerializeField] private GameObject spark;
        [SerializeField, Min(0f)] private float scale = 2.5f;

        private Railguns railgun;
        private PooledVFX pooledSpark;

        private void Awake()
        {
            if (!spark) throw new InvalidOperationException($"{nameof(DeepStrikeSpark)} on '{name}' needs its spark.");
            railgun = GetComponent<Railguns>();
            pooledSpark = spark.GetComponent<PooledVFX>();
        }

        private void OnEnable() => railgun.DeepStruck += Play;

        private void OnDisable() => railgun.DeepStruck -= Play;

        public void ApplyPresentation(bool visible) => enabled = visible;

        private void Play(Vector3 point) => PooledVFX.Play(pooledSpark, point, Quaternion.identity, scale);
    }
}
