using System;
using Asteroids.Fields;
using Cameras;
using Ships;
using UnityEngine;
using World;

namespace Game.Session
{
    internal sealed class SessionContext
    {
        public SessionContext(SectorSessionConfig config, ShipRespawnRunner respawnRunner)
        {
            Config = config ?? throw new ArgumentNullException(nameof(config));
            RespawnRunner = respawnRunner ?? throw new ArgumentNullException(nameof(respawnRunner));
        }

        public SectorSessionConfig Config { get; }
        public ShipRespawnRunner RespawnRunner { get; }
        public WorldRoot World { get; set; }
        public UpdatingAsteroidField AsteroidField { get; set; }
        public CameraRig CameraRig { get; set; }
        public ShipRegistry ShipRegistry { get; set; }
        public Ship Player { get; set; }
        public Ship Enemy { get; set; }

        public Transform WorldFollowerTransform => World && World.Follower ? World.Follower.transform : null;
    }
}
