using Loot;
using UnityEngine;

namespace Ships.Mods
{
    [CreateAssetMenu(fileName = "PassiveMod", menuName = "Ship/Passive Mod")]
    public sealed class PassiveMod : BehaviourMod
    {
        public override bool LandsOnShip => true;

        public override bool Fits(Ship ship) => ship;

        public override string Landing(Ship ship) => "Ship";

#if UNITY_EDITOR
        internal void Configure(string displayName, string effect)
        {
            SetDisplayName(displayName);
            SetEffect(effect);
        }
#endif
    }
}
