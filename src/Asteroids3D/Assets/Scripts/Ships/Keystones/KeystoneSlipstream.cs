using System;
using Substrate.Presentation;
using UnityEngine;

namespace Ships.Keystones
{
    public sealed class KeystoneSlipstream : MonoBehaviour, IPresentationPart
    {
        private const float ReleaseSeconds = 0.17f;
        [SerializeField] private Keystone keystone;
        [SerializeField] private ParticleSystem wake;
        [SerializeField] private ParticleSystem crown;
        [SerializeField] private float localShipLength = 2f;

        private readonly ParticleSystem.Particle[] wakeStrokes = new ParticleSystem.Particle[2];
        private readonly ParticleSystem.Particle[] bowStroke = new ParticleSystem.Particle[1];
        private float startedAt, lastActiveAt;
        private bool drawing;

        private void Awake()
        {
            if (!keystone || !wake || !(localShipLength > 0f))
                throw new InvalidOperationException($"{name} needs a keystone, painted wake, and positive hull length.");
        }

        public void ApplyPresentation(bool visible) => enabled = visible;

        private void OnDisable()
        {
            if (!drawing) return;
            Clear();
        }

        private void Clear()
        {
            wake.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (crown) crown.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            drawing = false;
        }

        private void LateUpdate()
        {
            if (keystone.Active)
            {
                if (!drawing)
                {
                    startedAt = Time.time;
                    wake.Play(false);
                    if (crown) crown.Play(false);
                    drawing = true;
                }
                lastActiveAt = Time.time;
                DrawActive(Time.time - startedAt);
            }
            else
            {
                if (!drawing) return;
                var release = (Time.time - lastActiveAt) / ReleaseSeconds;
                if (release >= 1f)
                {
                    Clear();
                    return;
                }
                var fade = 1f - Mathf.SmoothStep(0f, 1f, release);
                SetCel(ref wakeStrokes[0], 3f, fade);
                SetCel(ref wakeStrokes[1], 3f, fade * 0.64f);
                if (crown) SetCel(ref bowStroke[0], 3f, fade * 0.9f);
            }
            wake.SetParticles(wakeStrokes, wakeStrokes.Length);
            if (crown) crown.SetParticles(bowStroke, bowStroke.Length);
        }

        private void DrawActive(float age)
        {
            var length = transform.TransformVector(Vector3.up * localShipLength).magnitude;
            var velocity = (Vector2)keystone.Body.linearVelocity;
            Vector2 tail = velocity.sqrMagnitude > 0.01f ? -velocity.normalized : -(Vector2)transform.up;
            var axis = Mathf.Atan2(tail.y, tail.x) * Mathf.Rad2Deg;
            var rear = transform.position - transform.up * (length * 0.31f);
            var shoulder = transform.right * (length * 0.17f);
            DrawWake(ref wakeStrokes[0], rear - shoulder, axis - 3f, length, age, 1f);
            DrawWake(ref wakeStrokes[1], rear + shoulder, axis + 5f, length * 0.81f, age - 0.035f, 0.64f);
            if (!crown) return;
            var bow = transform.position + transform.up * (length * 0.35f);
            var scale = Mathf.Lerp(0.78f, 1f, Mathf.SmoothStep(0f, 1f, age / 0.12f));
            bowStroke[0].position = bow;
            bowStroke[0].rotation3D = new Vector3(0f, 0f, transform.eulerAngles.z);
            bowStroke[0].startSize3D = new Vector3(1.55f, 1.25f, 1f) * (length * scale);
            SetCel(ref bowStroke[0], ActiveCel(age * 0.78f), 0.92f);
        }

        private static void DrawWake(ref ParticleSystem.Particle stroke, Vector3 root, float angle,
            float length, float age, float opacity)
        {
            var stretch = Mathf.SmoothStep(0.35f, 1f, age / 0.115f);
            var size = new Vector3(2.5f * stretch, 1.64f, 1f) * length;
            var rotation = Quaternion.AngleAxis(angle, Vector3.forward);
            stroke.position = root + rotation * new Vector3(0.38f * size.x, 0.04f * size.y, 0f);
            stroke.rotation3D = new Vector3(0f, 0f, angle);
            stroke.startSize3D = size;
            SetCel(ref stroke, ActiveCel(age), age < 0f ? 0f : opacity);
        }

        private static float ActiveCel(float age)
        {
            if (age < 0.06f) return 0f;
            if (age < 0.105f) return Mathf.InverseLerp(0.06f, 0.105f, age);
            var beat = Mathf.Repeat(age - 0.105f, 0.28f);
            return 1f + Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.07f, 0.105f, beat))
                      - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.205f, 0.25f, beat));
        }

        private static void SetCel(ref ParticleSystem.Particle stroke, float cel, float alpha)
        {
            // Particle age selects the painted flipbook pose.
            stroke.startLifetime = 100f;
            stroke.remainingLifetime = 100f * (1f - cel * 0.25f);
            stroke.startColor = new Color(1f, 1f, 1f, alpha);
            stroke.randomSeed = 1;
        }
    }
}
