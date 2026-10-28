namespace Substrate.Sectors
{
    public readonly struct SectorResult
    {
        public bool Success { get; }
        public string FailReason { get; }
        public float? DepartureHeading { get; }

        private SectorResult(bool success, string failReason, float? departureHeading)
        {
            Success = success;
            FailReason = failReason;
            DepartureHeading = departureHeading;
        }

        public static SectorResult Extracted(float departureHeading) => new(true, null, departureHeading);
        public static SectorResult Failed(string reason) => new(false, reason, null);
    }
}
