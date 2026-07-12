using UnityEngine;

namespace Game.Sectors
{
    [CreateAssetMenu(fileName = "SectorConfig", menuName = "Game/Sector Config")]
    public class SectorSettings : ScriptableObject
    {
        [SerializeField] private string displayName = "Unnamed Sector";
        [SerializeField] private int difficultySeed;

        [Header("Environment")]
        [Tooltip("Locale scene supplying this sector's skybox / ambient / reflection / fog / audio. " +
                 "Unassigned → inherit boot-scene lighting (also the headless path).")]
        [SerializeField] private SceneReference locale;

        public string DisplayName => displayName;
        public int DifficultySeed => difficultySeed;
        public SceneReference Locale => locale;
    }
}
