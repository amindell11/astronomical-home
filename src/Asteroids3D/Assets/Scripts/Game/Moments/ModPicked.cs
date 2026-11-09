using System;
using System.Collections.Generic;
using Loot;

namespace Game.Moments
{
    public sealed class ModPicked : IRunMoment
    {
        public const string PickSlot = "mod-pick";

        public LullMod Mod { get; }
        public string Slot => PickSlot;
        public IReadOnlyDictionary<string, string> Facts { get; }

        public ModPicked(LullMod mod)
        {
            if (!mod) throw new ArgumentNullException(nameof(mod));
            Mod = mod;
            Facts = new Dictionary<string, string> { ["mod"] = mod.DisplayName };
        }
    }
}
