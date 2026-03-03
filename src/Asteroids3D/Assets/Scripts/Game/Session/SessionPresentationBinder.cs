using System;
using System.Linq;
using Player;
using Ships;
using UnityEngine;

namespace Game.Session
{
    internal sealed class SessionPresentationBinder
    {
        private readonly Func<Transform> worldFollowerProvider;
        private SessionContext sessionContext;

        public SessionPresentationBinder(Func<Transform> worldFollowerProvider)
        {
            this.worldFollowerProvider = worldFollowerProvider ?? throw new ArgumentNullException(nameof(worldFollowerProvider));
        }

        public void Bind(SessionContext context)
        {
            sessionContext = context ?? throw new ArgumentNullException(nameof(context));
            var config = context.Config.GameConfig;

            InitializeCamera(config);
            InitializeAsteroidField(config);
            ConfigurePlayerInputProjection();
        }

        public void Unbind()
        {
            if (sessionContext?.ShipRegistry != null)
            {
                sessionContext.ShipRegistry.ActiveShips.OnAdd -= OnShipAddedToRegistry;
                sessionContext.ShipRegistry.ActiveShips.OnRemove -= OnShipRemovedFromRegistry;
            }

            sessionContext = null;
        }

        private void InitializeCamera(GameConfig config)
        {
            if (!config.CameraRig)
                return;

            sessionContext.CameraRig = UnityEngine.Object.Instantiate(config.CameraRig);

            var cameraFollow = sessionContext.CameraRig.ObserverCam;
            if (sessionContext.Player)
                cameraFollow.SetSubject(sessionContext.Player.transform);

            if (sessionContext.ShipRegistry != null)
            {
                cameraFollow.AddSecondarySubjects(sessionContext.ShipRegistry.ActiveShips.Where(s => s != sessionContext.Player).Select(s => s.transform));
                sessionContext.ShipRegistry.ActiveShips.OnAdd += OnShipAddedToRegistry;
                sessionContext.ShipRegistry.ActiveShips.OnRemove += OnShipRemovedFromRegistry;
            }
        }

        private void OnShipAddedToRegistry(Ship ship)
        {
            if (!ship) return;
            sessionContext?.CameraRig?.ObserverCam?.AddSecondarySubject(ship.transform);
        }

        private void OnShipRemovedFromRegistry(Ship ship)
        {
            if (!ship) return;
            sessionContext?.CameraRig?.ObserverCam?.RemoveSecondarySubject(ship.transform);
        }

        private void InitializeAsteroidField(GameConfig config)
        {
            if (!config.AsteroidAsteroidField)
                return;

            var cullingBoundary = sessionContext.World ? sessionContext.World.AsteroidCullingBoundary : null;
            sessionContext.AsteroidField = UnityEngine.Object.Instantiate(config.AsteroidAsteroidField);
            sessionContext.AsteroidField.Initialize(cullingBoundary);
            sessionContext.AsteroidField.SetWorldAnchor(worldFollowerProvider());

            if (sessionContext.CameraRig)
                sessionContext.AsteroidField.CurrentAnchorPos = () => GamePlane.ProjectOntoPlane(sessionContext.CameraRig.transform.position);
        }

        private void ConfigurePlayerInputProjection()
        {
            if (sessionContext?.Player?.Commander is not PlayerCommander playerCommander)
                return;
            if (!sessionContext.CameraRig)
                return;

            playerCommander.SetScreenToGamePlane(pos =>
                GamePlane.ProjectOntoPlane(sessionContext.CameraRig.MainCamera.ScreenToWorldPoint(pos)) + GamePlane.Origin);
        }
    }
}
