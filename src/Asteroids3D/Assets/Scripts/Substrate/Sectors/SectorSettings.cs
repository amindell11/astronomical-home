using Substrate.Services.Locales;
using UnityEngine;

namespace Substrate.Sectors
{
    [CreateAssetMenu(fileName = "SectorConfig", menuName = "Game/Sector Config")]
    public class SectorSettings : ScriptableObject
    {
        [SerializeField] private string displayName = "Unnamed Sector";
        [SerializeField] private int difficultySeed;

        [Header("Locale")]
        [Tooltip("Locale prefab supplying this sector's sky layers / lighting / audio. " +
                 "Required for any sector a player sees; only runtime-built headless configs leave it unassigned.")]
        [SerializeField] private LocaleSky locale;

        public string DisplayName => displayName;
        public int DifficultySeed => difficultySeed;
        public LocaleSky Locale => locale;
    }
}
