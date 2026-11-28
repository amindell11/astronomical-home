using Combat.Weapons;
using Ships.Keystones;
using UnityEngine;

namespace Loot
{
    /// <summary>
    /// A lull mod that changes how a part behaves rather than one of its numbers. The mod is only a
    /// key: the weapon, keystone kit or ship component that owns the behaviour holds a reference to
    /// it, asks its <see cref="HeldBehaviours"/> whether the key is held, and keeps the tunables
    /// itself. Each kind names the parts it lands on; the bound ship hands every part the held keys
    /// that land on it whenever the run's stack changes. The arc: https://github.com/amindell11/astronomical-home/issues/1771
    /// </summary>
    public abstract class BehaviourMod : LullMod
    {
        [Tooltip("The card's effect line: what the mod changes, in a few words.")]
        [SerializeField] private string effect;

        public override string Effect => effect;

        public virtual bool LandsOn(WeaponComponent weaponPrefab) => false;

        public virtual bool LandsOn(Keystone kitPrefab) => false;

        public virtual bool LandsOnShip => false;

#if UNITY_EDITOR
        protected void SetEffect(string value) => effect = value;
#endif
    }
}
