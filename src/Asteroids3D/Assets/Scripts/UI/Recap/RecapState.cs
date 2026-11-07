namespace UI.Recap
{
    public readonly struct RecapState
    {
        public RecapPhase Phase { get; }
        public float HudAlpha { get; }
        public float ShipHudAlpha { get; }
        public bool CameraHeld { get; }
        public bool SimulationFrozen { get; }
        public float BannerAlpha { get; }
        public bool BannerPrompt { get; }
        public float BreakdownAlpha { get; }
        public bool BreakdownInteractable { get; }

        public RecapState(RecapPhase phase, float hudAlpha, float shipHudAlpha, bool cameraHeld, bool simulationFrozen,
            float bannerAlpha, bool bannerPrompt, float breakdownAlpha, bool breakdownInteractable)
        {
            Phase = phase;
            HudAlpha = hudAlpha;
            ShipHudAlpha = shipHudAlpha;
            CameraHeld = cameraHeld;
            SimulationFrozen = simulationFrozen;
            BannerAlpha = bannerAlpha;
            BannerPrompt = bannerPrompt;
            BreakdownAlpha = breakdownAlpha;
            BreakdownInteractable = breakdownInteractable;
        }
    }
}
