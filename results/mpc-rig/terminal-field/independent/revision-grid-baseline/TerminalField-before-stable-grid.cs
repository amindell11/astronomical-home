using System;
using AI.Scanning;
using Movement;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace AI.Navigation.MPC.TerminalField
{
    /// <summary>
    /// Owns one ship's terrain distances and bake scratch. Plan refreshes the field synchronously
    /// before scheduling rollouts, so views remain stable through candidate evaluation and diagnostics.
    /// Regional queries use independent storage through the ship's existing obstacle scanner.
    /// </summary>
    public sealed class TerminalField : IDisposable
    {
        private readonly ObstacleScanner scanner;
        private readonly MpcSettings settings;
        private readonly Dynamics dynamics;
        private NativeArray<float> distances;
        private NativeArray<byte> occupied;
        private NativeArray<int> heap;
        private NativeArray<int> heapPosition;
        private NativeArray<ObstacleData> obstacles;
        private NativeArray<TerminalFieldBakeJob.BakeResult> result;
        private DetectedObstacle[] queryBuffer = new DetectedObstacle[64];
        private TerminalFieldView view;
        private double elapsed;

        public TerminalFieldView View => view;
        public NativeArray<byte>.ReadOnly Occupied => occupied.AsReadOnly();
        public int BakeCount { get; private set; }

        public TerminalField(MpcSettings settings, Dynamics dynamics, ObstacleScanner scanner)
        {
            if (settings.terminalFieldResolution < 6)
                throw new ArgumentOutOfRangeException(nameof(settings.terminalFieldResolution), "Terminal field needs at least six cells per axis.");
            if (!math.isfinite(settings.terminalFieldMinSpacing) || settings.terminalFieldMinSpacing <= 0f)
                throw new ArgumentOutOfRangeException(nameof(settings.terminalFieldMinSpacing));
            if (!math.isfinite(settings.terminalFieldBakeInterval) || settings.terminalFieldBakeInterval <= 0f)
                throw new ArgumentOutOfRangeException(nameof(settings.terminalFieldBakeInterval));
            this.settings = settings;
            this.dynamics = dynamics;
            this.scanner = scanner;
            var count = checked(settings.terminalFieldResolution * settings.terminalFieldResolution);
            distances = new NativeArray<float>(count, Allocator.Persistent);
            occupied = new NativeArray<byte>(count, Allocator.Persistent);
            heap = new NativeArray<int>(count, Allocator.Persistent);
            heapPosition = new NativeArray<int>(count, Allocator.Persistent);
            obstacles = new NativeArray<ObstacleData>(192, Allocator.Persistent);
            result = new NativeArray<TerminalFieldBakeJob.BakeResult>(1, Allocator.Persistent);
            view = new TerminalFieldView { distances = distances, resolution = settings.terminalFieldResolution };
        }

        public void Update(float dt, float2 ship, bool hasGoal, float2 goal, in Config config)
        {
            elapsed += dt;
            if (!hasGoal || scanner == null)
            {
                view.valid = false;
                return;
            }
            if (view.valid && elapsed + 1e-6 < settings.terminalFieldBakeInterval
                && view.Contains(ship) && view.Contains(goal)) return;

            var clearance = dynamics.shipRadius + config.collisionSafetyMargin;
            var reach = dynamics.maxSpeed * (config.horizon * config.dt + settings.terminalFieldBakeInterval) + clearance;
            var spacing = math.max(settings.terminalFieldMinSpacing,
                (math.cmax(math.abs(goal - ship)) * 0.5f + reach) / ((view.resolution - 1) * 0.5f - 2f));
            var center = (ship + goal) * 0.5f;
            var halfDomain = spacing * (view.resolution - 1) * 0.5f;
            var scan = scanner.Query(new Vector2(center.x, center.y), halfDomain + spacing * 0.5f + clearance, ref queryBuffer);
            var capacity = checked(scan.count * 3);
            if (capacity > obstacles.Length)
            {
                obstacles.Dispose();
                obstacles = new NativeArray<ObstacleData>(math.ceilpow2(capacity), Allocator.Persistent);
            }
            var count = 0;
            for (var i = 0; i < scan.count; i++)
            {
                var obstacle = scan.buffer[i];
                if (settings.multiSphereObstacles && obstacle.lobeCount > 1)
                {
                    for (var j = 0; j < obstacle.lobeCount; j++)
                    {
                        var lobe = obstacle.Lobe(j);
                        obstacles[count++] = new ObstacleData { position = new float2(lobe.center.x, lobe.center.y), radius = lobe.radius };
                    }
                }
                else obstacles[count++] = new ObstacleData { position = new float2(obstacle.position.x, obstacle.position.y), radius = obstacle.radius };
            }
            new TerminalFieldBakeJob
            {
                obstacles = obstacles, obstacleCount = count, resolution = view.resolution,
                spacing = spacing, origin = center - halfDomain, goal = goal, clearance = clearance,
                distances = distances, occupied = occupied, heap = heap, heapPosition = heapPosition, result = result,
            }.Schedule().Complete();
            view = new TerminalFieldView
            {
                distances = distances, valid = true, resolution = view.resolution, spacing = spacing,
                origin = center - halfDomain, goal = goal, seedIndex = result[0].seedIndex,
                maxFiniteDistance = result[0].maxFiniteDistance,
            };
            elapsed = 0d;
            BakeCount++;
        }

        public void Dispose()
        {
            distances.Dispose();
            occupied.Dispose();
            heap.Dispose();
            heapPosition.Dispose();
            obstacles.Dispose();
            result.Dispose();
        }
    }
}
