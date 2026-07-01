using UnityEngine;

namespace Ships.Visuals
{
    public class ThrusterVisuals : MonoBehaviour
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

            var shouldPlay = ship.Movement.CurrentCommand.thrust > 0.05f;
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
