using Cameras;
using Game.Services;
using Player;
using Ships;
using Ships.Command;
using UnityEngine;
namespace Game.Sectors.Utils
{
    public class SectorUtils
    {
        public static Ship BuildAndWirePlayer(Ship playerTemplate, Commander playerCommander, int team, Vector2  playerSpawnPosition, IGameServices services)
        {
            var player = services.UnitService.SpawnShip(
                playerTemplate,
                playerCommander,
                0,
                services.Arena.Place(playerSpawnPosition),
                GamePlane.Rotation);
            player.tag = "Player";
            services.EnvironmentService.World.Follower?.SetTarget(player.transform);
            var observer = services.CameraService.GetCamera<ObserverCam>(CameraTag.Observer);
            if (!observer) return player;
            
            observer?.SetSubject(player.transform);
            (player.Commander as PlayerCommander)?.SetScreenToGamePlane(pos =>
                GamePlane.ProjectOntoPlane(observer.Cam.ScreenToWorldPoint(pos))
                + GamePlane.Origin);
            
            return player;
        }

        public static ObserverCam BuildAndWireObserverCam(IGameServices services,  ObserverCam observerCamPrefab)
        {
            var observer = Object.Instantiate(observerCamPrefab);
            services.CameraService.Initialize();
            services.CameraService.AddCamera(CameraTag.Observer,observer);
            
            var registry = services.UnitService.ActiveRegistry;
            if (registry == null)
                return observer;

            registry.ActiveShips.OnAdd += s => observer.AddSecondarySubject(s.transform);
            registry.ActiveShips.OnRemove += s => observer.RemoveSecondarySubject(s.transform);
            return observer;
        }
    }
} 

