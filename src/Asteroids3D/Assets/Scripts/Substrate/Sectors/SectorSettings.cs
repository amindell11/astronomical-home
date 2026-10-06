using UnityEngine;

namespace Substrate.Sectors
{
    [CreateAssetMenu(fileName = "SectorConfig", menuName = "Game/Sector Config")]
    public class SectorSettings : ScriptableObject
    {
        [SerializeField] private string displayName = "Unnamed Sector";
        [SerializeField] private int difficultySeed;

        [Header("Locale")]
        [Tooltip("Locale scene supplying this sector's sky layers / ambient / reflection / fog / audio. " +
                 "Required for any sector a player sees; only runtime-built headless configs leave it unassigned.")]
        [SerializeField] private SceneReference locale;

        public string DisplayName => displayName;
        public int DifficultySeed => difficultySeed;
        public SceneReference Locale => locale;
    }
}
