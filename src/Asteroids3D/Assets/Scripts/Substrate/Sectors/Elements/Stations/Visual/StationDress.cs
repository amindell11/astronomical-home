using System;
using UnityEngine;

namespace Substrate.Sectors.Elements.Stations.Visual
{
    /// <summary>
    /// How a faction dresses the find stations in its sectors: a dressed model per station kind, baked
    /// with the faction's parts and hatching, and palettes each placed station rolls by its position.
    /// The design record: https://github.com/amindell11/astronomical-home/issues/1592
    /// </summary>
    [Serializable]
    public struct StationDress
    {
        private static readonly Vector3 RollKey = new(61.433f, 27.947f, 91.817f);

        [Tooltip("The palettes the faction's stations roll between, each by where it is placed.")]
        public StationPalette[] palettes;

        [Tooltip("The faction's dressed repair dock.")]
        public StationModel repair;

        [Tooltip("The faction's dressed mod station.")]
        public StationModel mod;

        [Tooltip("The faction's dressed armory.")]
        public StationModel armory;

        public StationModel ModelFor(StationKind kind) => kind switch
        {
            StationKind.Repair => repair,
            StationKind.Mod => mod,
            StationKind.Armory => armory,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "A station kind with no dressed model.")
        };

        public StationPalette PaletteAt(Vector3 position)
        {
            var index = (int)(PlacedHash.Unit(position, RollKey) * palettes.Length);
            if (index >= palettes.Length)
                index = palettes.Length - 1;
            return palettes[index];
        }
    }

    [Serializable]
    public struct StationPalette
    {
        [Tooltip("Name of this palette, e.g. \"Red hull\".")]
        public string name;

        [Tooltip("Main plates.")]
        public Color bone;

        [Tooltip("Second plates.")]
        public Color canvas;

        [Tooltip("Frame.")]
        public Color slate;

        [Tooltip("First accent.")]
        public Color ochre;

        [Tooltip("Second accent.")]
        public Color brick;

        [Tooltip("Third accent.")]
        public Color blue;

        [Tooltip("The crew windows' emission, calibrated so the game's grading and bloom land it on the palette's window colour.")]
        [ColorUsage(false, true)]
        public Color windowLight;

        [Tooltip("How deep the baked brush strokes shade and light the hull.")]
        [Range(0f, 2f)]
        public float brush;
    }
}
