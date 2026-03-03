using System;
using Player;
using Ships;
using UnityEngine;

namespace Game.Session
{
    internal sealed class SessionRuntimeValidator
    {
        public void ValidateRuntimeWiring(
            SessionContext context,
            ShipRespawnRunner respawnRunner,
            Transform referencePlane,
            Action<Ship> validateShipWiring)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            if (!respawnRunner)
                throw new InvalidOperationException("ShipRespawnRunner must be initialized before gameplay starts.");
            if (!referencePlane)
                throw new InvalidOperationException("GameInitiator requires a serialized reference plane Transform.");
            if (validateShipWiring == null)
                throw new ArgumentNullException(nameof(validateShipWiring));

            validateShipWiring(context.Player);
            if (context.Enemy)
                validateShipWiring(context.Enemy);

            if (context.Player?.Commander is PlayerCommander { HasScreenProjectorConfigured: false })
                throw new InvalidOperationException("PlayerCommander requires a configured screen-to-plane projector.");

            if (!respawnRunner.IsInitialized)
                throw new InvalidOperationException("ShipRespawnRunner must be initialized before gameplay starts.");

            if (GamePlane.Plane != referencePlane)
                throw new InvalidOperationException("GamePlane must be configured from the serialized reference plane.");
        }
    }
}
