using UnityEngine;

namespace Ships.Mods
{
    public interface IRockMods
    {
        bool CountsForLieInWait(Collider rock);
        bool CountsForOrbit(Collider rock);
    }
}
