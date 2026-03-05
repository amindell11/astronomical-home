using Game.Sectors;

namespace Game.Bootstrap
{
    [System.Serializable]
    public class SectorEntry
    {
        public SectorConfigSO config;
        public SectorManager managerPrefab;
    }
}
