namespace Objectives.Presentation
{
    public readonly struct SectorPhaseProgress
    {
        public int Index { get; }
        public int Count { get; }
        public float SecondsLeft { get; }
        public bool ExtractionOpen { get; }

        public SectorPhaseProgress(int index, int count, float secondsLeft, bool extractionOpen)
        {
            Index = index;
            Count = count;
            SecondsLeft = secondsLeft;
            ExtractionOpen = extractionOpen;
        }
    }
}
