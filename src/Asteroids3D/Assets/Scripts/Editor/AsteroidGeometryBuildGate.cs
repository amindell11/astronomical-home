using System.Collections.Generic;
using Asteroids.Spawning;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace AsteroidTools
{
    /// <summary>
    /// Refuses to build a player whose asteroid geometry bake no longer matches the meshes
    /// it came from: every shipped <see cref="AsteroidSpawnSettings.MeshInfo"/> is
    /// re-derived and compared, and a mismatch fails the build.
    ///
    /// The runtime deliberately carries no check of its own. With the bake automatic
    /// (<see cref="AsteroidVolumePostprocessor"/>) and the build gated here, bad data
    /// reaching a spawned asteroid would mean this gate is broken — and a runtime guard
    /// against a broken gate is a guard in costume.
    /// </summary>
    public class AsteroidGeometryBuildGate : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        /// <summary>
        /// Bake and gate run the same math, so a match is near-exact — Unity serializes
        /// floats at round-trip precision. A real mesh edit moves these by percent, not
        /// parts per million, so the tolerance stays well clear of both.
        /// </summary>
        private const float RelativeTolerance = 1e-5f;

        public void OnPreprocessBuild(BuildReport report) => Validate();

        /// <summary>Throws with every problem found, not just the first — a build that
        /// fails ten times in a row over one asset each is its own kind of broken.</summary>
        public static void Validate()
        {
            var problems = new List<string>();

            foreach (var guid in AssetDatabase.FindAssets("t:AsteroidSpawnSettings"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var settings = AssetDatabase.LoadAssetAtPath<AsteroidSpawnSettings>(path);
                if (settings == null) continue;

                if (settings.meshInfos == null || settings.meshInfos.Length == 0)
                {
                    problems.Add($"{path}: no meshInfos — nothing can spawn.");
                    continue;
                }

                for (int i = 0; i < settings.meshInfos.Length; i++)
                    Inspect(path, i, settings.meshInfos[i], problems);
            }

            if (problems.Count == 0) return;

            throw new BuildFailedException(
                "Asteroid geometry bake is stale or invalid. Re-import the asteroid models " +
                "(volume rebakes automatically) and run Tools/Asteroids/Rebake Asteroid Lobes " +
                "for lobe failures.\n  - " + string.Join("\n  - ", problems));
        }

        private static void Inspect(
            string path, int index, AsteroidSpawnSettings.MeshInfo info, List<string> problems)
        {
            var where = $"{path}[{index}]";

            if (info.mesh == null)
            {
                problems.Add($"{where}: null mesh.");
                return;
            }

            where = $"{where} '{info.mesh.name}'";

            if (!AsteroidMeshVolume.TryCompute(info.mesh, out var volume, out _))
            {
                problems.Add($"{where}: mesh is not closed or not readable — volume undefined.");
                return;
            }

            if (info.cachedVolume <= 0f)
                problems.Add($"{where}: cachedVolume is {info.cachedVolume}; expected {volume}.");
            else if (!WithinTolerance(info.cachedVolume, volume))
                problems.Add($"{where}: cachedVolume {info.cachedVolume} is stale; mesh gives {volume}.");

            if (info.cachedLobes == null || info.cachedLobes.Length == 0)
            {
                problems.Add($"{where}: no cachedLobes — the MPC has no shape for this rock.");
                return;
            }

            var fresh = AsteroidLobeBaker.Bake(info.mesh, out _);
            if (fresh.Length != info.cachedLobes.Length)
            {
                problems.Add(
                    $"{where}: {info.cachedLobes.Length} baked lobes, mesh now gives {fresh.Length}.");
                return;
            }

            for (int i = 0; i < fresh.Length; i++)
            {
                if (info.cachedLobes[i].radius <= 0f)
                {
                    problems.Add($"{where}: lobe {i} has non-positive radius {info.cachedLobes[i].radius}.");
                    continue;
                }
                if (!WithinTolerance(info.cachedLobes[i].radius, fresh[i].radius) ||
                    Vector3.Distance(info.cachedLobes[i].center, fresh[i].center) >
                        fresh[i].radius * RelativeTolerance)
                    problems.Add($"{where}: lobe {i} is stale.");
            }
        }

        private static bool WithinTolerance(float baked, float fresh) =>
            Mathf.Abs(baked - fresh) <= Mathf.Max(Mathf.Abs(fresh), 1e-6f) * RelativeTolerance;
    }
}
