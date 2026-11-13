using Objectives;
using UnityEngine;

namespace Substrate.Sectors.Elements.Stations
{
    /// <summary>A station's body and its parts; serialized refs point a copy at the copy's own parts.</summary>
    public sealed class FindStationBody : MonoBehaviour
    {
        [Tooltip("The fly-in trigger, on or under the body.")]
        [SerializeField] private ExtractionZone zone;
        [Tooltip("The glow and the minimap marker, under the body; hidden when the station goes dark.")]
        [SerializeField] private GameObject lit;
        [Tooltip("The minimap marker under the lit part, tinted with its kind's find colour.")]
        [SerializeField] private Renderer marker;

        public ExtractionZone Zone => zone;

        public GameObject Lit => lit;

        public Renderer Marker => marker;
    }
}
