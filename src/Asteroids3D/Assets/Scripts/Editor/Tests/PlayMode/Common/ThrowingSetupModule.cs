using System;
using System.Collections;
using Substrate.Sectors;
using Substrate.Sectors.Elements;

namespace Tests.PlayMode.Common
{
    public sealed class ThrowingSetupModule : SectorModule
    {
        public static readonly Exception Fault = new InvalidOperationException("Sector setup failed on cue.");

        public bool Entered { get; private set; }
        public bool AllowThrow { get; set; }

        public override IEnumerator Setup(SectorBuildContext context)
        {
            Entered = true;
            while (!AllowThrow) yield return null;
            throw Fault;
        }
    }
}
