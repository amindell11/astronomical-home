#if UNITY_EDITOR
using Game.Encounters;

namespace Game.Sectors
{
    public partial class EncounterSequenceModule
    {
        /// <summary>Test/editor seam: set the encounter sequence without reflecting the serialized field.</summary>
        internal void SetEncounters(Encounter[] encounters) => this.encounters = encounters;
    }
}
#endif
