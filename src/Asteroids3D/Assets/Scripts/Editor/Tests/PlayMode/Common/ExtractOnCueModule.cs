using Substrate.Sectors;
using Substrate.Sectors.Elements;

namespace Tests.PlayMode.Common
{
    public sealed class ExtractOnCueModule : SectorModule
    {
        public void Extract() => RequestSectorEnd(SectorResult.Extracted());
    }
}
