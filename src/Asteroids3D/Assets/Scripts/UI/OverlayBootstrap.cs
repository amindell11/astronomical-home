using System;
using Game;
using Ships;
using UnityEngine;

namespace UI
{
    public class OverlayBootstrap : MonoBehaviour
    {
        [SerializeField] private GameInitiator gameInitiator;
        [SerializeField] private GameConfig gameConfig;

        private Overlay overlay;

        private void Awake()
        {
            if (!gameInitiator)
                throw new InvalidOperationException("OverlayBootstrap requires a serialized GameInitiator reference.");
            if (!gameConfig || !gameConfig.UI)
                throw new InvalidOperationException("OverlayBootstrap requires a GameConfig with a UI overlay prefab.");

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

            overlay = Instantiate(gameConfig.UI);
            overlay.SetCanvasWorldCamera(uiCamera);
            overlay.Initialize(playerShip);
        }
    }
}
