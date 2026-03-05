using System;
using Game.Bootstrap;
using Ships;
using UnityEngine;

namespace UI
{
    public class OverlayBootstrap : MonoBehaviour
    {
        [SerializeField] private MainGameManager gameManager;
        [SerializeField] private Overlay overlayPrefab;

        private Overlay overlay;

        private void Awake()
        {
            if (!gameManager)
                throw new InvalidOperationException("OverlayBootstrap requires a serialized MainGameManager reference.");
            if (!overlayPrefab)
                throw new InvalidOperationException("OverlayBootstrap requires a serialized overlay prefab.");
        }

        private void OnDestroy()
        {
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
