using System;
using System.Collections.Generic;
using Cameras;
using Ships;
using UnityEngine;

namespace UI.Recap
{
    /// <summary>
    /// Applies recap world presentation to the HUD, ship bars and observer, preserving prior framing.
    /// Defeat follows the captured death point; victory holds the current view. Screen playback can
    /// run without this presenter when travel already owns the camera and HUD.
    /// </summary>
    public sealed class RecapPresenter
    {
        private readonly Overlay overlay;
        private readonly IEnumerable<Ship> ships;
        private readonly ObserverCam observer;
        private readonly bool cameraWasEnabled;
        private readonly Vector2? deathPoint;
        private readonly float deathZoom;
        private readonly Vector2? previousCenter;
        private readonly float? previousZoom;

        public RecapPresenter(RecapOutcomeContext context, Overlay overlay, IEnumerable<Ship> ships, ObserverCam observer)
        {
            this.overlay = overlay;
            this.ships = ships ?? Array.Empty<Ship>();
            this.observer = observer;
            cameraWasEnabled = observer.enabled;
            deathPoint = context.Outcome == RecapOutcome.Defeat ? context.DeathPoint : null;
            deathZoom = observer.Cam.orthographicSize;
            previousCenter = observer.ManualCenter;
            previousZoom = observer.ManualZoom;
        }

        public void Apply(RecapState state) => ApplyWorld(state.HudAlpha, state.ShipHudAlpha, state.CameraHeld);

        public void Restore() => ApplyWorld(1f, 1f, false);

        private void ApplyWorld(float hudAlpha, float shipHudAlpha, bool cameraHeld)
        {
            if (overlay)
            {
                overlay.SetVisible(hudAlpha > 0f);
                overlay.SetOpacity(hudAlpha, shipHudAlpha);
            }
            foreach (var ship in ships)
                if (ship && ship.VisualRig) ship.VisualRig.SetHudOpacity(shipHudAlpha);
            if (!observer) return;
            var followDeath = cameraHeld && deathPoint.HasValue;
            observer.SetManualCenter(followDeath ? deathPoint : previousCenter);
            observer.SetManualZoom(followDeath ? deathZoom : previousZoom);
            observer.enabled = cameraWasEnabled && (!cameraHeld || followDeath);
        }
    }
}
