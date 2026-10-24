using System;
using System.Collections.Generic;
using Cameras;
using Ships;
using UI.Screens;

namespace UI.Recap
{
    /// <summary>
    /// Applies a recap state to the real renderers: the screen, the player's overlay, every ship's
    /// status bars and the observer camera. The game and the preview host each build one over their
    /// own instances, so neither renders the recap any other way.
    /// </summary>
    public sealed class RecapPresenter
    {
        private readonly RunRecapScreen screen;
        private readonly Overlay overlay;
        private readonly IEnumerable<Ship> ships;
        private readonly ObserverCam observer;
        private readonly bool cameraWasEnabled;

        public RecapPresenter(RunRecapScreen screen, Overlay overlay, IEnumerable<Ship> ships, ObserverCam observer)
        {
            this.screen = screen;
            this.overlay = overlay;
            this.ships = ships ?? Array.Empty<Ship>();
            this.observer = observer;
            cameraWasEnabled = observer && observer.enabled;
        }

        public void Apply(RecapState state)
        {
            ApplyWorld(state.HudAlpha, state.ShipHudAlpha, state.CameraHeld);
            screen.Apply(state);
        }

        public void Restore() => ApplyWorld(1f, 1f, false);

        private void ApplyWorld(float hudAlpha, float shipHudAlpha, bool cameraHeld)
        {
            if (overlay)
            {
                overlay.SetVisible(hudAlpha > 0f);
                overlay.SetOpacity(hudAlpha);
            }
            foreach (var ship in ships)
                if (ship && ship.VisualRig) ship.VisualRig.SetHudOpacity(shipHudAlpha);
            if (observer) observer.enabled = cameraWasEnabled && !cameraHeld;
        }
    }
}
