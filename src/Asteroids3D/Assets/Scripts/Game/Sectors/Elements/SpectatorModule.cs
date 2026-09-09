using System.Collections;
using System.Collections.Generic;
using Cameras;
using Ships;
using UnityEngine;

namespace Game.Sectors.Elements
{
    /// <summary>
    /// Spectator-camera behavior, relocated from <c>ArenaSector</c>. Swaps the observer camera's
    /// default <see cref="ObserverCamInputHandler"/> for an <see cref="ArenaSpectatorInput"/>, feeds
    /// it the player + all active ships (so the spectator can cycle between them), and locks the
    /// camera to its subject. Reads <see cref="SectorBuildContext.Player"/> and the active ships from
    /// the unit service; the observer camera is resolved through the camera service.
    /// </summary>
    public class SpectatorModule : SectorModule
    {
        private ArenaSpectatorInput spectatorInput;

        public override IEnumerator Setup(SectorBuildContext ctx)
        {
            var observer = ctx.Services.CameraService.GetCamera<ObserverCam>(CameraTag.Observer);
            if (!observer) yield break;

            // Remove the default observer input; the arena drives the camera itself.
            var defaultInput = observer.GetComponent<ObserverCamInputHandler>();
            if (defaultInput) Destroy(defaultInput);

            spectatorInput = observer.gameObject.AddComponent<ArenaSpectatorInput>();

            var allShips = new List<Ship>();
            if (ctx.Player) allShips.Add(ctx.Player);
            foreach (var s in ctx.Services.UnitService.ActiveRegistry.ActiveShips)
                if (s && s != ctx.Player) allShips.Add(s);
            spectatorInput.SetShips(allShips);

            // Start locked to the (player) subject.
            observer.SetLockCameraToSubject(true);
            observer.SetLockZoomToSubject(true);
        }

        public override IEnumerator Teardown(SectorBuildContext ctx)
        {
            if (spectatorInput) Destroy(spectatorInput);
            spectatorInput = null;
            yield break;
        }
    }
}
