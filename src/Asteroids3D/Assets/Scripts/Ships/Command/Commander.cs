using UnityEngine;

namespace Ships.Command
{
    public abstract class Commander : MonoBehaviour, ICommandSource
    {
        protected Command cachedCommand = default;

        public abstract void InitializeCommander(Ships.Ship ship);

        public bool TryGetCommand(State state, out Command cmd)
        {
            cmd = cachedCommand;
            return true;
        }

        public int Priority => 100;
    }
}
