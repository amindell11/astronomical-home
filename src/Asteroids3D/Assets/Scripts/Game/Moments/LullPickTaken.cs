using System;
using System.Collections.Generic;
using Loot;

namespace Game.Moments
{
    public sealed class LullPickTaken : IRunMoment
    {
        public const string PickSlot = "lull-pick";

        public LullMod Mod { get; }
        public string Slot => PickSlot;
        public IReadOnlyDictionary<string, string> Facts { get; }

        public LullPickTaken(LullMod mod)
        {
            if (!mod) throw new ArgumentNullException(nameof(mod));
            Mod = mod;
            Facts = new Dictionary<string, string> { ["mod"] = mod.DisplayName };
        }
    }
}
