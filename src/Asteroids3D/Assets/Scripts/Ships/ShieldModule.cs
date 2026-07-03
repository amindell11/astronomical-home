using UnityEngine;

namespace Ships
{
    /// <summary>
    /// Shield stats for a ship. One of the composable modules a <see cref="Ship"/> carries. Owns every
    /// shield-related field (disjoint ownership: the resolver copies these verbatim into
    /// <see cref="ResolvedShipStats"/>).
    /// </summary>
    [CreateAssetMenu(fileName = "ShieldModule", menuName = "Ship/Modules/Shield")]
    public class ShieldModule : TunableModule
    {
        [Header("Shield")]
        public float maxShield = 50f;
        public float shieldRegenDelay = 4f;
        public float shieldRegenRate = 10f;
    }
}
