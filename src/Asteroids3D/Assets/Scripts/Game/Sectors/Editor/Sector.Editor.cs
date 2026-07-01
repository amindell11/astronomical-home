#if UNITY_EDITOR
namespace Game.Sectors
{
    public partial class Sector
    {
        /// <summary>
        /// Test/editor seam: inject the baked manifest directly, mirroring what the inspector Sync
        /// writes via <see cref="SyncManifest"/>. Null arguments leave that slice untouched. Lets tests
        /// construct a sector's manifest without reflecting the private serialized arrays.
        /// </summary>
        internal void SetManifest(AdoptEntry[] adopted, SectorSpawner[] spawners, SectorModule[] modules)
        {
            if (adopted != null) this.adopted = adopted;
            if (spawners != null) this.spawners = spawners;
            if (modules != null) this.modules = modules;
        }
    }
}
#endif
