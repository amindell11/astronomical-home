using Ships.Registry;

namespace Substrate.Sectors.Elements.Elites
{
    public readonly struct EliteMoment
    {
        public int SectorIndex { get; }
        public string FactionId { get; }
        public ShipId Ship { get; }

        public EliteMoment(int sectorIndex, string factionId, ShipId ship)
        {
            SectorIndex = sectorIndex;
            FactionId = factionId;
            Ship = ship;
        }
    }
}
