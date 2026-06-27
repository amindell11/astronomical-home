using UnityEngine;

namespace Game.Sectors
{
    [CreateAssetMenu(fileName = "SectorConfig", menuName = "Game/Sector Config")]
    public class SectorSettings : ScriptableObject
    {
        [SerializeField] private string displayName = "Unnamed Sector";
        [SerializeField] private string sceneName = "BasicWorld";
        [SerializeField] private bool loadScene = true;
        [SerializeField] private int difficultySeed;

        public string DisplayName => displayName;
        public string SceneName => sceneName;
        public bool LoadScene => loadScene;
        public int DifficultySeed => difficultySeed;
    }
}
