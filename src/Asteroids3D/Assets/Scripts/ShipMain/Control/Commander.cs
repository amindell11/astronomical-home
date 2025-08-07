using UnityEngine;

namespace ShipMain.Control
{
    public abstract class Commander : MonoBehaviour, ICommandSource
    {
        protected Command CachedCommand = default;
        public abstract void InitializeCommander(Ship ship);

        public bool TryGetCommand(State state, out Command cmd)
        {
            cmd = CachedCommand;
            return true;
        }

        public int Priority => 100;
    }
}