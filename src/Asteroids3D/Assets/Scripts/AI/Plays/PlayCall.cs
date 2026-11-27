using System;
using System.Collections.Generic;
using Movement;
using Ships;

namespace AI.Plays
{
    /// <summary>
    /// The play one wave shares. The rival advances the read; the wave's ships take their orders from here.
    /// </summary>
    public sealed class PlayCall
    {
        private readonly List<Ship> members = new();
        private readonly Random random;
        private bool clocked;
        private float imposeStart;
        private float stageStart;
        private float frozenImposed;
        private float answeredFor;
        private PlayRead read;
        private Ship spotted;
        private UnityEngine.Vector2 bearing;

        public PlayCall(Ship rival, WavePlay[] book, Random random)
        {
            if (book == null || book.Length == 0)
                throw new ArgumentException("A wave play needs a book with at least one play.", nameof(book));
            Rival = rival;
            Play = book[random.Next(book.Length)];
            this.random = random;
            Mirror = random.Next(2) == 0 ? -1 : 1;
            Stage = PlayStage.Imposing;
        }

        internal Ship Rival { get; }
        internal WavePlay Play { get; }
        internal PlayStage Stage { get; private set; }
        internal int Mirror { get; private set; }

        internal bool Broken => !Rival || !Rival.gameObject.activeInHierarchy;

        internal void Join(Ship minion) => members.Add(minion);

        internal int RankOf(Ship minion)
        {
            var rank = 0;
            for (var i = 0; i < members.Count; i++)
            {
                var ship = members[i];
                var flying = ship && ship.gameObject.activeInHierarchy;
                if (flying) rank++;
                if (!ReferenceEquals(ship, minion)) continue;
                if (!flying)
                    throw new InvalidOperationException("A play ranks a minion that is flying.");
                return rank;
            }

            throw new InvalidOperationException("A play ranks only a minion that joined it.");
        }

        internal int Flying
        {
            get
            {
                var count = 0;
                for (var i = 0; i < members.Count; i++)
                {
                    var ship = members[i];
                    if (ship && ship.gameObject.activeInHierarchy) count++;
                }

                return count;
            }
        }

        internal PlayView View(in Kinematics rival, in Kinematics target, float now)
        {
            var stageSeconds = clocked ? now - stageStart : 0f;
            var imposedSeconds = !clocked || Stage == PlayStage.Searching
                ? 0f
                : Stage is PlayStage.AnswerA or PlayStage.AnswerB or PlayStage.Reforming
                    ? frozenImposed
                    : now - imposeStart;
            return new PlayView(rival, target, Stage, stageSeconds, imposedSeconds, Mirror, Flying, read,
                answeredFor, clocked ? bearing : (target.pos - rival.pos).normalized);
        }

        internal void Command(in Kinematics rival, in Kinematics target, float now)
        {
            if (!clocked || Stage == PlayStage.Searching)
            {
                BeginImpose(now, rival, target);
                return;
            }

            if (Stage == PlayStage.Imposing)
            {
                var taken = Play.Read(View(rival, target, now));
                if (taken == PlayRead.None) return;
                read = taken;
                Enter(PlayStage.Committing, now);
                return;
            }

            if (Stage == PlayStage.Committing)
            {
                if (now - stageStart < Play.readDelay) return;
                frozenImposed = now - imposeStart;
                if (read != PlayRead.A && read != PlayRead.B)
                    throw new InvalidOperationException("A committing play has no read.");
                Enter(read == PlayRead.A ? PlayStage.AnswerA : PlayStage.AnswerB, now);
                return;
            }

            if (Stage == PlayStage.AnswerA || Stage == PlayStage.AnswerB)
            {
                if (now - stageStart < Play.AnswerSeconds) return;
                answeredFor = now - stageStart;
                Enter(PlayStage.Reforming, now);
                return;
            }

            if (Stage != PlayStage.Reforming)
                throw new InvalidOperationException($"A play call cannot be commanded while {Stage}.");
            if (now - stageStart < Play.reformSeconds) return;
            Mirror = random.Next(2) == 0 ? -1 : 1;
            BeginImpose(now, rival, target);
        }

        internal void LoseTarget()
        {
            if (Stage == PlayStage.Searching) return;
            Stage = PlayStage.Searching;
            clocked = false;
        }

        internal void Spot(Ship target) => spotted = target;

        internal bool TryTakeSpotted(out Ship target)
        {
            target = spotted;
            spotted = null;
            if (target) return true;
            return false;
        }

        private void BeginImpose(float now, in Kinematics rival, in Kinematics target)
        {
            bearing = (target.pos - rival.pos).normalized;
            Stage = PlayStage.Imposing;
            imposeStart = now;
            stageStart = now;
            clocked = true;
            read = PlayRead.None;
        }

        private void Enter(PlayStage stage, float now)
        {
            Stage = stage;
            stageStart = now;
        }
    }
}
