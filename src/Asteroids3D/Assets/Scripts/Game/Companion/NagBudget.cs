using System;
using UI.Comms;

namespace Game.Companion
{
    /// <summary>
    /// Holds at most one waiting line and decides when it may speak: one line per interval, must-say lines
    /// skip the wait, a plain line never cuts one still held, and a line that cannot speak before it goes
    /// stale is dropped, never queued. A follow-up accepted with a line speaks when that line's hold ends,
    /// and the gap between lines then counts from it.
    /// Reads the feel profile live, so tuning applies mid-playtest. Times are unscaled seconds, passed in.
    /// </summary>
    public sealed class NagBudget
    {
        private readonly CompanionFeelProfile feel;
        private float lastSpokenAt = float.NegativeInfinity;
        private float heldUntil = float.NegativeInfinity;
        private CommMessage? waiting;
        private bool waitingMustSay;
        private float offeredAt;
        private CommMessage? waitingFollowUp;
        private CommMessage? armed;
        private float armedDueAt;

        public NagBudget(CompanionFeelProfile feel)
        {
            if (!feel) throw new ArgumentNullException(nameof(feel));
            this.feel = feel;
        }

        public void Offer(CommMessage line, bool mustSay, float now, CommMessage? followUp)
        {
            if (!mustSay && (now - lastSpokenAt < feel.MinLineIntervalSeconds || now < heldUntil ||
                    waiting != null && waitingMustSay))
                return;
            waiting = line;
            waitingMustSay = mustSay;
            offeredAt = now;
            waitingFollowUp = followUp;
        }

        public bool TryTake(float now, bool canSpeak, out CommMessage line)
        {
            line = default;
            if (armed != null && now >= armedDueAt)
            {
                line = armed.Value;
                armed = null;
                Spoke(line, now);
                return true;
            }

            if (waiting == null) return false;
            if (now - offeredAt > feel.StaleSeconds)
            {
                waiting = null;
                waitingFollowUp = null;
                return false;
            }

            if (!canSpeak) return false;

            line = waiting.Value;
            var followUp = waitingFollowUp;
            waiting = null;
            waitingFollowUp = null;
            armed = followUp;
            if (followUp != null)
                armedDueAt = now + feel.HoldFor(line.Plain);
            Spoke(line, now);
            return true;
        }

        private void Spoke(CommMessage line, float now)
        {
            lastSpokenAt = now;
            heldUntil = now + feel.HoldFor(line.Plain);
        }
    }
}
