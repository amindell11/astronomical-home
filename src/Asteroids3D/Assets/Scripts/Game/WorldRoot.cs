using UnityEngine;

namespace Game
{
    public class WorldRoot : MonoBehaviour
    {
        public WorldFollow Follow { get; private set; }
        private void Awake()
        {
            Follow = GetComponent<WorldFollow>();
            ServiceLocator.Register(this);
        }
    }
}
