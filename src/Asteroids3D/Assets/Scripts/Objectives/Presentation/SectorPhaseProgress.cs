namespace Objectives.Presentation
{
    /// <summary>
    /// What the HUD reads while a sector's hunt holds its opening lull.
    /// Searching and Spotted count the search clock, Coming the gap after the alarm.
    /// </summary>
    public enum HuntReading { None, Searching, Spotted, Coming }

    public enum PhaseCaption { NextWave, Lull, Elite, Extraction }

    /// <summary>
    /// What the HUD reads for the sector clock. SecondsLeft is the time until the phase table
    /// is done, except during a hunt reading, which keeps the search clock or the gap.
    /// The caption names the next wave, a lull, the elite, or extraction.
    /// </summary>
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
        public PhaseCaption Caption { get; }
        public float CaptionSeconds { get; }

        public SectorPhaseProgress(int index, int count, float secondsLeft, bool extractionOpen,
            HuntReading hunt = HuntReading.None, float spottedFill = 0f, float beaconFill = 1f,
            bool beaconExpiring = false, PhaseCaption caption = PhaseCaption.NextWave, float captionSeconds = 0f)
        {
            Index = index;
            Count = count;
            SecondsLeft = secondsLeft;
            ExtractionOpen = extractionOpen;
            Hunt = hunt;
            SpottedFill = spottedFill;
            BeaconFill = beaconFill;
            BeaconExpiring = beaconExpiring;
            Caption = caption;
            CaptionSeconds = captionSeconds;
        }
    }
}
