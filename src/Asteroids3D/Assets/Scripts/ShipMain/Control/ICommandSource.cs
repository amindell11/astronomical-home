namespace ShipMain.Control
{
    /// <summary>
    /// A component that can supply high-level control commands for a <see cref="Ship"/>.
    /// Multiple sources can co-exist (e.g. AI, player, BT) – the Ship chooses which one to honour.
    /// </summary>
    public interface ICommandSource
    {
        /// <summary>
        /// Initializes the command source with a reference to the ship it controls.
        /// Called once by the ship during its Awake phase.
        /// </summary>
        /// <param name="ship">The ship this source will be controlling.</param>
        void InitializeCommander(Ship ship);

        /// <summary>
        /// Attempt to obtain a control command for this frame.
        /// </summary>
        /// <param name="state">Current state of the ship (kinematics, weapons)</param>
        /// <param name="cmd">Output command structure (undefined if method returns false)</param>
        /// <returns>True if this source wishes to drive the ship this frame.</returns>
        bool TryGetCommand(State state, out Command cmd);

        /// <summary>
        /// Priority of this source.  Higher values override lower ones when multiple sources are active.
        /// </summary>
        int Priority { get; }
    }




} 