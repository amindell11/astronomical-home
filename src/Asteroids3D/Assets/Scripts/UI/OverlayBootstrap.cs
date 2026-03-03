using System;
using Game;
using Ships;
using UnityEngine;

namespace UI
{
    public class OverlayBootstrap : MonoBehaviour
    {
        [SerializeField] private GameInitiator gameInitiator;
        [SerializeField] private Overlay overlayPrefab;

        private Overlay overlay;

        private void Awake()
        {
            if (!gameInitiator)
                throw new InvalidOperationException("OverlayBootstrap requires a serialized GameInitiator reference.");
            if (!overlayPrefab)
                throw new InvalidOperationException("OverlayBootstrap requires a serialized overlay prefab.");

            gameInitiator.PresentationReady += HandlePresentationReady;
        }

        private void OnDestroy()
        {
            if (gameInitiator)
                gameInitiator.PresentationReady -= HandlePresentationReady;

            if (overlay)
                Destroy(overlay.gameObject);
        }

        private void HandlePresentationReady(Ship playerShip, Camera uiCamera)
        {
            if (!playerShip)
                return;

            if (overlay)
                Destroy(overlay.gameObject);

            overlay = Instantiate(overlayPrefab);
            overlay.SetCanvasWorldCamera(uiCamera);
            overlay.Initialize(playerShip);
        }
    }
}
