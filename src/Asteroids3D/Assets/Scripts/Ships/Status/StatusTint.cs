using UnityEngine;

namespace Ships.Status
{
    /// <summary>The hull tint each effect holds while it lasts, and the colour of the word that announces it.</summary>
    public static class StatusTint
    {
        public static readonly Color Stunned = new(1f, .9f, .2f);
        public static readonly Color Jammed = new(.25f, .75f, 1f);
        public static readonly Color Suppressed = new(.55f, .58f, .62f);
        public static readonly Color Stalled = new(.7f, .35f, 1f);

        public static Color? Of(StatusEffects status)
        {
            if (status.Has(StatusEffect.Stunned)) return Stunned;
            if (status.Has(StatusEffect.Stalled)) return Stalled;
            if (status.Has(StatusEffect.WeaponsJammed)) return Jammed;
            if (status.Has(StatusEffect.Suppressed)) return Suppressed;
            return null;
        }
    }
}
