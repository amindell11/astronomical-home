using Substrate;
using Substrate.Presentation;
using UnityEngine;

namespace Combat.Projectiles.Visual
{
    /// <summary>
    /// Retro-thrust flame: lights when the charge starts braking and blows its exhaust ahead,
    /// toward the target point, so the player reads where the charge will stop.
    /// </summary>
    [RequireComponent(typeof(Grenade))]
    public sealed class GrenadeThrustVisual : MonoBehaviour, IPresentationPart
    {
        [SerializeField] private ParticleSystem flame;

        private Grenade grenade;

        private void Awake()
        {
            grenade = GetComponent<Grenade>();
        }

        private void OnEnable()
        {
            grenade.BrakingStarted += Ignite;
        }

        private void OnDisable()
        {
            grenade.BrakingStarted -= Ignite;
            if (flame) flame.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        public void ApplyPresentation(bool visible) => enabled = visible;

        private void Ignite()
        {
            if (!flame) return;
            flame.transform.rotation = Quaternion.LookRotation(grenade.Heading, GamePlane.Normal);
            flame.Play(true);
        }
    }
}
