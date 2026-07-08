namespace Ships
{
    /// <summary>
    /// The pending module selection a hangar edits before it is applied to a ship. Holds the chosen
    /// engine + shield; <see cref="Ship.Reequip"/> is what actually installs them. Kept separate from
    /// the ship so the hangar can stage a build without touching the live ship until the player
    /// commits (Launch).
    ///
    /// Weapons are a slot in the design too, but they are instantiated child prefabs rather than data
    /// modules, so they swap via a different (GameObject-lifecycle) path and join this selection in a
    /// follow-up.
    /// </summary>
    public class ShipLoadout
    {
        public EngineModule Engine;
        public ShieldModule Shield;

        public ShipLoadout(EngineModule engine, ShieldModule shield)
        {
            Engine = engine;
            Shield = shield;
        }
    }
}
