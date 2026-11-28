using System;
using Cameras.Shake;
using Combat.Weapons.Arsenal;
using Ships;
using Substrate;
using Substrate.Presentation;
using UnityEngine;
using Utils;

namespace Combat.Weapons.Tells
{
    /// <summary>
    /// Stopping power's tell: each pellet that lands shakes the shooter's camera and throws a hit
    /// spark, both growing with how many of the pull's pellets that body has taken.
    /// </summary>
    [RequireComponent(typeof(Scatterguns))]
    public sealed class StoppingPowerTell : MonoBehaviour, IPresentationPart
    {
        [Tooltip("Shake played on the shooter per landed pellet, at the share of the pull's pellets landed so far.")]
        [SerializeField] private CameraShakeProfile profile;
        [Tooltip("The pellets' usual hit spark.")]
        [SerializeField] private GameObject spark;
        [Tooltip("Spark scale added per pellet the body has already taken.")]
        [SerializeField, Min(0f)] private float sparkGrowth = 0.35f;

        private Scatterguns scattergun;
        private PooledVFX pooledSpark;
        private Ship ship;

        private void Awake()
        {
            if (!profile || !spark) throw new InvalidOperationException($"{nameof(StoppingPowerTell)} on '{name}' needs its shake and spark.");
            scattergun = GetComponent<Scatterguns>();
            pooledSpark = spark.GetComponent<PooledVFX>();
            ship = GetComponentInParent<Ship>();
        }

        private void OnEnable() => scattergun.PelletLanded += Land;

        private void OnDisable() => scattergun.PelletLanded -= Land;

        public void ApplyPresentation(bool visible) => enabled = visible;

        private void Land(Vector3 point, int count)
        {
            PooledVFX.Play(pooledSpark, point, Quaternion.identity, 1f + sparkGrowth * (count - 1));
            if (!ship) return;
            ship.Shake.Raise(this, profile, -GamePlane.WorldDirToPlane(scattergun.FireDirection));
            ship.Shake.Scale(this, (float)count / scattergun.Pellets);
        }
    }
}
