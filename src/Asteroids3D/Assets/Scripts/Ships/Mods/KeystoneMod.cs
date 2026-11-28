using System;
using Balance;
using Loot;
using Ships.Keystones;
using UnityEngine;

namespace Ships.Mods
{
    [CreateAssetMenu(fileName = "KeystoneMod", menuName = "Ship/Keystone Mod")]
    public sealed class KeystoneMod : BehaviourMod
    {
        [Tooltip("Keystone kit prefabs this mod lands on; offered only on a ship that carries one. Required.")]
        [Stat, SerializeField] private Keystone[] fits = Array.Empty<Keystone>();

        public override bool LandsOn(Keystone kitPrefab)
        {
            if (fits.Length == 0)
                throw new InvalidOperationException($"Keystone mod '{name}' lists no keystone kit to land on.");
            return kitPrefab && Array.IndexOf(fits, kitPrefab) >= 0;
        }

        public override bool Fits(Ship ship) => ship && LandsOn(ship.KeystonePrefab);

        public override string Landing(Ship ship) => ship.KeystonePrefab.DisplayName;

#if UNITY_EDITOR
        internal void Configure(string displayName, string effect, params Keystone[] fits)
        {
            SetDisplayName(displayName);
            SetEffect(effect);
            this.fits = fits;
        }
#endif
    }
}
