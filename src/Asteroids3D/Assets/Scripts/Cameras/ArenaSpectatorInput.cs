using System.Collections.Generic;
using System.Linq;
using Game;
using Ships;
using UnityEngine;

namespace Cameras
{
    /// <summary>
    /// Arena spectator camera input: edge-pan when unlocked, ship cycling,
    /// scroll zoom, and C-key toggle between follow and free-fly modes.
    /// </summary>
    [RequireComponent(typeof(ObserverCam))]
    public class ArenaSpectatorInput : MonoBehaviour
    {
        [Header("Edge Pan")]
        [SerializeField] private float edgePanSpeed = 30f;
        [SerializeField] private float edgeThreshold = 20f;

        [Header("Zoom")]
        [SerializeField] private float scrollZoomSpeed = 5f;
        [SerializeField] private float defaultZoom = 15f;

        [Header("Keys")]
        [SerializeField] private KeyCode toggleFreeKey = KeyCode.C;
        [SerializeField] private KeyCode cycleNextKey = KeyCode.Tab;

        private ObserverCam observerCam;
        private readonly List<Ship> ships = new();
        private int currentShipIndex;
        private bool freeMode;
        private Vector2 panPosition;
        private float currentZoom;

        private void Awake()
        {
            observerCam = GetComponent<ObserverCam>();
            currentZoom = defaultZoom;
        }

        public void SetShips(IEnumerable<Ship> allShips)
        {
            ships.Clear();
            ships.AddRange(allShips.Where(s => s));
            currentShipIndex = 0;
        }

        public void AddShip(Ship ship)
        {
            if (ship && !ships.Contains(ship))
                ships.Add(ship);
        }

        private void Update()
        {
            if (!observerCam) return;

            ships.RemoveAll(s => !s);

            HandleToggle();
            HandleShipCycling();
            HandleScrollZoom();

            if (freeMode)
                HandleEdgePan();
        }

        private void HandleToggle()
        {
            if (!Input.GetKeyDown(toggleFreeKey)) return;

            if (freeMode)
            {
                // Lock to nearest ship
                freeMode = false;
                var nearest = FindNearestShip(panPosition);
                if (nearest >= 0)
                {
                    currentShipIndex = nearest;
                    FocusOnShip(currentShipIndex);
                }
                observerCam.SetManualCenter(null);
                observerCam.SetLockCameraToSubject(true);
                observerCam.SetLockZoomToSubject(true);
            }
            else
            {
                // Enter free mode from current camera position
                freeMode = true;
                panPosition = observerCam.CurrentCenter2D;
                currentZoom = observerCam.Cam.orthographicSize;
                observerCam.SetLockCameraToSubject(false);
                observerCam.SetLockZoomToSubject(false);
                observerCam.SetManualCenter(panPosition);
                observerCam.SetManualZoom(currentZoom);
            }
        }

        private void HandleShipCycling()
        {
            if (ships.Count == 0) return;

            int targetIndex = -1;

            // Tab cycles forward
            if (Input.GetKeyDown(cycleNextKey))
            {
                targetIndex = (currentShipIndex + 1) % ships.Count;
            }

            // Number keys 1-9 select directly
            for (int i = 0; i < Mathf.Min(ships.Count, 9); i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1 + i))
                {
                    targetIndex = i;
                    break;
                }
            }

            if (targetIndex < 0) return;

            currentShipIndex = targetIndex;
            FocusOnShip(currentShipIndex);

            if (freeMode)
            {
                freeMode = false;
                observerCam.SetManualCenter(null);
                observerCam.SetLockCameraToSubject(true);
                observerCam.SetLockZoomToSubject(true);
            }
        }

        private void HandleScrollZoom()
        {
            var scroll = Input.mouseScrollDelta.y;
            if (Mathf.Approximately(scroll, 0f)) return;

            currentZoom -= scroll * scrollZoomSpeed;
            currentZoom = Mathf.Clamp(currentZoom, 5f, 50f);

            if (freeMode)
            {
                observerCam.SetManualZoom(currentZoom);
            }
            else
            {
                // In follow mode, adjust the lock zoom distance
                observerCam.SetManualZoom(currentZoom);
            }
        }

        private void HandleEdgePan()
        {
            var mousePos = Input.mousePosition;
            var dir = Vector2.zero;

            if (mousePos.x <= edgeThreshold) dir.x = -1f;
            else if (mousePos.x >= Screen.width - edgeThreshold) dir.x = 1f;

            if (mousePos.y <= edgeThreshold) dir.y = -1f;
            else if (mousePos.y >= Screen.height - edgeThreshold) dir.y = 1f;

            if (dir == Vector2.zero) return;

            panPosition += dir.normalized * (edgePanSpeed * Time.unscaledDeltaTime);
            observerCam.SetManualCenter(panPosition);
        }

        private void FocusOnShip(int index)
        {
            if (index < 0 || index >= ships.Count) return;
            var ship = ships[index];
            if (!ship) return;

            observerCam.SetSubject(ship.transform, removeOldSubject: false);
            panPosition = GamePlane.WorldPointToPlane(ship.transform.position);
        }

        private int FindNearestShip(Vector2 planePos)
        {
            int best = -1;
            float bestDist = float.MaxValue;

            for (int i = 0; i < ships.Count; i++)
            {
                if (!ships[i] || !ships[i].gameObject.activeInHierarchy) continue;
                var dist = (GamePlane.WorldPointToPlane(ships[i].transform.position) - planePos).sqrMagnitude;
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = i;
                }
            }

            return best;
        }
    }
}
