using Substrate.Sessions;

namespace Substrate.Sectors
{
    /// <summary>
    /// A sector part that reads values from its sector's run row at setup. The session checks every row
    /// against its layout's readers when it is constructed, so a row missing a value fails at session start.
    /// </summary>
    public interface ISectorRowReader
    {
        /// <summary>Throws, naming the value, when <paramref name="row"/> lacks one this part reads.</summary>
        void CheckRow(SectorEntry row);
    }
}
