using Game;
using UnityEngine;

namespace World
{
    public class WorldRoot : MonoBehaviour
    {
        public WorldFollow Follower { get; private set; }

        private void Awake()
        {
            Follower = GetComponent<WorldFollow>();
        }

        private void LateUpdate()
        {
            transform.rotation = Quaternion.LookRotation(GamePlane.Normal, GamePlane.Forward);
        }
    }
}