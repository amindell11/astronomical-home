using Objectives;
using UnityEngine;

namespace Substrate.Sectors.Elements.Stations
{
    /// <summary>A mod station's body and its parts; serialized refs point a copy at the copy's own parts.</summary>
    public sealed class ModStationBody : MonoBehaviour
    {
        [Tooltip("The fly-in trigger, on or under the body.")]
        [SerializeField] private ExtractionZone zone;
        [Tooltip("The glow and the minimap marker, under the body; hidden when the station goes dark.")]
        [SerializeField] private GameObject lit;

        public ExtractionZone Zone => zone;

        public GameObject Lit => lit;
    }
}
