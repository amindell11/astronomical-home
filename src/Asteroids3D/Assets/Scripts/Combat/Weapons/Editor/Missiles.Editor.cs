#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Combat.Weapons
{
    public partial class WeaponMissiles
    {
        void OnDrawGizmos()
        {
            if (firePoint == null || rounds == null) return;
            var ammoText = $"Ammo: {rounds.AmmoCount}/{rounds.MaxAmmo}";
            Handles.Label(firePoint.position + Vector3.up * 2f, $"Missiles\n{ammoText}");
        }
    }
}
#endif
