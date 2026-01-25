#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Combat.Weapons
{
    public partial class WeaponMissiles
    {
        private void OnDrawGizmos()
        {
            if (!firePoint || !Rounds) return;
            var ammoText = $"Ammo: {Rounds.AmmoCount}/{Rounds.MaxAmmo}";
            Handles.Label(firePoint.position + Vector3.up * 2f, $"Missiles\n{ammoText}");
        }
    }
}
#endif
