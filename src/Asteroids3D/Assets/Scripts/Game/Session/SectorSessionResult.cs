using System;

namespace Game.Session
{
    internal readonly struct SectorSessionResult
    {
        private SectorSessionResult(bool started, SessionContext context, string error)
        {
            Started = started;
            Context = context;
            Error = error;
        }

        public bool Started { get; }
        public SessionContext Context { get; }
        public string Error { get; }

        public static SectorSessionResult Success(SessionContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            return new SectorSessionResult(true, context, null);
        }

        public static SectorSessionResult Failed(string error)
        {
            if (string.IsNullOrWhiteSpace(error))
                throw new ArgumentException("Error message must not be null or whitespace.", nameof(error));
            return new SectorSessionResult(false, null, error);
        }
    }
}
