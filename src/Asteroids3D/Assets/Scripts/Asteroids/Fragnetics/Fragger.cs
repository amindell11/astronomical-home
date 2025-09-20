using System;
using System.Collections;
using UnityEngine;
using Utils;

namespace Asteroids.Fragnetics
{

    public class Fragger : MonoSingleton<Fragger>
    {
        [SerializeField] private Settings fragSettings;
        private Calculator calc;

        protected override void Awake()
        {
            base.Awake();
            calc = new Calculator(fragSettings);
        }

        /// <summary>
        /// Public entry point with explosion callback for delayed explosion option
        /// </summary>
        public void CreateFragments(Asteroid asteroid, HitData hit, System.Action<Frag[]> onFragment = null)
        {            
            var ast = new AsteroidData(asteroid);
            var frags = calc.GenerateFragments(ast);
            var initialMomentum = calc.CalculateInitialMomentum(ast, hit);
            if (frags.Length <= 0) {
                onFragment?.Invoke(frags);
            }
            StartCoroutine(CreateFragmentsWithPlaceholders(ast, hit, frags, initialMomentum, asteroid.Spawner, onFragment));
        }

        /// <summary>
        /// spawns placeholder fragments immediately, then updates them with proper physics
        /// </summary>
        private IEnumerator CreateFragmentsWithPlaceholders(AsteroidData ast, HitData hit, Frag[] frags, (Vector3 linear, Vector3 angular) momentum, Spawner spawn, System.Action<Frag[]> onFragment = null)
        {

            calc.CalculatePlaceholderPhysics(ast, hit, frags);
            var placeholderFragments = SpawnPlaceholderFragments(ast, hit, frags, spawn);
            onFragment += OnFrag;
            yield return null;
            yield return StartCoroutine(calc.CoCalculateFragmentPhysics(
                ast,
                hit,
                frags,
                momentum, 
                onFragment
            ));
            yield break;
            void OnFrag(Frag[] f) => UpdatePlaceholderFragments(placeholderFragments, f);
        }

        /// <summary>
        /// Spawn fragments immediately with rough physics for visual continuity
        /// </summary>
        private Asteroid[] SpawnPlaceholderFragments(AsteroidData ast, HitData hit, Frag[] frags, Spawner spawn)
        {
            var fragments = new Asteroid[frags.Length];
            calc.CalculatePlaceholderPhysics(ast, hit, frags);
            for (int i = 0; i < frags.Length; i++)
            {
                fragments[i] = spawn.SpawnFragment(frags[i]);
                if (fragSettings.fragmentFadeInTime > 0f)
                    StartCoroutine(FadeInFragment(fragments[i]));
            }
            return fragments;
        }

        /// <summary>
        /// Update placeholder fragments with proper physics calculations
        /// </summary>
        private static void UpdatePlaceholderFragments(Asteroid[] fragments, Frag[] frags)
        {
            for (int i = 0; i < fragments.Length; i++)
            {
                fragments[i]?.UpdateKinematics(frags[i].Velocity, frags[i].Spin);
            }
        }

        /// <summary>
        /// Fade in a fragment over time for smoother visual transition
        /// </summary>
        private IEnumerator FadeInFragment(Asteroid fragment)
        {
            if (!fragment || fragSettings.fragmentFadeInTime <= 0f) yield break;

            var re = fragment.Renderer;
            if (!re) yield break;

            var material = re.material;
            var originalColor = material.color;
            var transparentColor = new Color(originalColor.r, originalColor.g, originalColor.b, 0f);
        
            material.color = transparentColor;

            float elapsed = 0f;
            while (elapsed < fragSettings.fragmentFadeInTime)
            {
                elapsed += Time.deltaTime;
                float alpha = Mathf.Lerp(0f, originalColor.a, elapsed / fragSettings.fragmentFadeInTime);
                material.color = new Color(originalColor.r, originalColor.g, originalColor.b, alpha);
                yield return null;
            }

            material.color = originalColor;
        }
    }
}