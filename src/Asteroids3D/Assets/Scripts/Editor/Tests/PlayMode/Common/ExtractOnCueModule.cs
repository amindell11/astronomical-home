using Substrate.Sectors;
using Substrate.Sectors.Elements;

namespace Tests.PlayMode.Common
{
    public sealed class ExtractOnCueModule : SectorModule
    {
        public void Extract() => RequestSectorEnd(SectorResult.Extracted(0f));
        public void Fail() => RequestSectorEnd(SectorResult.Failed("cue"));
    }
}
