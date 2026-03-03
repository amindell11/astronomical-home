namespace Game.Session
{
    public readonly struct SectorSessionResult
    {
        public SectorSessionResult(bool started, SessionContext context, string error)
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
            return new SectorSessionResult(true, context, null);
        }

        public static SectorSessionResult Failed(string error)
        {
            return new SectorSessionResult(false, null, error);
        }
    }
}
