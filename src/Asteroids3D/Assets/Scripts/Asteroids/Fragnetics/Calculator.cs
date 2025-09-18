using System;
using System.Linq;
using UnityEngine;

namespace Asteroids
{
    public static class FragCalculator
    {
        /// <summary>
        /// Returns an array of fragment masses that:
        ///   - each ≥ minMass
        ///   - count is between minFragments and maxFragments
        ///   - total = totalMass
        ///   - biased toward using more fragments when possible
        /// Returns an empty array if not enough mass to create minFragments.
        /// </summary>
        private float[] GenerateFragmentMasses(float totalMass)
        {
            // Determine the feasible number of fragments
            if (totalMass <= 0 || minMass <= 0) return Array.Empty<float>();
            int feasibleMax = Mathf.Min(maxFragments, Mathf.FloorToInt(totalMass / minMass));
            if (feasibleMax < minFragments) return Array.Empty<float>();

            // Choose a fragment count, biased toward the high end
            float randomBiased = Mathf.Pow(UnityEngine.Random.value, highCountBias);
            int n = minFragments + Mathf.FloorToInt(randomBiased * (feasibleMax - minFragments + 1));

            // Slice totalMass into n parts using a Dirichlet distribution
            float remainingMass = totalMass - n * minMass;
            if (remainingMass < 0) remainingMass = 0;

            // Generate n random weights
            var weights = Enumerable.Range(0, n)
                .Select(_ => UnityEngine.Random.value)
                .ToArray();
            float sumOfWeights = weights.Sum();

            // If the sum of weights is zero (highly unlikely), distribute the remaining mass equally
            if (sumOfWeights == 0)
            {
                float extraPerFragment = remainingMass / n;
                var masses = Enumerable.Repeat(minMass + extraPerFragment, n).ToArray();
                return masses;
            }

            // Distribute the remaining mass according to the weights
            var finalMasses = weights.Select(w => minMass + (w / sumOfWeights) * remainingMass).ToArray();
            return finalMasses;
        }
    }
}