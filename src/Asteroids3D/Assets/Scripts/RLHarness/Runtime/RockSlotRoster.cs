using System;
using AI;
using AI.Scanning;
using UnityEngine;

namespace Game.RLHarness
{
    /// <summary>One rock slot's scan snapshot at the last decision boundary, for the obs writer.</summary>
    public readonly struct RockSlotView
    {
        public readonly bool valid;
        public readonly Vector2 pos;
        public readonly Vector2 vel;
        public readonly float radius;
        public readonly float healthPct;

        public RockSlotView(Vector2 pos, Vector2 vel, float radius, float healthPct)
        {
            valid = true;
            this.pos = pos;
            this.vel = vel;
            this.radius = radius;
            this.healthPct = healthPct;
        }
    }

    /// <summary>The sticky rock-slot selection service (#485): owns the 6-slot asteroid referent menu the policy observes and the
    /// boundary slot→entity capture. Roster rule: nearest-<see cref="NearestPerSide"/>-to-self ∪
    /// nearest-to-enemy, dedup, backfilled by self-distance. Occupancy is sticky — a challenger must
    /// beat an occupant's proximity score by a real margin, and an occupant bound by the held sentence
    /// is never challenged out; only despawn or range-exit (leaving the scan) removes it. Slot indices
    /// are stable across updates, and the host updates only at the decision boundary, so an action's
    /// referent index always resolves against the roster the policy observed — never a fresh scan.</summary>
    public sealed class RockSlotRoster
    {
        public const int SlotCount = 6;
        public const int NearestPerSide = 3;

        // Proximity score = min(distance-to-self, distance-to-enemy): a scalar proxy for roster
        // desirability the margin can compare across the two-sided membership rule.
        public const float DefaultHysteresisMargin = 2f;

        private struct Candidate
        {
            public AsteroidRef rock;
            public float dSelf;
            public float dEnemy;
            public Vector2 pos;
            public Vector2 vel;
            public float radius;
            public float healthPct;
            public bool ideal;
            public bool rostered;
        }

        private readonly float margin;
        private readonly AsteroidRef[] slots = new AsteroidRef[SlotCount];
        private readonly RockSlotView[] views = new RockSlotView[SlotCount];
        private Candidate[] candidates = new Candidate[64];
        private int candidateCount;

        public RockSlotRoster(float hysteresisMargin = DefaultHysteresisMargin)
        {
            margin = hysteresisMargin;
        }

        public bool TryGetSlot(int index, out AsteroidRef rock)
        {
            rock = slots[index];
            return rock.IsBound;
        }

        /// <summary>The occupant's scan snapshot from the last Update — the obs writer's read; invalid when the slot is empty.</summary>
        public RockSlotView SlotView(int index) => views[index];

        /// <summary>Back to the pre-first-update state; stale refs must not outlive an episode boundary.</summary>
        public void Reset()
        {
            Array.Clear(slots, 0, slots.Length);
            Array.Clear(views, 0, views.Length);
        }

        /// <summary>One decision-boundary refresh. <paramref name="bound"/> names the rocks the held
        /// sentence binds — those occupants only leave by despawn or range-exit.</summary>
        public void Update(Vector2 selfPlanePos, Vector2 enemyPlanePos, in ObstacleScan rocks,
            ReadOnlySpan<AsteroidRef> bound)
        {
            Gather(selfPlanePos, enemyPlanePos, in rocks);
            MarkIdeal();
            EvictDeadAndDeparted();
            FillAndChallenge(bound);
            RefreshViews();
        }

        private void Gather(Vector2 selfPlanePos, Vector2 enemyPlanePos, in ObstacleScan rocks)
        {
            candidateCount = 0;
            var buffer = rocks.buffer;
            var count = buffer == null ? 0 : Mathf.Min(rocks.count, buffer.Length);
            if (candidates.Length < count) candidates = new Candidate[count];

            for (var i = 0; i < count; i++)
            {
                var source = buffer[i].source;
                if (!source) continue;
                candidates[candidateCount++] = new Candidate
                {
                    rock = AsteroidRef.Of(source),
                    dSelf = (buffer[i].position - selfPlanePos).magnitude,
                    dEnemy = (buffer[i].position - enemyPlanePos).magnitude,
                    pos = buffer[i].position,
                    vel = buffer[i].velocity,
                    radius = buffer[i].radius,
                    healthPct = buffer[i].healthPct,
                };
            }
        }

        private void MarkIdeal()
        {
            var idealTarget = Mathf.Min(SlotCount, candidateCount);
            MarkNearest(NearestPerSide, bySelf: true);
            MarkNearest(NearestPerSide, bySelf: false);

            // Backfill by self-distance until the union reaches the roster size.
            var idealCount = 0;
            for (var i = 0; i < candidateCount; i++)
                if (candidates[i].ideal)
                    idealCount++;
            while (idealCount < idealTarget)
            {
                var best = -1;
                for (var i = 0; i < candidateCount; i++)
                {
                    if (candidates[i].ideal) continue;
                    if (best < 0 || candidates[i].dSelf < candidates[best].dSelf) best = i;
                }
                candidates[best].ideal = true;
                idealCount++;
            }
        }

        // The two sides select independently over ALL candidates (the union rule), so a rock can be
        // nearest on both sides — dedup is the ideal flag itself.
        private void MarkNearest(int take, bool bySelf)
        {
            Span<int> chosen = stackalloc int[take];
            var chosenCount = 0;
            for (var n = 0; n < take; n++)
            {
                var best = -1;
                for (var i = 0; i < candidateCount; i++)
                {
                    if (chosen[..chosenCount].IndexOf(i) >= 0) continue;
                    var d = bySelf ? candidates[i].dSelf : candidates[i].dEnemy;
                    if (best < 0 || d < (bySelf ? candidates[best].dSelf : candidates[best].dEnemy)) best = i;
                }
                if (best < 0) return;
                chosen[chosenCount++] = best;
                candidates[best].ideal = true;
            }
        }

        private void EvictDeadAndDeparted()
        {
            for (var s = 0; s < SlotCount; s++)
            {
                if (!slots[s].IsBound) continue;
                var found = IndexOf(slots[s]);
                if (!slots[s].IsLive || found < 0)
                {
                    slots[s] = default;
                    continue;
                }
                candidates[found].rostered = true;
            }
        }

        private void FillAndChallenge(ReadOnlySpan<AsteroidRef> bound)
        {
            while (true)
            {
                var challenger = BestUnrosteredIdeal();
                if (challenger < 0) return;

                var empty = EmptySlot();
                if (empty >= 0)
                {
                    Seat(challenger, empty);
                    continue;
                }

                var victim = WorstChallengeableSlot(bound);
                // Challengers only weaken from here, so the first failed challenge ends the round.
                if (victim < 0 || Score(challenger) + margin >= Score(IndexOf(slots[victim]))) return;
                Seat(challenger, victim);
            }
        }

        private void Seat(int candidate, int slot)
        {
            slots[slot] = candidates[candidate].rock;
            candidates[candidate].rostered = true;
        }

        // Every seated occupant survived EvictDeadAndDeparted, so it is in the current scan by construction.
        private void RefreshViews()
        {
            for (var s = 0; s < SlotCount; s++)
            {
                if (!slots[s].IsBound)
                {
                    views[s] = default;
                    continue;
                }
                var c = candidates[IndexOf(slots[s])];
                views[s] = new RockSlotView(c.pos, c.vel, c.radius, c.healthPct);
            }
        }

        private int BestUnrosteredIdeal()
        {
            var best = -1;
            for (var i = 0; i < candidateCount; i++)
            {
                if (!candidates[i].ideal || candidates[i].rostered) continue;
                if (best < 0 || Score(i) < Score(best)) best = i;
            }
            return best;
        }

        private int EmptySlot()
        {
            for (var s = 0; s < SlotCount; s++)
                if (!slots[s].IsBound)
                    return s;
            return -1;
        }

        /// <summary>The occupant a challenger may displace: worst score among slots that are neither
        /// sentence-bound nor part of the current ideal roster.</summary>
        private int WorstChallengeableSlot(ReadOnlySpan<AsteroidRef> bound)
        {
            var worst = -1;
            var worstScore = float.NegativeInfinity;
            for (var s = 0; s < SlotCount; s++)
            {
                var cand = IndexOf(slots[s]);
                if (candidates[cand].ideal || Contains(bound, slots[s])) continue;
                var score = Score(cand);
                if (score <= worstScore) continue;
                worst = s;
                worstScore = score;
            }
            return worst;
        }

        private float Score(int candidate) =>
            Mathf.Min(candidates[candidate].dSelf, candidates[candidate].dEnemy);

        private int IndexOf(in AsteroidRef rock)
        {
            for (var i = 0; i < candidateCount; i++)
                if (candidates[i].rock.Equals(rock))
                    return i;
            return -1;
        }

        private static bool Contains(ReadOnlySpan<AsteroidRef> bound, in AsteroidRef rock)
        {
            foreach (var b in bound)
                if (b.Equals(rock))
                    return true;
            return false;
        }
    }
}
