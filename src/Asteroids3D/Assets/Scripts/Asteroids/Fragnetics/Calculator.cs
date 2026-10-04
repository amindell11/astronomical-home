using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using Utils;

namespace Asteroids.Fragnetics
{
    public class Calculator
    {

        
        private readonly AsteroidFragSettings asteroidFragSettings;

        public Calculator(AsteroidFragSettings s)
        {
            asteroidFragSettings = s;
        }
        
        public Frag[] GenerateFragments(AsteroidData ast, ref DeterministicRandom rng)
        {
            var masses = GenerateFragmentMasses(ast.Mass * asteroidFragSettings.massLossFactor, asteroidFragSettings, ref rng);
            if (masses.Length == 0)
            {
                return Array.Empty<Frag>();
            }
            var positions = CalculateFragmentPositions(ast.Position, masses.Length, ref rng);
            var fragments = new Frag[masses.Length];
            for (var i = 0; i < masses.Length; i++)
            {
                fragments[i] = new Frag(masses[i], positions[i], rng.RotationUniform());
            }
            return fragments;
        }
        public (Vector3 linear, Vector3 angular) CalculateInitialMomentum(AsteroidData ast, HitData hit)
        {
	        // Discarded mass leaves with its momentum share; the projectile is absorbed whole.
	        var retained = asteroidFragSettings.massLossFactor;
	        var totalLinearMomentum = retained * ast.Mass * ast.Velocity + hit.Mass * hit.Velocity;

	        var localAngularVelocity = Quaternion.Inverse(ast.Rotation) * ast.AngularVelocity;
	        var localAngularMomentum = Vector3.Scale(ast.InertiaTensor, localAngularVelocity);
	        var asteroidAngularMomentum = retained * (ast.Rotation * localAngularMomentum);

	        var r = hit.HitPoint - ast.Position;
	        var projectileAngularMomentum = Vector3.Cross(r, hit.Mass * hit.Velocity);
	        var totalAngularMomentum = asteroidAngularMomentum + projectileAngularMomentum;

	        return (totalLinearMomentum, totalAngularMomentum);
        }
        public void CalculatePlaceholderPhysics(AsteroidData ast, HitData hit, Frag[] frags, ref DeterministicRandom rng)
        {
	        var impactDirection = (hit.Velocity - ast.Velocity).normalized;

	        for (var i = 0; i < frags.Length; i++)
	        {
		        var roughDirection = (frags[i].Position - ast.Position).normalized;
		        frags[i].Velocity = ast.Velocity + 
		                        (roughDirection * (asteroidFragSettings.baseSeparationSpeed * 0.5f)) + 
		                        (impactDirection * (asteroidFragSettings.baseSeparationSpeed * 0.3f));
                
		        frags[i].Spin = rng.InsideUnitSphere() * (asteroidFragSettings.spinVariation * 0.5f);
	        }
        }

        public IEnumerator CoCalculateFragmentPhysics(AsteroidData ast,
	        HitData hit,
	        Frag[] frags,
	        (Vector3 linear, Vector3 angular) momentum,
	        Action<Frag[]> onFrag,
	        ref DeterministicRandom rng)
        {
	        // The iterator cannot take the stream by ref, so every draw happens here, up front.
	        var jitter = new FragJitter[frags.Length];
	        for (var i = 0; i < jitter.Length; i++)
		        jitter[i] = new FragJitter(rng.InsideUnitSphere().normalized, rng.Range(0.8f, 1.2f),
			        rng.InsideUnitSphere() * asteroidFragSettings.spinVariation);
	        return CoCalculateFragmentPhysics(ast, hit, frags, momentum, onFrag, jitter, asteroidFragSettings);
        }
        
        /// <summary>
        /// Returns an array of fragment masses that:
        ///   - each ≥ minMass
        ///   - count is between minFragments and maxFragments
        ///   - total = totalMass
        ///   - biased toward using more fragments when possible
        /// Returns an empty array if not enough mass to create minFragments.
        /// </summary>
        private static float[] GenerateFragmentMasses(float totalMass, AsteroidFragSettings s, ref DeterministicRandom rng)
        {
            // Determine the feasible number of fragments
            if (totalMass <= 0 || s.minMass <= 0) return Array.Empty<float>();
            var feasibleMax = Mathf.Min(s.maxFragments, Mathf.FloorToInt(totalMass / s.minMass));
            if (feasibleMax < s.minFragments) return Array.Empty<float>();

            // Choose a fragment count, biased toward the high end
            var randomBiased = Mathf.Pow(rng.NextFloat(), s.highCountBias);
            var n = s.minFragments + Mathf.FloorToInt(randomBiased * (feasibleMax - s.minFragments + 1));

            // Slice totalMass into n parts using a Dirichlet distribution
            var remainingMass = totalMass - n * s.minMass;
            if (remainingMass < 0) remainingMass = 0;

            // Generate n random weights
            var weights = new float[n];
            for (var i = 0; i < n; i++) weights[i] = rng.NextFloat();
            var sumOfWeights = weights.Sum();

            // If the sum of weights is zero (highly unlikely), distribute the remaining mass equally
            if (sumOfWeights == 0)
            {
                var extraPerFragment = remainingMass / n;
                var masses = Enumerable.Repeat(s.minMass + extraPerFragment, n).ToArray();
                return masses;
            }

            // Distribute the remaining mass according to the weights
            var finalMasses = weights.Select(w => s.minMass + (w / sumOfWeights) * remainingMass).ToArray();
            return finalMasses;
        }

        private static Vector3[] CalculateFragmentPositions(Vector3 parentPosition, int fragmentCount, ref DeterministicRandom rng)
        {
	        var positions = new Vector3[fragmentCount];
	        for (var i = 0; i < fragmentCount; i++)
	        {
		        Vector3 randomOffset = rng.Direction2() * 0.5f;
		        positions[i] = parentPosition + randomOffset;
	        }

	        return positions;
        }
        
        private static IEnumerator CoCalculateFragmentPhysics(
	        AsteroidData ast, HitData hit, Frag[] frags,  
	        (Vector3 linear, Vector3 angular) momentum,
            Action<Frag[]> onFrag, FragJitter[] jitter, AsteroidFragSettings s)
        {
			var acc = new FragSum();

			var center = ast.Position;
			var (hitDir, hitRelVel) = HitDirAndRelVel(ast, hit);

			// Momentum-per-mass scale (m/s): p / M, with coupling
			var totalFragMass = 0f;
			for (var i = 0; i < frags.Length; ++i) totalFragMass += frags[i].Mass;
			var projectileMomentumMag = hit.Mass * hitRelVel * s.momentumCoupling;
			var momentumPerMass = projectileMomentumMag / Mathf.Max(totalFragMass, 1e-6f);

			for (var i = 0; i < frags.Length; ++i)
			{
				frags[i].Velocity = FragmentationVelocity(frags[i].Position, center, hitDir, momentumPerMass, jitter[i], s);

				var r = frags[i].Position - center;
				AccumulateFragmentSums(ref acc, frags[i].Mass, frags[i].Velocity, r);
				
				yield return null;
			}
			
			var (vCorr, omegaBase) = MomentumCorrection(momentum, acc);
			ApplyCorrections(frags, jitter, vCorr, omegaBase);

			onFrag?.Invoke(frags);
		}

        private static (Vector3 hitDir, float hitRelVel) HitDirAndRelVel(AsteroidData ast, HitData hit)
        {
	        var rel = hit.Velocity - ast.Velocity;
	        return (rel.sqrMagnitude > 0f ? rel.normalized : Vector3.zero, rel.magnitude);
        }
        
        private static Vector3 FragmentationVelocity
	        (Vector3 pos, Vector3 center, Vector3 bulletDir, float momentumPerMass, FragJitter jitter, AsteroidFragSettings s)
        {
	        var outward = (pos - center).normalized;
	        var dir = (s.outwardBias * outward + s.bulletBias * bulletDir + s.randomBias * jitter.Direction).normalized;
            var speed = Mathf.Max(s.baseSeparationSpeed * momentumPerMass, s.minSeparationSpeed)
                        * jitter.SpeedScale;
	        return dir * speed;
        }
        
		private static void AccumulateFragmentSums
			(ref FragSum acc, float mass, Vector3 velocity, Vector3 r)
		{
			acc.totalMass += mass;
			acc.pFrag += mass * velocity;
			acc.mrSum += mass * r;
			acc.lOrbit += Vector3.Cross(r, mass * velocity);
			var radius = Mathf.Pow(mass, 1f / 3f);
			acc.iTotal += 0.4f * mass * radius * radius;
		}

		private static (Vector3 vCorr, Vector3 omegaBase) MomentumCorrection
			((Vector3 linear, Vector3 angular) mom, FragSum acc)
		{
			var vCorr = (mom.linear - acc.pFrag) / Mathf.Max(acc.totalMass, 1e-6f);
			var lOrbit = acc.lOrbit + Vector3.Cross(acc.mrSum, vCorr);
			var lSpin = (mom.angular - lOrbit);
			var omegaBase = acc.iTotal > 0 ? lSpin / acc.iTotal : Vector3.zero;
			return (vCorr, omegaBase);
		}

		private static void ApplyCorrections
			(Frag[] frags, FragJitter[] jitter,
				Vector3 vCorr, Vector3 omegaBase)
		{
			for (var i = 0; i < frags.Length; ++i)
			{
				frags[i].Velocity += vCorr;
				frags[i].Spin = omegaBase + jitter[i].Spin;
			}
		}
		
    }
}