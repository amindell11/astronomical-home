using System;
using System.Collections.Generic;
using UnityEngine;

namespace UI.Hangar
{
    /// <summary>
    /// The hangar preview's hologram set. The stage swaps every copy material whose shader has a counterpart
    /// here; the counterpart's shader receives the ship material's value for each property it tags
    /// <c>[PerRendererData]</c>. The preview keeps driving <c>_DebrisVisibility</c> (swoop fade) and
    /// <c>_ContourPixels</c> (ink scale) per copy, so a counterpart shader that wants them declares those
    /// names. The projector cone is authored in framing-radius units.
    /// </summary>
    [CreateAssetMenu(menuName = "Astronomical/UI/Hangar hologram")]
    public sealed class HangarHologramProfile : ScriptableObject
    {
        [Serializable]
        public struct Counterpart
        {
            [Tooltip("The ship shader this counterpart replaces on preview copies.")]
            public Shader drawn;
            [Tooltip("The shared material preview copies wear instead. Its shader's [PerRendererData] properties take the ship material's values.")]
            public Material hologram;
        }

        [SerializeField] private Counterpart[] counterparts;
        [Tooltip("Opacity of the whole preview image.")]
        [SerializeField, Range(0f, 1f)] private float layerOpacity = .75f;
        [Tooltip("Placed at the stage anchor under the settled ship. One unit is the framing radius.")]
        [SerializeField] private GameObject projectorCone;

        public float LayerOpacity => layerOpacity;
        public GameObject ProjectorCone => projectorCone;
        public IReadOnlyList<Counterpart> Counterparts => counterparts;

        public Material CounterpartOf(Shader drawn)
        {
            foreach (var counterpart in counterparts)
                if (counterpart.drawn == drawn)
                    return counterpart.hologram;
            return null;
        }

        private void OnValidate()
        {
            for (var i = 0; i < counterparts.Length; i++)
            {
                if (!counterparts[i].drawn || !counterparts[i].hologram)
                    throw new InvalidOperationException(
                        $"Hangar hologram profile '{name}' counterpart {i} needs both a drawn shader and a hologram material.");
                for (var j = 0; j < i; j++)
                    if (counterparts[j].drawn == counterparts[i].drawn)
                        throw new InvalidOperationException(
                            $"Hangar hologram profile '{name}' pairs '{counterparts[i].drawn.name}' twice.");
            }
            if (!projectorCone)
                throw new InvalidOperationException($"Hangar hologram profile '{name}' needs a projector cone.");
        }
    }
}
