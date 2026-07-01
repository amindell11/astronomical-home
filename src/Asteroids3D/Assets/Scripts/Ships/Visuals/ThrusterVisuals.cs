using System;
using Ships.Command;
using Ships.Presentation;
using UnityEngine;

namespace Ships.Visuals
{
    public class ThrusterVisuals : MonoBehaviour, IShipVisual
    {
        public ParticleSystem[] thrustParticles;
        private Func<PilotCommand> command;

        public void Bind(in ShipView view) => command = view.Command;

        private void Update()
        {
            if (thrustParticles == null || thrustParticles.Length == 0 || command == null) return;

            var shouldPlay = command().thrust > 0.05f;
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
