using System.Collections.Generic;

namespace Game.Moments
{
    public sealed class HangarOpened : IRunMoment
    {
        public const string TutorialSlot = "hangar.tutorial";

        private static readonly IReadOnlyDictionary<string, string> NoFacts = new Dictionary<string, string>();

        public string Slot => TutorialSlot;
        public IReadOnlyDictionary<string, string> Facts => NoFacts;
    }
}
