using UnityEngine;

namespace Game.Sectors
{
    /// <summary>A sector signal's identity: the component reference IS the wiring, so a consumer can never name a signal no publisher owns. Authored manually; never delete-and-recreate (fileID identity is load-bearing).</summary>
    [AddComponentMenu("")]
    public class SignalPort : MonoBehaviour
    {
        public enum PortKind
        {
            Level,
            Latch,
        }

        [SerializeField] private PortKind kind = PortKind.Latch;

        public PortKind Kind => kind;

#if UNITY_EDITOR
        internal void SetKind(PortKind kind) => this.kind = kind;
#endif
    }

    internal static class SignalPortGuards
    {
        public static bool ValidPortRef(Component owner, SignalPort port, string role, Sector sector)
        {
            if (!port)
            {
                Debug.LogError($"{owner.GetType().Name} on '{owner.name}' has an unassigned {role} port — inert.", owner);
                return false;
            }
            if (sector && !port.transform.IsChildOf(sector.transform))
            {
                Debug.LogError($"{owner.GetType().Name} on '{owner.name}' references {role} port '{port.name}' outside its own sector — inert.", owner);
                return false;
            }
            return true;
        }
    }
}
