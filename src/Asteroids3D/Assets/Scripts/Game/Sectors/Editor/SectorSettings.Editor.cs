#if UNITY_EDITOR
namespace Game.Sectors
{
    public partial class SectorSettings
    {
        /// <summary>Test/editor seam: toggle scene loading so tests can build a sector without a scene load.</summary>
        internal void SetLoadScene(bool value) => loadScene = value;
    }
}
#endif
