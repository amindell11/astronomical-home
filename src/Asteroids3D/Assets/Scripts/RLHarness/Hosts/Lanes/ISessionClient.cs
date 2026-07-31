using System.Collections;

namespace Game.RLHarness
{
    /// <summary>A lane client: given the composed host and its spec, sequences the host's primitives (NewComposition / RunBlock) into one lane's protocol. Selected by <see cref="SessionLane"/> in <see cref="HarnessSessionHost"/>.</summary>
    internal interface ISessionClient
    {
        IEnumerator Run(HarnessSessionHost host, SessionSpec spec);
    }
}
