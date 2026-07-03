using System.Collections.Generic;
using System.Linq;
using Asteroids.Fields.Core;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// Headless tests for the deterministic asteroid field core: LUT
    /// determinism, reload identity, override-overlay persistence and
    /// streaming hysteresis — no physics, no scene, no MonoBehaviours.
    /// </summary>
    [Category("Asteroids")]
    public class AsteroidFieldCoreEditModeTests
    {
        private const int Seed = 987654;

        private static FieldGenerationParams DefaultParams(bool drift = true) => new()
        {
            CellSize = 40f,
            AverageAsteroidsPerCell = 6f,
            NoiseFrequency = 0.35f,
            DensityMultiplierRange = new Vector2(0.3f, 1.7f),
            FieldRadius = 400f,
            MeshVolumes = new[] { 1f, 3f, 8f },
            MeshDensity = 2f,
            MassScaleRange = new Vector2(0.5f, 2f),
            VelocityRange = new Vector2(0.5f, 2f),
            SpinRange = new Vector2(-30f, 30f),
            AmbientDrift = drift
        };

        private static AsteroidFieldLayout NewLayout(int seed = Seed, bool drift = true) =>
            new(seed, DefaultParams(drift));

        private static List<FieldAsteroidSpec> Generate(AsteroidFieldLayout layout, int cx, int cy)
        {
            var list = new List<FieldAsteroidSpec>();
            layout.GenerateCell(cx, cy, list);
            return list;
        }

        private static void AssertSpecsEqual(FieldAsteroidSpec a, FieldAsteroidSpec b)
        {
            Assert.AreEqual(a.Id, b.Id);
            Assert.AreEqual(a.PlanePosition, b.PlanePosition);
            Assert.AreEqual(a.Rotation, b.Rotation);
            Assert.AreEqual(a.MeshIndex, b.MeshIndex);
            Assert.AreEqual(a.Mass, b.Mass);
            Assert.AreEqual(a.Scale, b.Scale);
            Assert.AreEqual(a.PlaneVelocity, b.PlaneVelocity);
            Assert.AreEqual(a.AngularVelocity, b.AngularVelocity);
        }

        // ── Determinism ──────────────────────────────────────────────────────────

        [Test]
        public void SameSeedAndCell_TwoIndependentGenerations_AreIdentical()
        {
            for (var cx = -2; cx <= 2; cx++)
            for (var cy = -2; cy <= 2; cy++)
            {
                var first = Generate(NewLayout(), cx, cy);
                var second = Generate(NewLayout(), cx, cy);
                Assert.AreEqual(first.Count, second.Count, $"cell ({cx},{cy})");
                for (var i = 0; i < first.Count; i++) AssertSpecsEqual(first[i], second[i]);
            }
        }

        [Test]
        public void DifferentSeeds_ProduceDifferentLayouts()
        {
            var a = new List<FieldAsteroidSpec>();
            var b = new List<FieldAsteroidSpec>();
            var layoutA = NewLayout(1111);
            var layoutB = NewLayout(2222);
            for (var cx = 0; cx < 3; cx++)
            {
                layoutA.GenerateCell(cx, 0, a);
                layoutB.GenerateCell(cx, 0, b);
            }
            var samePositions = a.Count == b.Count &&
                a.Zip(b, (x, y) => x.PlanePosition == y.PlanePosition).All(same => same);
            Assert.IsFalse(samePositions, "distinct seeds must yield distinct fields");
        }

        [Test]
        public void GenerateAsteroid_FromIdAlone_MatchesCellGeneration()
        {
            var layout = NewLayout();
            var viaCell = Generate(layout, 3, -4);
            Assume.That(viaCell.Count, Is.GreaterThan(0), "test cell unexpectedly empty — pick another");
            foreach (var spec in viaCell)
                AssertSpecsEqual(spec, layout.GenerateAsteroid(spec.Id));
        }

        [Test]
        public void Ids_AreUniqueWithinCell_AndStableAcrossRuns()
        {
            var specs = Generate(NewLayout(), 1, 1);
            var ids = specs.Select(s => s.Id).ToArray();
            Assert.AreEqual(ids.Length, ids.Distinct().Count());
            CollectionAssert.AreEqual(ids, Generate(NewLayout(), 1, 1).Select(s => s.Id));
        }

        [Test]
        public void DensityMultiplier_StaysWithinAuthoredRange()
        {
            var layout = NewLayout();
            for (var cx = -20; cx <= 20; cx += 3)
            for (var cy = -20; cy <= 20; cy += 3)
            {
                var m = layout.DensityMultiplier(cx, cy);
                Assert.That(m, Is.InRange(0.3f, 1.7f));
            }
        }

        [Test]
        public void Asteroids_StayInsideTheirCell_AndInsideFieldRadius()
        {
            var layout = NewLayout();
            for (var cx = -3; cx <= 3; cx += 2)
            for (var cy = -3; cy <= 3; cy += 2)
            {
                foreach (var spec in Generate(layout, cx, cy))
                {
                    Assert.That(spec.PlanePosition.x, Is.InRange(cx * 40f, (cx + 1) * 40f));
                    Assert.That(spec.PlanePosition.y, Is.InRange(cy * 40f, (cy + 1) * 40f));
                    Assert.LessOrEqual(spec.PlanePosition.magnitude, 400f);
                }
            }
        }

        [Test]
        public void FieldRadius_CullsFarCells()
        {
            var layout = NewLayout();
            // Cell far beyond the 400u field radius must be empty.
            var far = Generate(layout, 50, 50);
            Assert.IsEmpty(far);
        }

        [Test]
        public void AmbientDriftOff_ZeroesKinematics_ButKeepsLayoutIdentical()
        {
            var with = Generate(NewLayout(drift: true), 0, 0);
            var without = Generate(NewLayout(drift: false), 0, 0);
            Assert.AreEqual(with.Count, without.Count);
            for (var i = 0; i < with.Count; i++)
            {
                Assert.AreEqual(with[i].Id, without[i].Id);
                Assert.AreEqual(with[i].PlanePosition, without[i].PlanePosition);
                Assert.AreEqual(with[i].Rotation, without[i].Rotation);
                Assert.AreEqual(with[i].Mass, without[i].Mass);
                Assert.AreEqual(Vector2.zero, without[i].PlaneVelocity);
                Assert.AreEqual(Vector3.zero, without[i].AngularVelocity);
            }
        }

        [Test]
        public void MassScaleRelation_MatchesOldInlineMath()
        {
            foreach (var spec in Generate(NewLayout(), 0, 0))
            {
                var baseMass = DefaultParams().MeshVolumes[spec.MeshIndex] * 2f;
                Assert.AreEqual(spec.Mass, baseMass * spec.Scale * spec.Scale * spec.Scale, spec.Mass * 1e-4f);
                var speedScale = 1f / Mathf.Pow(spec.Mass, 1f / 3f);
                Assert.That(spec.PlaneVelocity.magnitude,
                    Is.InRange(0.5f * speedScale - 1e-4f, 2f * speedScale + 1e-4f));
            }
        }

        // ── Reload identity & overlay persistence (model level) ─────────────────

        [Test]
        public void ReloadIdentity_UntouchedChunk_ReappearsIdentically()
        {
            var model = new AsteroidFieldModel(NewLayout());
            var chunk = new Vector2Int(0, 0);

            var firstLoad = new List<FieldAsteroidSpec>();
            model.GetChunkContents(chunk, firstLoad);
            // "Unload" writes nothing for untouched asteroids; reload:
            var secondLoad = new List<FieldAsteroidSpec>();
            model.GetChunkContents(chunk, secondLoad);

            Assert.AreEqual(firstLoad.Count, secondLoad.Count);
            for (var i = 0; i < firstLoad.Count; i++) AssertSpecsEqual(firstLoad[i], secondLoad[i]);
        }

        [Test]
        public void DestroyedBaseline_IsTombstoned_AndFragmentsPresent_AfterReload()
        {
            var model = new AsteroidFieldModel(NewLayout());
            var chunk = new Vector2Int(0, 0);
            var loaded = new List<FieldAsteroidSpec>();
            model.GetChunkContents(chunk, loaded);
            Assume.That(loaded.Count, Is.GreaterThan(0));

            var victim = loaded[0];
            model.Overlay.Tombstone(victim.Id);
            var fragId = model.NextAuthoredId();
            model.Overlay.WriteAuthored(new AuthoredAsteroid
            {
                Id = fragId,
                PlanePosition = victim.PlanePosition + new Vector2(1f, 0f),
                Rotation = Quaternion.identity,
                MeshIndex = 1,
                Mass = victim.Mass * 0.4f,
                Scale = 0.7f,
                HealthFraction = 1f
            });

            var reloaded = new List<FieldAsteroidSpec>();
            model.GetChunkContents(chunk, reloaded);

            Assert.IsFalse(reloaded.Any(s => s.Id.Equals(victim.Id)), "tombstoned baseline must not reload");
            var frag = reloaded.Where(s => s.Id.Equals(fragId)).ToArray();
            Assert.AreEqual(1, frag.Length, "authored fragment must reload in its home chunk");
            Assert.AreEqual(victim.Mass * 0.4f, frag[0].Mass);
            Assert.AreEqual(1, frag[0].MeshIndex);
            Assert.AreEqual(Vector2.zero, frag[0].PlaneVelocity, "fragments reload at rest");
        }

        [Test]
        public void DamagedBaseline_ReloadsChewedDown_NotPristine()
        {
            var model = new AsteroidFieldModel(NewLayout());
            var chunk = new Vector2Int(1, 0);
            var loaded = new List<FieldAsteroidSpec>();
            model.GetChunkContents(chunk, loaded);
            Assume.That(loaded.Count, Is.GreaterThan(0));

            model.Overlay.RecordDamage(loaded[0].Id, 0.35f);

            var reloaded = new List<FieldAsteroidSpec>();
            model.GetChunkContents(chunk, reloaded);
            Assert.AreEqual(0.35f, reloaded.First(s => s.Id.Equals(loaded[0].Id)).HealthFraction);
            // Everyone else stays pristine.
            Assert.IsTrue(reloaded.Where(s => !s.Id.Equals(loaded[0].Id)).All(s => s.HealthFraction >= 1f));
        }

        [Test]
        public void DestroyedFragment_LeavesTheOverlay()
        {
            var model = new AsteroidFieldModel(NewLayout());
            var fragId = model.NextAuthoredId();
            model.Overlay.WriteAuthored(new AuthoredAsteroid { Id = fragId, PlanePosition = new Vector2(5f, 5f) });
            model.Overlay.RemoveAuthored(fragId);

            var reloaded = new List<FieldAsteroidSpec>();
            model.GetChunkContents(new Vector2Int(0, 0), reloaded);
            Assert.IsFalse(reloaded.Any(s => s.Id.Equals(fragId)));
        }

        [Test]
        public void Tombstone_ClearsAnyDamageEntry()
        {
            var overlay = new AsteroidFieldOverlay();
            var id = AsteroidId.Baseline(0, 0, 0);
            overlay.RecordDamage(id, 0.5f);
            overlay.Tombstone(id);
            Assert.IsFalse(overlay.DamagedHealth.ContainsKey(id));
            Assert.IsTrue(overlay.IsTombstoned(id));
        }

        [Test]
        public void AuthoredIds_AreDistinctFromBaselineIds()
        {
            var model = new AsteroidFieldModel(NewLayout());
            var authored = model.NextAuthoredId();
            Assert.IsTrue(authored.Authored);
            Assert.AreNotEqual(AsteroidId.Baseline(0, 0, 0), authored);
            Assert.AreNotEqual(model.NextAuthoredId(), authored, "authored ids must be session-unique");
        }

        // ── Streaming hysteresis ─────────────────────────────────────────────────

        [Test]
        public void Streamer_LoadsChunksWithinLoadRadius()
        {
            var streamer = new ChunkStreamer(40f, 80f, 120f);
            var toLoad = new List<Vector2Int>();
            var toUnload = new List<Vector2Int>();
            streamer.Update(new Vector2(20f, 20f), toLoad, toUnload);

            Assert.IsNotEmpty(toLoad);
            Assert.IsEmpty(toUnload);
            Assert.IsTrue(streamer.IsLoaded(new Vector2Int(0, 0)));
            foreach (var chunk in toLoad)
            {
                var center = new Vector2((chunk.x + 0.5f) * 40f, (chunk.y + 0.5f) * 40f);
                Assert.LessOrEqual(Vector2.Distance(center, new Vector2(20f, 20f)), 80f);
            }
        }

        [Test]
        public void Streamer_BoundarySitting_DoesNotThrash()
        {
            var streamer = new ChunkStreamer(40f, 80f, 120f);
            var toLoad = new List<Vector2Int>();
            var toUnload = new List<Vector2Int>();
            streamer.Update(Vector2.zero, toLoad, toUnload);

            // Oscillate across a chunk-load boundary by a few units; the
            // hysteresis band must absorb it with zero load/unload churn.
            for (var i = 0; i < 20; i++)
            {
                toLoad.Clear();
                toUnload.Clear();
                var wobble = new Vector2(i % 2 == 0 ? 3f : -3f, 0f);
                streamer.Update(wobble, toLoad, toUnload);
                Assert.IsEmpty(toUnload, $"unload thrash at step {i}");
                if (i > 0) Assert.IsEmpty(toLoad, $"load thrash at step {i}");
            }
        }

        [Test]
        public void Streamer_UnloadsOnlyPastUnloadRadius()
        {
            var streamer = new ChunkStreamer(40f, 80f, 120f);
            var toLoad = new List<Vector2Int>();
            var toUnload = new List<Vector2Int>();
            streamer.Update(Vector2.zero, toLoad, toUnload);
            var origin = new Vector2Int(0, 0);
            Assert.IsTrue(streamer.IsLoaded(origin));

            // Move just past the load radius: origin chunk stays loaded.
            toLoad.Clear(); toUnload.Clear();
            streamer.Update(new Vector2(100f, 0f), toLoad, toUnload);
            Assert.IsTrue(streamer.IsLoaded(origin), "inside hysteresis band must stay loaded");

            // Move past the unload radius: now it unloads.
            toLoad.Clear(); toUnload.Clear();
            streamer.Update(new Vector2(200f, 0f), toLoad, toUnload);
            Assert.IsFalse(streamer.IsLoaded(origin));
            CollectionAssert.Contains(toUnload, origin);
        }

        [Test]
        public void Streamer_ReturnTrip_ReloadsTheSameChunks()
        {
            var streamer = new ChunkStreamer(40f, 80f, 120f);
            var toLoad = new List<Vector2Int>();
            var toUnload = new List<Vector2Int>();

            streamer.Update(Vector2.zero, toLoad, toUnload);
            var initial = toLoad.OrderBy(c => (c.x, c.y)).ToArray();

            // Fly far away (everything unloads), then come back.
            toLoad.Clear(); toUnload.Clear();
            streamer.Update(new Vector2(1000f, 0f), toLoad, toUnload);
            Assert.IsFalse(streamer.IsLoaded(new Vector2Int(0, 0)));

            toLoad.Clear(); toUnload.Clear();
            streamer.Update(Vector2.zero, toLoad, toUnload);
            var reloadedNearOrigin = toLoad.Where(c => initial.Contains(c)).OrderBy(c => (c.x, c.y)).ToArray();
            CollectionAssert.AreEqual(initial, reloadedNearOrigin);
        }

        // ── Noise profile shaping (octaves / ridged / contrast / floor / warp) ──

        private static FieldGenerationParams SpindleParams()
        {
            var p = DefaultParams();
            p.NoiseOctaves = 4;
            p.NoiseLacunarity = 2.2f;
            p.NoisePersistence = 0.5f;
            p.RidgedNoise = true;
            p.NoiseContrast = 2.5f;
            p.DensityFloor = 0.35f;
            p.WarpStrength = 1.5f;
            p.WarpFrequency = 0.11f;
            return p;
        }

        [Test]
        public void NoiseProfile_FullPipeline_IsDeterministic()
        {
            var a = new AsteroidFieldLayout(Seed, SpindleParams());
            var b = new AsteroidFieldLayout(Seed, SpindleParams());
            for (var cx = -4; cx <= 4; cx++)
            for (var cy = -4; cy <= 4; cy++)
                Assert.AreEqual(a.DensityMultiplier(cx, cy), b.DensityMultiplier(cx, cy));

            var specsA = Generate(a, 0, 0);
            var specsB = Generate(b, 0, 0);
            Assert.AreEqual(specsA.Count, specsB.Count);
            for (var i = 0; i < specsA.Count; i++) AssertSpecsEqual(specsA[i], specsB[i]);
        }

        [Test]
        public void NeutralProfile_MatchesStructDefaults()
        {
            // Explicit neutral profile must normalize to the same output as an
            // all-zero (struct default) profile — the back-compat guarantee.
            var neutral = DefaultParams();
            neutral.NoiseOctaves = 1;
            neutral.NoiseLacunarity = 2f;
            neutral.NoisePersistence = 0.5f;
            neutral.NoiseContrast = 1f;

            var a = new AsteroidFieldLayout(Seed, DefaultParams());
            var b = new AsteroidFieldLayout(Seed, neutral);
            for (var cx = -5; cx <= 5; cx++)
            for (var cy = -5; cy <= 5; cy++)
                Assert.AreEqual(a.DensityMultiplier(cx, cy), b.DensityMultiplier(cx, cy));
        }

        [Test]
        public void ContrastAboveOne_LowersMeanDensity()
        {
            var flat = DefaultParams();
            var punchy = DefaultParams();
            punchy.NoiseContrast = 3f;
            var a = new AsteroidFieldLayout(Seed, flat);
            var b = new AsteroidFieldLayout(Seed, punchy);

            float meanA = 0f, meanB = 0f;
            var count = 0;
            for (var cx = -15; cx <= 15; cx++)
            for (var cy = -15; cy <= 15; cy++)
            {
                meanA += a.DensityMultiplier(cx, cy);
                meanB += b.DensityMultiplier(cx, cy);
                count++;
            }
            Assert.Less(meanB / count, meanA / count,
                "contrast > 1 must crush midtones toward the range minimum (peakier field)");
        }

        [Test]
        public void DensityFloor_CarvesTrueVoids()
        {
            var p = DefaultParams();
            p.NoiseContrast = 2f;
            p.DensityFloor = 0.5f;
            var layout = new AsteroidFieldLayout(Seed, p);

            var voids = 0;
            var occupied = 0;
            for (var cx = -15; cx <= 15; cx++)
            for (var cy = -15; cy <= 15; cy++)
            {
                var m = layout.DensityMultiplier(cx, cy);
                if (m <= 0f)
                {
                    voids++;
                    Assert.AreEqual(0, layout.CountForCell(cx, cy), "void cells must spawn nothing");
                }
                else
                {
                    occupied++;
                    Assert.That(m, Is.InRange(0.3f, 1.7f), "non-void cells stay in the authored range");
                }
            }
            Assert.Greater(voids, 0, "a 0.5 floor should carve some true voids");
            Assert.Greater(occupied, 0, "and still leave occupied cells");
        }

        [Test]
        public void DomainWarp_ChangesThePattern()
        {
            var straight = SpindleParams();
            straight.WarpStrength = 0f;
            var warped = SpindleParams();

            var a = new AsteroidFieldLayout(Seed, straight);
            var b = new AsteroidFieldLayout(Seed, warped);

            var anyDifferent = false;
            for (var cx = -10; cx <= 10 && !anyDifferent; cx++)
            for (var cy = -10; cy <= 10 && !anyDifferent; cy++)
                anyDifferent = !Mathf.Approximately(a.DensityMultiplier(cx, cy), b.DensityMultiplier(cx, cy));
            Assert.IsTrue(anyDifferent, "warp must actually displace the sample domain");
        }

        // ── Spawn-overlap priority rejection ─────────────────────────────────────

        private static FieldGenerationParams PackedParams(float margin = 1.2f, float minSpacing = 0.5f)
        {
            var p = DefaultParams();
            p.PackingMargin = margin;
            p.MinSpacing = minSpacing;
            return p;
        }

        private static List<FieldAsteroidSpec> GeneratePatch(AsteroidFieldLayout layout, int extent = 3)
        {
            var list = new List<FieldAsteroidSpec>();
            for (var cx = -extent; cx <= extent; cx++)
            for (var cy = -extent; cy <= extent; cy++)
                layout.GenerateCell(cx, cy, list);
            return list;
        }

        [Test]
        public void OverlapRejection_AcceptedAsteroids_RespectMinSeparation()
        {
            const float margin = 1.2f;
            const float minSpacing = 0.5f;
            var layout = new AsteroidFieldLayout(Seed, PackedParams(margin, minSpacing));
            var specs = GeneratePatch(layout);
            Assume.That(specs.Count, Is.GreaterThan(20), "patch unexpectedly sparse");

            // Only pairs fully inside the patch are meaningful (edge cells have
            // unexamined outside neighbours) — but every generated pair must hold,
            // since both members were accepted against each other.
            for (var i = 0; i < specs.Count; i++)
            for (var j = i + 1; j < specs.Count; j++)
            {
                var a = layout.GenerateCandidate(specs[i].Id);
                var b = layout.GenerateCandidate(specs[j].Id);
                var required = Mathf.Max((a.Radius + b.Radius) * margin, a.Radius + b.Radius + minSpacing);
                Assert.GreaterOrEqual(
                    Vector2.Distance(a.Position, b.Position), required - 1e-4f,
                    $"{specs[i].Id} and {specs[j].Id} spawn overlapping");
            }
        }

        [Test]
        public void OverlapRejection_ActuallyRejects_AndIsDeterministic()
        {
            var rejecting = GeneratePatch(new AsteroidFieldLayout(Seed, PackedParams()));
            var baseline = GeneratePatch(new AsteroidFieldLayout(Seed, DefaultParams()));
            Assert.Less(rejecting.Count, baseline.Count,
                "at authored density some candidates must be rejected — otherwise this test guards nothing");

            // Same seed → identical accepted set (IDs + poses), including rejections.
            var again = GeneratePatch(new AsteroidFieldLayout(Seed, PackedParams()));
            Assert.AreEqual(rejecting.Count, again.Count);
            for (var i = 0; i < rejecting.Count; i++) AssertSpecsEqual(rejecting[i], again[i]);
        }

        [Test]
        public void OverlapRejection_RejectedIdsStayRejected_AndAcceptedIdsDoNotRenumber()
        {
            var layout = new AsteroidFieldLayout(Seed, PackedParams());
            var first = GeneratePatch(layout, extent: 2).Select(s => s.Id).ToArray();
            var second = GeneratePatch(new AsteroidFieldLayout(Seed, PackedParams()), extent: 2).Select(s => s.Id).ToArray();
            CollectionAssert.AreEqual(first, second, "rejection must be stable across regenerations");

            // A rejected index is a skip, not a renumber: every accepted ID still
            // regenerates its own spec verbatim (the determinism keystone).
            foreach (var spec in GeneratePatch(layout, extent: 2))
                AssertSpecsEqual(spec, layout.GenerateAsteroid(spec.Id));
        }

        [Test]
        public void OverlapRejection_ZeroMarginAndSpacing_DisablesRejection()
        {
            // Struct-default params must reproduce the pre-rejection layout:
            // every counted index inside the field radius is present.
            var layout = NewLayout();
            var specs = Generate(layout, 0, 0);
            Assert.AreEqual(layout.CountForCell(0, 0), specs.Count,
                "cell (0,0) is fully inside the field radius; with rejection off every index must be present");
        }

        [Test]
        public void GenerateCandidate_MatchesGenerateAsteroid_OpeningDraws()
        {
            // The candidate a cell computes for a neighbour must match what that
            // neighbour computes for itself — same function, same stream prefix.
            var layout = new AsteroidFieldLayout(Seed, PackedParams());
            for (var i = 0; i < 12; i++)
            {
                var id = AsteroidId.Baseline(2, -3, i);
                var candidate = layout.GenerateCandidate(id);
                var spec = layout.GenerateAsteroid(id);
                Assert.AreEqual(spec.PlanePosition, candidate.Position);
                Assert.AreEqual(spec.MeshIndex, candidate.MeshIndex);
                var volume = DefaultParams().MeshVolumes[spec.MeshIndex];
                var expectedRadius = Mathf.Pow(3f * volume / (4f * Mathf.PI), 1f / 3f) * spec.Scale;
                Assert.AreEqual(expectedRadius, candidate.Radius, expectedRadius * 1e-4f);
            }
        }

        // ── Exclusion volumes (authored clearings) ───────────────────────────────

        [Test]
        public void ExclusionVolume_HomesInside_AreNeverGenerated_SurvivorsUnchanged()
        {
            var center = new Vector2(20f, 20f);
            const float radius = 35f;
            var p = DefaultParams();
            p.ExclusionVolumes = new[] { new ExclusionVolume { Center = center, Radius = radius } };

            var withClearing = GeneratePatch(new AsteroidFieldLayout(Seed, p));
            var baseline = GeneratePatch(new AsteroidFieldLayout(Seed, DefaultParams()));
            Assert.Less(withClearing.Count, baseline.Count,
                "a cell-sized clearing at authored density must cull something");

            foreach (var spec in withClearing)
                Assert.Greater(Vector2.Distance(spec.PlanePosition, center), radius,
                    $"{spec.Id} spawned inside the clearing");

            // The cull is a skip, not a reshuffle: survivors are exactly the
            // baseline specs outside the volume, verbatim.
            var expected = baseline.Where(s => (s.PlanePosition - center).sqrMagnitude > radius * radius).ToList();
            Assert.AreEqual(expected.Count, withClearing.Count);
            for (var i = 0; i < expected.Count; i++) AssertSpecsEqual(expected[i], withClearing[i]);
        }

        [Test]
        public void ExclusionVolume_ComposesWithOverlapRejection_Deterministically()
        {
            var p = PackedParams();
            p.ExclusionVolumes = new[] { new ExclusionVolume { Center = new Vector2(-30f, 10f), Radius = 40f } };

            var first = GeneratePatch(new AsteroidFieldLayout(Seed, p));
            var second = GeneratePatch(new AsteroidFieldLayout(Seed, p));
            Assert.AreEqual(first.Count, second.Count);
            for (var i = 0; i < first.Count; i++) AssertSpecsEqual(first[i], second[i]);

            // An excluded candidate still blocks its neighbours: the accepted
            // set outside the clearing matches the no-clearing layout exactly.
            var noClearing = PackedParams();
            var reference = GeneratePatch(new AsteroidFieldLayout(Seed, noClearing))
                .Where(s => (s.PlanePosition - new Vector2(-30f, 10f)).sqrMagnitude > 40f * 40f).ToList();
            Assert.AreEqual(reference.Count, first.Count);
            for (var i = 0; i < reference.Count; i++) AssertSpecsEqual(reference[i], first[i]);
        }

        // ── Deterministic RNG sanity ─────────────────────────────────────────────

        [Test]
        public void DeterministicRandom_SameSeed_SameStream()
        {
            var a = new DeterministicRandom(42);
            var b = new DeterministicRandom(42);
            for (var i = 0; i < 100; i++) Assert.AreEqual(a.NextUInt(), b.NextUInt());
        }

        [Test]
        public void DeterministicRandom_FloatsStayInRange_AndRotationsAreUnit()
        {
            var rng = new DeterministicRandom(7);
            for (var i = 0; i < 200; i++)
            {
                var f = rng.NextFloat();
                Assert.That(f, Is.InRange(0f, 1f));
                var q = rng.RotationUniform();
                var norm = Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
                Assert.AreEqual(1f, norm, 1e-4f);
            }
        }

        [Test]
        public void Hash_IsOrderSensitive()
        {
            Assert.AreNotEqual(
                DeterministicRandom.Hash(1, 2, 3),
                DeterministicRandom.Hash(3, 2, 1));
            Assert.AreNotEqual(
                DeterministicRandom.Hash(1, 0, 0),
                DeterministicRandom.Hash(0, 1, 0));
        }
    }
}
