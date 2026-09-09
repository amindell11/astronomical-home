namespace Game.Sectors.Elements
{
    using UnityEngine;

    /// <summary>
    /// Pure tag component. An empty child carrying this marks the player start pose for a sector;
    /// the sector surfaces it via <see cref="Sector.PlayerStart"/> and the session tier resets the
    /// injected player there on entry. The sector never resets the player itself.
    /// </summary>
    public class PlayerStartMarker : MonoBehaviour { }
}
