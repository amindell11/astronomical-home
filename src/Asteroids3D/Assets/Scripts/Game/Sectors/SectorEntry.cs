using Game.Sectors;

namespace Game.Bootstrap
{
    [System.Serializable]
    public class SectorEntry
    {
        public SectorSettings config;
        public Sector prefab;
    }
}
