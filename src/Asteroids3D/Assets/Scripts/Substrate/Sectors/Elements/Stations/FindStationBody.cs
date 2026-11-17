using Objectives;
using Substrate.Sectors.Elements.Stations.Visual;
using UnityEngine;

namespace Substrate.Sectors.Elements.Stations
{
    /// <summary>A station's body and its parts; serialized refs point a copy at the copy's own parts.</summary>
    public sealed class FindStationBody : MonoBehaviour
    {
        [Tooltip("The fly-in trigger, on or under the body.")]
        [SerializeField] private ExtractionZone zone;
        [Tooltip("Holds the minimap marker, under the body; hidden when the station goes dark.")]
        [SerializeField] private GameObject lit;
        [Tooltip("The minimap marker under the lit part, tinted with its kind's find colour.")]
        [SerializeField] private Renderer marker;

        [Tooltip("The station's model, outside the lit parts; setup swaps it for the sector faction's dressed one.")]
        [SerializeField] private StationModel model;

        public ExtractionZone Zone => zone;

        public GameObject Lit => lit;

        public Renderer Marker => marker;

        public StationModel Model => model;

        internal void SwapModel(StationModel dressed)
        {
            var current = model.transform;
            var next = Instantiate(dressed, current.parent);
            next.transform.localPosition = current.localPosition;
            next.transform.localRotation = current.localRotation;
            next.transform.localScale = current.localScale;
            next.name = dressed.name;
            var old = model;
            model = next;
            old.gameObject.SetActive(false);
            // Detach: Destroy is deferred, so the body copy would include it.
            old.transform.SetParent(null, false);
            Destroy(old.gameObject);
        }
    }
}
