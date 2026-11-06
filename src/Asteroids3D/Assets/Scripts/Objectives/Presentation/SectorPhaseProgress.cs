namespace Objectives.Presentation
{
    /// <summary>What the countdown counts while a sector's hunt holds its opening lull.</summary>
    public enum HuntReading { None, Searching, Spotted }

    public readonly struct SectorPhaseProgress
    {
        public int Index { get; }
        public int Count { get; }
        public float SecondsLeft { get; }
        public bool ExtractionOpen { get; }
        public HuntReading Hunt { get; }

        public SectorPhaseProgress(int index, int count, float secondsLeft, bool extractionOpen,
            HuntReading hunt = HuntReading.None)
        {
            Index = index;
            Count = count;
            SecondsLeft = secondsLeft;
            ExtractionOpen = extractionOpen;
            Hunt = hunt;
        }
    }
}
