namespace Objectives.Presentation
{
    /// <summary>
    /// What the HUD reads while a sector's hunt holds its opening lull.
    /// Searching and Spotted count the search clock, Coming the gap after the alarm.
    /// </summary>
    public enum HuntReading { None, Searching, Spotted, Coming }

    public readonly struct SectorPhaseProgress
    {
        public int Index { get; }
        public int Count { get; }
        public float SecondsLeft { get; }
        public bool ExtractionOpen { get; }
        public HuntReading Hunt { get; }
        public float SpottedFill { get; }
        public float BeaconFill { get; }
        public bool BeaconExpiring { get; }

        public SectorPhaseProgress(int index, int count, float secondsLeft, bool extractionOpen,
            HuntReading hunt = HuntReading.None, float spottedFill = 0f, float beaconFill = 1f, bool beaconExpiring = false)
        {
            Index = index;
            Count = count;
            SecondsLeft = secondsLeft;
            ExtractionOpen = extractionOpen;
            Hunt = hunt;
            SpottedFill = spottedFill;
            BeaconFill = beaconFill;
            BeaconExpiring = beaconExpiring;
        }
    }
}
