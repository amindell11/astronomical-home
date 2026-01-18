#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Weapons
{
    public partial class Missiles
    {
        void OnDrawGizmos()
        {
            if (firePoint == null || _rounds == null) return;
            var ammoText = $"Ammo: {_rounds.AmmoCount}/{_rounds.MaxAmmo}";
            Handles.Label(firePoint.position + Vector3.up * 2f, $"Missiles\n{ammoText}");
        }
    }
}
#endif
