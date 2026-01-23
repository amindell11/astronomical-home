using UnityEngine;
using Utils;

namespace Ships.Visuals
{
    public class Thruster : MonoBehaviour
    {
        public ParticleSystem[] thrustParticles;
        private Ships.Ship ship;

        private void Start()    
        {
            ship = GetComponentInParent<Ships.Ship>();
        }

        private void Update()
        {
            if (thrustParticles == null || thrustParticles.Length == 0 || !ship) return;

            // Also check global VFX setting
            var shouldPlay = ship.CurrentCommand.thrust > 0.05f && GameSettings.VfxEnabled;
            UpdateThrustAnimations(shouldPlay);
        }

        private void UpdateThrustAnimations(bool shouldPlay)
        {
            foreach (var ps in thrustParticles)
            {
                if (!ps) continue;
                if (shouldPlay && !ps.isPlaying)
                    ps.Play(true);
                else if (!shouldPlay && ps.isPlaying)
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }
    }
}
