using System;
using UnityEngine;

namespace Game.Session
{
    internal sealed class SessionObjectGraphDisposer
    {
        public void DestroySessionObjects(SessionContext context, Action<SectorSessionConfig> unloadWorldScene)
        {
            if (context == null)
                return;

            if (context.CameraRig)
                UnityEngine.Object.Destroy(context.CameraRig.gameObject);
            if (context.AsteroidField)
                UnityEngine.Object.Destroy(context.AsteroidField.gameObject);
            if (context.World)
                UnityEngine.Object.Destroy(context.World.gameObject);
            if (context.Player)
                UnityEngine.Object.Destroy(context.Player.gameObject);
            if (context.Enemy)
                UnityEngine.Object.Destroy(context.Enemy.gameObject);

            unloadWorldScene?.Invoke(context.Config);
        }
    }
}
