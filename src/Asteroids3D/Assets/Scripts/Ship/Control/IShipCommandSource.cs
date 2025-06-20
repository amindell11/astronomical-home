using ShipControl;

namespace ShipControl
{
    /// <summary>
    /// A component that can supply high-level control commands for a <see cref="Ship"/>.
    /// Multiple sources can co-exist (e.g. AI, player, BT) – the Ship chooses which one to honour.
    /// </summary>
    public interface IShipCommandSource
    {
        /// <summary>
        /// Attempt to obtain a control command for this frame.
        /// </summary>
        /// <param name="cmd">Output command structure (undefined if method returns false)</param>
        /// <returns>True if this source wishes to drive the ship this frame.</returns>
        bool TryGetCommand(out ShipCommand cmd);

        /// <summary>
        /// Priority of this source.  Higher values override lower ones when multiple sources are active.
        /// </summary>
        int Priority { get; }
    }
} 