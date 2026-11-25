using UnityEngine;

namespace FMODUnity
{
    public class StudioListener : MonoBehaviour
    {
        [SerializeField] private bool nonRigidbodyVelocity;
        [SerializeField] private GameObject attenuationObject;

        public GameObject AttenuationObject { get => attenuationObject; set => attenuationObject = value; }

        public int ListenerNumber => default;
    }
}
