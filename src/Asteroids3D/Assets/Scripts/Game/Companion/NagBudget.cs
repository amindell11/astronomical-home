using System;

namespace Game.Companion
{
    /// <summary>
    /// Holds at most one waiting line and decides when it may speak: one line per interval, must-say lines
    /// skip the wait, and a line that cannot speak before it goes stale is dropped, never queued. A follow-up
    /// accepted with a line speaks when that line's hold ends, and the gap between lines then counts from it.
    /// Reads the feel profile live, so tuning applies mid-playtest. Times are unscaled seconds, passed in.
    /// </summary>
    public sealed class NagBudget
    {
        private readonly CompanionFeelProfile feel;
        private float lastSpokenAt = float.NegativeInfinity;
        private string waiting;
        private bool waitingMustSay;
        private float offeredAt;
        private string waitingFollowUp;
        private string armed;
        private float armedDueAt;

        public NagBudget(CompanionFeelProfile feel)
        {
            if (!feel) throw new ArgumentNullException(nameof(feel));
            this.feel = feel;
        }

        public void Offer(string line, bool mustSay, float now, string followUp)
        {
            if (line == null) throw new ArgumentNullException(nameof(line));
            if (!mustSay && (now - lastSpokenAt < feel.MinLineIntervalSeconds || waiting != null && waitingMustSay))
                return;
            waiting = line;
            waitingMustSay = mustSay;
            offeredAt = now;
            waitingFollowUp = followUp;
        }

        public bool TryTake(float now, bool canSpeak, out string line)
        {
            line = null;
            if (armed != null && now >= armedDueAt)
            {
                line = armed;
                armed = null;
                lastSpokenAt = now;
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

            line = waiting;
            var followUp = waitingFollowUp;
            waiting = null;
            waitingFollowUp = null;
            armed = followUp;
            if (followUp != null)
                armedDueAt = now + feel.HoldSeconds;
            lastSpokenAt = now;
            return true;
        }
    }
}
