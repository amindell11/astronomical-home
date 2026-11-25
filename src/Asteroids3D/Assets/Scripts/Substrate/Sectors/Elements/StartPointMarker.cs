using UnityEngine;

namespace Substrate.Sectors.Elements
{
    /// <summary>
    /// Pure tag component. An empty child carrying this marks where a sector begins; the manifest
    /// sync bakes it onto the sector, which surfaces it via <see cref="Sector.StartPoint"/>.
    /// </summary>
    public class StartPointMarker : MonoBehaviour { }
}
