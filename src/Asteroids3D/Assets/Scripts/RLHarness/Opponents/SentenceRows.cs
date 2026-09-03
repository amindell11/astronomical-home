using System;
using AI;
using Ships;
using UnityEngine;

namespace Game.RLHarness
{
    /// <summary>The six session-confirmed tactic-bingo rows (#485 §Stage A brief, fork 3); the other bingo rows are rig-only by ruling.</summary>
    public enum SentenceRow { Orbit, Kite, CoverTake, FireLaneDodge, DummyCloseout, DriftHold }

    /// <summary>Stage A3's hand-authored sentence per session row: the fixed intent vector, the scripted opponent that stages the tactic, and the trigger stance. Vectors are starting points that live only here in code — the bingo run (#395) iterates them; verdicts freeze into the plan doc.</summary>
    public static class SentenceRows
    {
        // Hold-ring radii sit against the 20 u laser envelope: orbit/kite inside it, cover-take
        // deliberately outside (the read is whether rocks mediate the retreat), closeout well inside.
        private const float OrbitRing = 16f;
        private const float OrbitTangentialSpeed = 8f;
        private const float KiteRing = 18f;
        private const float CoverRing = 30f;
        private const float DodgeOffAxisRange = 12f;
        private const float CloseoutRing = 6f;

        public static readonly SentenceRow[] SessionRows =
        {
            SentenceRow.Orbit, SentenceRow.Kite, SentenceRow.CoverTake,
            SentenceRow.FireLaneDodge, SentenceRow.DummyCloseout, SentenceRow.DriftHold,
        };

        public static string Token(SentenceRow row) => row switch
        {
            SentenceRow.Orbit => "orbit",
            SentenceRow.Kite => "kite",
            SentenceRow.CoverTake => "cover-take",
            SentenceRow.FireLaneDodge => "fire-lane-dodge",
            SentenceRow.DummyCloseout => "dummy-closeout",
            SentenceRow.DriftHold => "drift-hold",
            _ => throw new ArgumentOutOfRangeException(nameof(row), row, null),
        };

        public static bool TryParse(string token, out SentenceRow row)
        {
            foreach (var candidate in SessionRows)
            {
                if (!string.Equals(token, Token(candidate), StringComparison.OrdinalIgnoreCase)) continue;
                row = candidate;
                return true;
            }
            row = default;
            return false;
        }

        public static string RowTokens => string.Join(", ", Array.ConvertAll(SessionRows, Token));

        /// <summary>The scripted opponent staging each row: stationary for the self-referential rows, an approacher to kite off, a range-holding shooter whose fire lane the dodge row plays against. The block label is the row token — two rows share the Dummy, and probe pools key on the label.</summary>
        public static OpponentSpec Block(SentenceRow row) => row switch
        {
            SentenceRow.Orbit => Staged(row, OpponentArchetype.Dummy),
            SentenceRow.Kite => Staged(row, OpponentArchetype.Aggressor),
            SentenceRow.CoverTake => Staged(row, OpponentArchetype.Aggressor),
            SentenceRow.FireLaneDodge => Staged(row, OpponentArchetype.Kiter),
            SentenceRow.DummyCloseout => Staged(row, OpponentArchetype.Dummy),
            SentenceRow.DriftHold => Staged(row, OpponentArchetype.Dummy),
            _ => throw new ArgumentOutOfRangeException(nameof(row), row, null),
        };

        private static OpponentSpec Staged(SentenceRow row, OpponentArchetype archetype) =>
            OpponentSpec.Sentence(archetype, Token(row));

        /// <summary>Only the closeout row fires — its success criterion is the kill; every other row reads movement composition without damage confounds.</summary>
        public static bool Engages(SentenceRow row) => row == SentenceRow.DummyCloseout;

        /// <summary>The row's fixed sentence, bound to the one session anchor. FIELD rides at the legacy authority 1 everywhere except drift-hold; weights stay in the hand-vector convention [−1, 1].</summary>
        public static NavObjective Author(SentenceRow row, ShipId anchor) => row switch
        {
            SentenceRow.Orbit => NavObjective.Anchored(anchor)
                .Facing(0f, 1f)
                .Position(0f, 0f, OrbitRing, 1f)
                .Velocity(0f, OrbitTangentialSpeed, 1f)
                .Field(1f),
            SentenceRow.Kite => NavObjective.Anchored(anchor)
                .Facing(0f, 1f)
                .Position(0f, 0f, KiteRing, 1f)
                .Field(1f),
            SentenceRow.CoverTake => NavObjective.Anchored(anchor)
                .Facing(0f, 0.5f)
                .Position(0f, 0f, CoverRing, 1f)
                .Field(1f),
            // A point held 90° CCW off the shooter's nose: off the boresight is the dodge.
            SentenceRow.FireLaneDodge => NavObjective.Anchored(anchor)
                .Facing(0f, 1f)
                .Position(DodgeOffAxisRange, Mathf.PI / 2f, 0f, 1f, Movement.MPC.ReferentFrame.Facing)
                .Field(1f),
            SentenceRow.DummyCloseout => NavObjective.Anchored(anchor)
                .Facing(0f, 1f)
                .Position(0f, 0f, CloseoutRing, 1f)
                .Field(1f),
            // All four slots armed at weight 0 — "nothing matters" is a sentence, absence of one is not
            // (never NavObjective.Drift): the solver still runs on the priors (A1's IsIdle generalization).
            SentenceRow.DriftHold => NavObjective.Anchored(anchor)
                .Facing(0f, 0f)
                .Position(0f, 0f, 0f, 0f)
                .Velocity(0f, 0f, 0f)
                .Field(0f),
            _ => throw new ArgumentOutOfRangeException(nameof(row), row, null),
        };
    }
}
