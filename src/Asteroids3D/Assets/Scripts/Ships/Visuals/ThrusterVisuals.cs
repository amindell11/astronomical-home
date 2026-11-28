using System;
using System.Collections.Generic;
using Ships.Movement;
using Ships.Presentation;
using UnityEngine;

namespace Ships.Visuals
{
    /// <summary>
    /// Drives the main exhaust and the RCS jets from the thrust the movement system applied.
    /// A jet lights when its push and its torque about the ship help the applied thrust, its
    /// size and opacity grow with that share up to full, and it stays on for a minimum time.
    /// </summary>
    public class ThrusterVisuals : MonoBehaviour, IShipVisual, IWearsLivery
    {
        public ParticleSystem[] thrustParticles;

        [Tooltip("Manoeuvring jets. Each pushes the ship along its socket's +Y; its flame points down the socket's −Y.")]
        [SerializeField] private ParticleSystem[] rcsJets = Array.Empty<ParticleSystem>();

        [Tooltip("Demand, as a fraction of the ship's full authority, at which a jet lights.")]
        [SerializeField] private float fireThreshold = 0.25f;

        [Tooltip("Seconds a lit jet stays on, so small corrections don't flicker.")]
        [SerializeField] private float minOnTime = 0.1f;

        [Tooltip("Jet size, as a fraction of its authored size, at the faintest demand that lights it. Full demand shows the authored size.")]
        [SerializeField, Range(0f, 1f)] private float faintestSize = 0.35f;

        [Tooltip("Jet opacity at the faintest demand that lights it. Full demand is fully opaque.")]
        [SerializeField, Range(0f, 1f)] private float faintestOpacity = 0.35f;

        private readonly List<(ParticleSystem system, ParticleSystemRenderer renderer)> flames = new();
        private Func<AppliedThrust> thrust;
        private Vector3[] push;
        private float[] yawLever;
        private float[] rollLever;
        private float[] litUntil;
        private float[] litStrength;
        private Vector3[] fullScale;

        // Each listed main thrust and the systems beside it in its exhaust, the small thrust among them.
        private void Awake()
        {
            foreach (var particle in thrustParticles)
            {
                if (!particle) continue;
                foreach (Transform sibling in particle.transform.parent)
                    if (sibling.TryGetComponent(out ParticleSystem system) && !flames.Exists(flame => flame.system == system))
                        flames.Add((system, sibling.GetComponent<ParticleSystemRenderer>()));
            }

            push = new Vector3[rcsJets.Length];
            yawLever = new float[rcsJets.Length];
            rollLever = new float[rcsJets.Length];
            litUntil = new float[rcsJets.Length];
            litStrength = new float[rcsJets.Length];
            fullScale = new Vector3[rcsJets.Length];
            for (var i = 0; i < rcsJets.Length; i++)
            {
                var jet = rcsJets[i];
                if (!jet) continue;
                fullScale[i] = jet.transform.localScale;
                if (flames.Exists(flame => flame.system == jet)) continue;
                flames.Add((jet, jet.GetComponent<ParticleSystemRenderer>()));
            }
        }

        public void Bind(in ShipView view)
        {
            thrust = view.Thrust;
            if (!view.Root) return;

            for (var i = 0; i < rcsJets.Length; i++)
            {
                var jet = rcsJets[i];
                if (!jet) continue;
                var socket = jet.transform.parent;
                var p = view.Root.InverseTransformPoint(socket.position);
                var u = view.Root.InverseTransformDirection(socket.up).normalized;
                var torque = Vector3.Cross(p, u) / Mathf.Max(p.magnitude, 1e-4f);
                push[i] = u;
                yawLever[i] = torque.z;
                rollLever[i] = torque.y;
            }
        }

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
            if (thrust == null) return;

            var applied = thrust();
            UpdateThrustAnimations(applied.Force.y > 0.05f);

            for (var i = 0; i < rcsJets.Length; i++)
            {
                var demand = push[i].x * applied.Force.x + push[i].y * applied.Force.y
                    + yawLever[i] * applied.Yaw + rollLever[i] * applied.Roll;
                if (demand > fireThreshold)
                {
                    litUntil[i] = Time.time + minOnTime;
                    litStrength[i] = Mathf.Clamp01((demand - fireThreshold) / (1f - fireThreshold));
                }

                var ps = rcsJets[i];
                if (!ps) continue;
                if (Time.time < litUntil[i])
                {
                    ps.transform.localScale = fullScale[i] * Mathf.Lerp(faintestSize, 1f, litStrength[i]);
                    var main = ps.main;
                    main.startColor = new Color(1f, 1f, 1f, Mathf.Lerp(faintestOpacity, 1f, litStrength[i]));
                    if (!ps.isPlaying)
                        ps.Play(true);
                }
                else if (ps.isPlaying)
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
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
