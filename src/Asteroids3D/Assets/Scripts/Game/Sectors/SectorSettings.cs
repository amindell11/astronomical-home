using UnityEngine;

namespace Game.Sectors
{
    // Per-entry / per-session overridable sector config (referenced by SectorEntry, passed into
    // Sector.Initialize). Scene identity (sceneName/loadScene) is intentionally NOT here — that is
    // sector-type intrinsic and lives on the Sector template.
    [CreateAssetMenu(fileName = "SectorConfig", menuName = "Game/Sector Config")]
    public class SectorSettings : ScriptableObject
    {
        [SerializeField] private string displayName = "Unnamed Sector";
        [SerializeField] private int difficultySeed;

        public string DisplayName => displayName;
        public int DifficultySeed => difficultySeed;
    }
}
