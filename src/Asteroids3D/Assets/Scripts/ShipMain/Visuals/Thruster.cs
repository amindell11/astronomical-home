using UnityEngine;
using Utils;

namespace ShipMain.Visuals
{
    public class Thruster : MonoBehaviour
    {
        public ParticleSystem[] thrustParticles;
        private Ship ship;

        private void Start()    
        {
            ship = GetComponentInParent<Ship>();
        }

        private void Update()
        {
            if (thrustParticles == null || thrustParticles.Length == 0 || !ship) return;

            // Also check global VFX setting
            bool shouldPlay = ship.CurrentCommand.Thrust > 0.05f && GameSettings.VfxEnabled;
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