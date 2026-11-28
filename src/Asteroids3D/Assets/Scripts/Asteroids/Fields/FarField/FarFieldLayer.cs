using System;
using System.Collections.Generic;
using System.Diagnostics;
using Asteroids.Fields.Core;
using Substrate;
using UnityEngine;
using UnityEngine.Rendering;

namespace Asteroids.Fields.FarField
{
    /// <summary>
    /// Draws one camera-facing sprite per baseline rock in the cells the main orthographic camera sees
    /// past the loaded field, straight from the layout (no spawning, depletion ignored). Rocks are
    /// cached per <see cref="FarFieldCells"/> block, generated lazily nearest the view first under a
    /// per-frame time budget; a block not generated yet is not drawn. A cell whose real rocks spawn
    /// keeps its sprites for a short dithered fade so the hand-off reads as one rock. Costs a corner
    /// test per frame while the view sits inside the loaded field.
    /// </summary>
    public sealed class FarFieldLayer : IDisposable
    {
        private const float GenerationBudgetMs = 1.5f;
        private const int CellsPerBlock = FarFieldCells.BlockCells * FarFieldCells.BlockCells;

        private static readonly int RocksId = Shader.PropertyToID("_FarRocks");
        private static readonly int RightId = Shader.PropertyToID("_FarRight");
        private static readonly int UpId = Shader.PropertyToID("_FarUp");
        private static readonly int ToCameraId = Shader.PropertyToID("_FarToCamera");

        private sealed class Block
        {
            public FarRock[] Rocks;
            public readonly int[] Offsets = new int[CellsPerBlock + 1];
        }

        private readonly AsteroidFieldLayout layout;
        private readonly FarFieldAtlas atlas;
        private readonly float loadRadius;
        private readonly float unloadRadius;
        private readonly float pad;
        private readonly Vector2 fieldOriginPlane;
        private readonly Func<Vector2Int, bool> isReal;

        private readonly Dictionary<Vector2Int, Block> blocks = new();
        private readonly List<FieldAsteroidSpec> specBuffer = new();
        private readonly List<FarRock> rockBuffer = new();
        private readonly List<Vector2Int> wholeFarBlocks = new();
        private readonly List<Vector2Int> boundaryBlocks = new();
        private readonly List<Vector2Int> boundaryFarCells = new();
        private readonly List<Vector2Int> missingBlocks = new();
        private readonly Comparison<Vector2Int> nearestViewFirst;
        private readonly Stopwatch generationClock = new();
        private Vector2 viewCentreBlocks;

        private HashSet<Vector2Int> edgeFarThisFrame = new();
        private HashSet<Vector2Int> edgeFarLastFrame = new();
        private readonly Dictionary<Vector2Int, float> fadeStart = new();
        private readonly List<Vector2Int> fadesDone = new();

        private Vector2Int lastBlockKey;
        private Block lastBlock;

        private readonly MaterialPropertyBlock properties = new();
        private RenderParams renderParams;
        private FarRock[] instances = new FarRock[1024];
        private GraphicsBuffer buffer;

        public FarFieldLayer(AsteroidFieldLayout layout, FarFieldAtlas atlas, float loadRadius, float unloadRadius,
            float maxScale, Vector2 fieldOriginPlane, Func<Vector2Int, bool> isReal)
        {
            if (!atlas.Material)
                throw new InvalidOperationException($"Far-field atlas '{atlas.name}' has no material; re-run the far-field bake.");
            this.layout = layout;
            this.atlas = atlas;
            this.loadRadius = loadRadius;
            this.unloadRadius = Mathf.Max(unloadRadius, loadRadius);
            foreach (var halfSize in atlas.HalfSizes) pad = Mathf.Max(pad, halfSize * maxScale);
            this.fieldOriginPlane = fieldOriginPlane;
            this.isReal = isReal;
            nearestViewFirst = (a, b) =>
                ((Vector2)a - viewCentreBlocks).sqrMagnitude.CompareTo(((Vector2)b - viewCentreBlocks).sqrMagnitude);

            properties.SetVector(RightId, GamePlane.Right);
            properties.SetVector(UpId, GamePlane.Forward);
            properties.SetVector(ToCameraId, -GamePlane.Normal);
            renderParams = new RenderParams(atlas.Material)
            {
                matProps = properties,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                lightProbeUsage = LightProbeUsage.BlendProbes
            };
        }

        public void Render(Camera cam, Vector2 anchorRel)
        {
            if (cam.cameraType != CameraType.Game || !cam.orthographic || !cam.CompareTag("MainCamera")) return;

            var view = ViewRect(cam);
            var rect = FarFieldCells.Expand(view, pad);
            var cellSize = layout.CellSize;
            var anyFar = FarFieldCells.Select(rect, anchorRel, loadRadius, unloadRadius, cellSize, isReal,
                wholeFarBlocks, boundaryBlocks, boundaryFarCells);
            if (!anyFar && edgeFarLastFrame.Count == 0 && fadeStart.Count == 0) return;

            var now = Time.unscaledTime;
            AdvanceHandOffs(rect, anchorRel, now);
            GenerateMissing(view.center / (cellSize * FarFieldCells.BlockCells));

            var count = 0;
            foreach (var key in wholeFarBlocks)
                if (blocks.TryGetValue(key, out var whole)) count = AppendAll(whole.Rocks, count);
            foreach (var cell in boundaryFarCells) count = AppendCell(cell, 1f, count);
            foreach (var (cell, start) in fadeStart)
                count = AppendCell(cell, 1f - (now - start) / atlas.HandOffSeconds, count);
            if (count == 0) return;

            Upload(count);
            renderParams.camera = cam;
            renderParams.worldBounds = BoundsOf(rect);
            Graphics.RenderPrimitives(renderParams, MeshTopology.Triangles, 6, count);
        }

        public void Dispose()
        {
            buffer?.Release();
            buffer = null;
        }

        // Only cells that can be loaded can turn real, so only they enter the hand-off bookkeeping.
        private void AdvanceHandOffs(Rect rect, Vector2 anchor, float now)
        {
            var cellSize = layout.CellSize;
            edgeFarThisFrame.Clear();
            foreach (var cell in boundaryFarCells)
                if (FarFieldCells.CanBeLoaded(cell, anchor, unloadRadius, cellSize))
                    edgeFarThisFrame.Add(cell);

            foreach (var cell in edgeFarLastFrame)
                if (!edgeFarThisFrame.Contains(cell) && isReal(cell) && FarFieldCells.Overlaps(rect, cell, cellSize))
                    fadeStart[cell] = now;
            (edgeFarLastFrame, edgeFarThisFrame) = (edgeFarThisFrame, edgeFarLastFrame);

            fadesDone.Clear();
            foreach (var (cell, start) in fadeStart)
                if (edgeFarLastFrame.Contains(cell) || !FarFieldCells.Overlaps(rect, cell, cellSize)
                    || now - start >= atlas.HandOffSeconds)
                    fadesDone.Add(cell);
            foreach (var cell in fadesDone) fadeStart.Remove(cell);
        }

        private void GenerateMissing(Vector2 viewCentreInBlocks)
        {
            missingBlocks.Clear();
            foreach (var key in wholeFarBlocks)
                if (!blocks.ContainsKey(key)) missingBlocks.Add(key);
            foreach (var key in boundaryBlocks)
                if (!blocks.ContainsKey(key)) missingBlocks.Add(key);
            if (missingBlocks.Count == 0) return;

            viewCentreBlocks = viewCentreInBlocks - new Vector2(0.5f, 0.5f);
            missingBlocks.Sort(nearestViewFirst);
            generationClock.Restart();
            foreach (var key in missingBlocks)
            {
                blocks[key] = Generate(key);
                if (generationClock.Elapsed.TotalMilliseconds >= GenerationBudgetMs) break;
            }
        }

        private Block Generate(Vector2Int key)
        {
            var result = new Block();
            var first = FarFieldCells.FirstCell(key);
            var halfSizes = atlas.HalfSizes;
            rockBuffer.Clear();
            for (var i = 0; i < CellsPerBlock; i++)
            {
                result.Offsets[i] = rockBuffer.Count;
                var cx = first.x + i % FarFieldCells.BlockCells;
                var cy = first.y + i / FarFieldCells.BlockCells;
                if (layout.CellBeyondFieldRadius(cx, cy)) continue;

                specBuffer.Clear();
                layout.GenerateCell(cx, cy, specBuffer);
                foreach (var spec in specBuffer)
                {
                    var pick = FarFieldViews.Pick(spec.Rotation, -GamePlane.Normal, GamePlane.Right, GamePlane.Forward);
                    rockBuffer.Add(new FarRock
                    {
                        Position = GamePlane.PlanePointToWorld(spec.PlanePosition + fieldOriginPlane),
                        HalfSize = halfSizes[spec.MeshIndex] * spec.Scale,
                        Spin = pick.Spin,
                        Cell = spec.MeshIndex * atlas.Views + pick.View,
                        Alpha = 1f
                    });
                }
            }
            result.Offsets[CellsPerBlock] = rockBuffer.Count;
            result.Rocks = rockBuffer.ToArray();
            return result;
        }

        private int AppendAll(FarRock[] rocks, int count)
        {
            EnsureCapacity(count + rocks.Length);
            Array.Copy(rocks, 0, instances, count, rocks.Length);
            return count + rocks.Length;
        }

        private int AppendCell(Vector2Int cell, float alpha, int count)
        {
            var key = FarFieldCells.BlockOf(cell);
            if (lastBlock == null || key != lastBlockKey)
            {
                if (!blocks.TryGetValue(key, out var found)) return count;
                lastBlockKey = key;
                lastBlock = found;
            }
            var local = cell - FarFieldCells.FirstCell(key);
            var index = local.y * FarFieldCells.BlockCells + local.x;
            var from = lastBlock.Offsets[index];
            var to = lastBlock.Offsets[index + 1];
            EnsureCapacity(count + to - from);
            for (var i = from; i < to; i++)
            {
                var rock = lastBlock.Rocks[i];
                rock.Alpha = alpha;
                instances[count++] = rock;
            }
            return count;
        }

        private void EnsureCapacity(int needed)
        {
            if (needed > instances.Length) Array.Resize(ref instances, Mathf.Max(instances.Length * 2, needed));
        }

        private Rect ViewRect(Camera cam)
        {
            var min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            var max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            for (var i = 0; i < 4; i++)
            {
                var viewport = new Vector3(i & 1, i >> 1, cam.nearClipPlane);
                var p = GamePlane.WorldPointToPlane(cam.ViewportToWorldPoint(viewport)) - fieldOriginPlane;
                min = Vector2.Min(min, p);
                max = Vector2.Max(max, p);
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private void Upload(int count)
        {
            if (buffer == null || buffer.count < count)
            {
                buffer?.Release();
                buffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, instances.Length, FarRock.Stride);
                properties.SetBuffer(RocksId, buffer);
            }
            buffer.SetData(instances, 0, 0, count);
        }

        private Bounds BoundsOf(Rect rect)
        {
            var size = Abs(GamePlane.Right * rect.width) + Abs(GamePlane.Forward * rect.height)
                       + Abs(GamePlane.Normal * (2f * pad));
            return new Bounds(GamePlane.PlanePointToWorld(rect.center + fieldOriginPlane), size);
        }

        private static Vector3 Abs(Vector3 v) => new(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
    }
}
