using System;
using UI.Projection;
using UnityEngine;

namespace UI.Screens.Windows
{
    /// <summary>
    /// The two cameras a screen draws through the canopy with: its light goes on the ink camera, which the
    /// canopy pass adds to the view, and its dark bodies and fade go on the gameplay camera beneath. Both
    /// canvases sit at one plane distance and the ink camera mirrors the gameplay camera, so they coincide.
    /// </summary>
    public sealed class ScreenCameras
    {
        private const float PlaneBeyondNear = .5f;

        private readonly int inkLayer;
        private readonly int underLayer;

        public Camera Ink { get; }
        public Camera Scene { get; }
        public CanopyProjection Projection { get; }

        public ScreenCameras(Camera ink, Camera scene, CanopyProjection projection)
        {
            if (!ink) throw new ArgumentNullException(nameof(ink));
            if (!scene) throw new ArgumentNullException(nameof(scene));
            if (!projection) throw new ArgumentNullException(nameof(projection));
            Ink = ink;
            Scene = scene;
            Projection = projection;
            inkLayer = LayerMask.NameToLayer("UIBloom");
            underLayer = LayerMask.NameToLayer("UI");
            if (inkLayer < 0 || underLayer < 0)
                throw new InvalidOperationException("Screens need the UIBloom and UI layers.");
        }

        internal void ConfigureInk(Canvas canvas) => Configure(canvas, Ink, inkLayer);

        internal void ConfigureUnder(Canvas canvas) => Configure(canvas, Scene, underLayer);

        private void Configure(Canvas canvas, Camera camera, int layer)
        {
            canvas.gameObject.layer = layer;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = Scene.nearClipPlane + PlaneBeyondNear;
        }
    }
}
