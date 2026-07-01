namespace Ships.Presentation
{
    /// <summary>
    /// Implemented by visual-rig components (hull, thrusters, engine/damage audio, indicators) so the
    /// rig can inject the ship's <see cref="ShipView"/> when it is attached. Replaces per-component
    /// GetComponentInParent discovery with explicit injection.
    /// </summary>
    public interface IShipVisual
    {
        /// <summary>Wire this visual to the ship it presents. Called once, at rig attach time.</summary>
        void Bind(in ShipView view);
    }
}
