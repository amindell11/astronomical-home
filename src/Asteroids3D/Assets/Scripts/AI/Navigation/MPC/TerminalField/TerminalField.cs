using System;
using AI.Scanning;
using Movement;
using Movement.MPC;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace AI.Navigation.MPC.TerminalField
{
    /// <summary>The <c>Mpc</c>-owned cost-to-go grid over the ship's rock occupancy: placed and spaced per bake to contain ship, goal, max-speed reach over rollout + bake interval, hull clearance and two cells of padding; gathered through the ship's own <see cref="ObstacleScanner"/>; baked synchronously so a rollout never samples a half-written grid. <see cref="Update"/> runs the bake clock on simulation time; every buffer is allocated once and lives as long as the owner (#461).</summary>
    public sealed class TerminalField : IDisposable
    {
        private const int PaddingCells = 2;

        private readonly int resolution;
        private readonly float minSpacing;
        private readonly float bakeInterval;
        private readonly float reach;
        private readonly float clearance;
        private readonly bool multiSphere;
        private readonly ObstacleScanner scanner;

        private NativeArray<float> distances;
        private NativeArray<byte> occupied;
        private NativeArray<int> heap;
        private NativeArray<int> heapSlot;
        private NativeArray<int> seedIndexOut;
        private NativeArray<float> maxFiniteOut;
        private NativeArray<float3> discs;
        private DetectedObstacle[] scratch = new DetectedObstacle[64];

        private TerminalFieldView view;
        private float sinceBake;
        private float2 centre;
        private float halfSpan;
        private bool disposed;

        public TerminalFieldView View => view;
        public int BakeCount { get; private set; }
        public int LastDiscCount { get; private set; }

        public TerminalField(MpcSettings settings, Dynamics dynamics, ObstacleScanner scanner)
        {
            if (settings.terminalFieldResolution < 2 * PaddingCells + 4)
                throw new ArgumentOutOfRangeException(nameof(settings), "terminalFieldResolution must leave cells past the padding");
            if (settings.terminalFieldMinSpacing <= 0f)
                throw new ArgumentOutOfRangeException(nameof(settings), "terminalFieldMinSpacing must be positive");
            if (settings.terminalFieldBakeInterval <= 0f)
                throw new ArgumentOutOfRangeException(nameof(settings), "terminalFieldBakeInterval must be positive");

            resolution = settings.terminalFieldResolution;
            minSpacing = settings.terminalFieldMinSpacing;
            bakeInterval = settings.terminalFieldBakeInterval;
            reach = dynamics.maxSpeed * (settings.horizonSeconds + bakeInterval);
            clearance = dynamics.shipRadius + settings.collisionSafetyMargin;
            multiSphere = settings.multiSphereObstacles;
            this.scanner = scanner;

            var cells = resolution * resolution;
            distances = new NativeArray<float>(cells, Allocator.Persistent);
            occupied = new NativeArray<byte>(cells, Allocator.Persistent);
            heap = new NativeArray<int>(cells, Allocator.Persistent);
            heapSlot = new NativeArray<int>(cells, Allocator.Persistent);
            seedIndexOut = new NativeArray<int>(1, Allocator.Persistent);
            maxFiniteOut = new NativeArray<float>(1, Allocator.Persistent);
            discs = new NativeArray<float3>(scratch.Length * 3, Allocator.Persistent);
            view = new TerminalFieldView { distances = distances, resolution = resolution };
        }

        /// <summary>Advances the bake clock by one solve's simulation time. No goal invalidates the view; a goal bakes on first sight, every bake interval, and early when ship or goal leaves the grid.</summary>
        public void Update(float2 shipPos, bool hasGoal, float2 goal, float simDt)
        {
            if (!hasGoal)
            {
                Invalidate();
                return;
            }
            sinceBake += simDt;
            if (view.valid != 0 && sinceBake < bakeInterval && Contains(shipPos) && Contains(goal)) return;
            Bake(shipPos, goal);
        }

        private void Invalidate()
        {
            view.valid = 0;
            sinceBake = 0f;
        }

        /// <summary><see cref="Update"/> is the production entry; this one serves the tests and the microbench.</summary>
        public void Bake(float2 shipPos, float2 goal)
        {
            centre = 0.5f * (shipPos + goal);
            var content = math.cmax(math.abs(shipPos - centre)) + reach + clearance;
            var spacing = math.max(minSpacing, 2f * content / (resolution - 1 - 2 * PaddingCells));
            halfSpan = (resolution - 1) * spacing * 0.5f;
            var origin = centre - halfSpan;

            var discCount = Gather(halfSpan + 0.5f * spacing + clearance);
            RunBake(discCount, spacing, origin, goal);
        }

        /// <summary>The bake job alone over the gathered discs; the microbench times this apart from <see cref="Gather"/>.</summary>
        internal void RunBake(int discCount, float spacing, float2 origin, float2 goal)
        {
            new TerminalFieldBakeJob
            {
                discs = discs,
                discCount = discCount,
                resolution = resolution,
                spacing = spacing,
                origin = origin,
                goal = goal,
                distances = distances,
                occupied = occupied,
                heap = heap,
                heapSlot = heapSlot,
                seedIndexOut = seedIndexOut,
                maxFiniteOut = maxFiniteOut,
            }.Run();

            var seed = seedIndexOut[0];
            view = new TerminalFieldView
            {
                distances = distances,
                valid = 1,
                resolution = resolution,
                spacing = spacing,
                origin = origin,
                goal = goal,
                seedIndex = seed,
                seedToGoal = math.distance(origin + new float2(seed % resolution, seed / resolution) * spacing, goal),
                maxFiniteDistance = maxFiniteOut[0],
            };
            sinceBake = 0f;
            BakeCount++;
            LastDiscCount = discCount;
        }

        private bool Contains(float2 p) => math.cmax(math.abs(p - centre)) <= halfSpan;

        // Mirrors ConvertObstacles: lobes when multi-sphere is on and the rock has them, else the primary circle; every disc inflated by the hull clearance.
        internal int Gather(float halfExtent)
        {
            if (scanner == null) return 0;
            var found = scanner.QueryAround(new UnityEngine.Vector2(centre.x, centre.y), halfExtent, ref scratch);
            if (discs.Length < scratch.Length * 3)
            {
                discs.Dispose();
                discs = new NativeArray<float3>(scratch.Length * 3, Allocator.Persistent);
            }

            var written = 0;
            for (var i = 0; i < found; i++)
            {
                var obs = scratch[i];
                if (multiSphere && obs.lobeCount > 1)
                {
                    for (var k = 0; k < obs.lobeCount; k++)
                    {
                        var lobe = obs.Lobe(k);
                        discs[written++] = new float3(lobe.center.x, lobe.center.y, lobe.radius + clearance);
                    }
                }
                else
                {
                    discs[written++] = new float3(obs.position.x, obs.position.y, obs.radius + clearance);
                }
            }
            return written;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            distances.Dispose();
            occupied.Dispose();
            heap.Dispose();
            heapSlot.Dispose();
            seedIndexOut.Dispose();
            maxFiniteOut.Dispose();
            discs.Dispose();
        }
    }
}
