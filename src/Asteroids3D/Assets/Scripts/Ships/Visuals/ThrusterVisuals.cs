using System;
using System.Collections.Generic;
using Ships.Command;
using Ships.Presentation;
using UnityEngine;

namespace Ships.Visuals
{
    public class ThrusterVisuals : MonoBehaviour, IShipVisual, IWearsLivery
    {
        public ParticleSystem[] thrustParticles;
        private readonly List<(ParticleSystem system, ParticleSystemRenderer renderer)> flames = new();
        private Func<PilotCommand> command;

        // Each listed main thrust and the systems beside it in its exhaust, the small thrust among them.
        private void Awake()
        {
            foreach (var thrust in thrustParticles)
            {
                if (!thrust) continue;
                foreach (Transform sibling in thrust.transform.parent)
                    if (sibling.TryGetComponent(out ParticleSystem system) && !flames.Exists(flame => flame.system == system))
                        flames.Add((system, sibling.GetComponent<ParticleSystemRenderer>()));
            }
        }

        public void Bind(in ShipView view) => command = view.Command;

        public void Wear(in ShipLivery livery)
        {
            if (!livery.Flame) return;
            foreach (var (system, renderer) in flames)
            {
                // The flame material carries the whole colour; the authored start colour would tint it.
                var main = system.main;
                main.startColor = Color.white;
                renderer.sharedMaterial = livery.Flame;
            }
        }

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
