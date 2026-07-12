using Game;
using UnityEngine;

namespace UI
{
    /// <summary>
    /// Simple billboard behaviour that keeps the GameObject aligned to the game plane.
    /// Attach to the root of a world-space canvas so it stays oriented with gameplay.
    /// </summary>
    public sealed class Billboard : MonoBehaviour
    {
        void LateUpdate()
        {
            transform.rotation = Quaternion.LookRotation(GamePlane.Normal, GamePlane.Forward);
        }
    }
}
