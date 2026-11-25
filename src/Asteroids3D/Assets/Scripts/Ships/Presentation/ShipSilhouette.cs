using System;
using UnityEngine;

namespace Ships.Presentation
{
    /// <summary>
    /// Each chassis's flat top-down silhouette, baked from its hull by the editor menu
    /// Tools/UI/Bake recap silhouettes into Resources under the chassis name. The run recap's
    /// kill markers and each ship's minimap marker draw it.
    /// </summary>
    public static class ShipSilhouette
    {
        public const string Folder = "RunRecap/Ships";

        public static Texture2D Load(string chassis) => Resources.Load<Texture2D>(Folder + "/" + chassis);

        public static Texture2D Of(UnityEngine.Object ship)
        {
            var chassis = ship.name.Replace("(Clone)", string.Empty);
            var texture = Load(chassis);
            if (!texture) throw new InvalidOperationException($"No silhouette for '{chassis}'.");
            return texture;
        }
    }
}
