using System;
using Balance;
using UnityEngine;

namespace Loot
{
    [CreateAssetMenu(fileName = "LullModPool", menuName = "Loot/Lull Mod Pool")]
    public class LullModPool : ScriptableObject
    {
        [Tooltip("Mods a lull may offer; an offer shows only those that fit the player's ship and weapons.")]
        [Stat] public LullMod[] mods = Array.Empty<LullMod>();
    }
}
