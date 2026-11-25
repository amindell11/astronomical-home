using System.Collections;
using Substrate.Sectors;
using Substrate.Sectors.Elements;

namespace Tests.PlayMode.Preview
{
    public sealed class SuspendedSetupModule : SectorModule
    {
        public bool AllowFinish { get; set; }
        public bool Entered { get; private set; }
        public bool Finished { get; private set; }

        public override IEnumerator Setup(SectorBuildContext context)
        {
            Entered = true;
            while (!AllowFinish) yield return null;
            Finished = true;
        }
    }
}
