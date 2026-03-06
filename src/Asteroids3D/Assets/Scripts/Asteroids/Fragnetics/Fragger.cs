using System;
using System.Collections;
using Asteroids.Spawning;
using JetBrains.Annotations;
using UnityEngine;

namespace Asteroids.Fragnetics
{

    public class Fragger : MonoBehaviour
    {
        [SerializeField] private AsteroidFragSettings asteroidFragAsteroidFragSettings;
        private Calculator calc;

        private void Awake()
        {
            calc = new Calculator(asteroidFragAsteroidFragSettings);
        }

        /// <summary>
        /// Public entry point with explosion callback for delayed explosion option
        /// </summary>
        public void CreateFragments(AsteroidController asteroid, HitData hit, Action<Frag[]> onFragment = null)
        {            
            var ast = new AsteroidData(asteroid);
            var frags = calc.GenerateFragments(ast);
            var initialMomentum = calc.CalculateInitialMomentum(ast, hit);
            StartCoroutine(CreateFragmentsWithPlaceholders(ast, hit, frags, initialMomentum, asteroid.AsteroidSpawner, onFragment));
        }

        /// <summary>
        /// spawns placeholder fragments immediately, then updates them with proper physics
        /// </summary>
        private IEnumerator CreateFragmentsWithPlaceholders(AsteroidData ast, HitData hit, Frag[] frags, (Vector3 linear, Vector3 angular) momentum, AsteroidSpawner spawn, [CanBeNull] Action<Frag[]> onFragment = null)
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
        private AsteroidController[] SpawnPlaceholderFragments(AsteroidData ast, HitData hit, Frag[] frags, AsteroidSpawner spawn)
        {
            var fragments = new AsteroidController[frags.Length];
            calc.CalculatePlaceholderPhysics(ast, hit, frags);
            for (var i = 0; i < frags.Length; i++)
            {
                fragments[i] = spawn.SpawnFragment(frags[i]);
                if (asteroidFragAsteroidFragSettings.fragmentFadeInTime > 0f)
                    StartCoroutine(FadeInFragment(fragments[i]));
            }
            return fragments;
        }

        /// <summary>
        /// Update placeholder fragments with proper physics calculations
        /// </summary>
        private static void UpdatePlaceholderFragments(AsteroidController[] fragments, Frag[] frags)
        {
            for (var i = 0; i < fragments.Length; i++)
                fragments[i]?.UpdateKinematics(frags[i].Velocity, frags[i].Spin);
            
        }

        /// <summary>
        /// Fade in a fragment over time for smoother visual transition
        /// </summary>
        private IEnumerator FadeInFragment(AsteroidController fragment)
        {
            if (!fragment || asteroidFragAsteroidFragSettings.fragmentFadeInTime <= 0f) yield break;

            var re = fragment.Renderer;
            if (!re) yield break;

            var material = re.material;
            var originalColor = material.color;
            var transparentColor = new Color(originalColor.r, originalColor.g, originalColor.b, 0f);
        
            material.color = transparentColor;

            var elapsed = 0f;
            while (elapsed < asteroidFragAsteroidFragSettings.fragmentFadeInTime)
            {
                elapsed += Time.deltaTime;
                var alpha = Mathf.Lerp(0f, originalColor.a, elapsed / asteroidFragAsteroidFragSettings.fragmentFadeInTime);
                material.color = new Color(originalColor.r, originalColor.g, originalColor.b, alpha);
                yield return null;
            }

            material.color = originalColor;
        }
    }
}
