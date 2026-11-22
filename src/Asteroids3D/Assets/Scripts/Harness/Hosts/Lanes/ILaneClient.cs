using System.Collections;
using Harness.Hosts;

namespace Harness.Hosts.Lanes
{
    /// <summary>A lane client: given the composed host and its spec, sequences the host's primitives (NewMatrixComposition / RunBlock) into one lane's protocol. Selected by <see cref="HarnessLane"/> in <see cref="HarnessHost"/>.</summary>
    internal interface ILaneClient
    {
        IEnumerator Run(HarnessHost host, HarnessSpec spec);
    }
}
