namespace Game.Sectors
{
    public readonly struct SectorResult
    {
        public bool Success { get; }
        public string FailReason { get; }

        private SectorResult(bool success, string failReason)
        {
            Success = success;
            FailReason = failReason;
        }

        public static SectorResult Extracted() => new(true, null);
        public static SectorResult Failed(string reason) => new(false, reason);
    }
}
