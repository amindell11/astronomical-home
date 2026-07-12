using System.Collections;

namespace Game.Bootstrap
{
    /// <summary>
    /// Driver-agnostic session lifecycle primitives. The seam contract: any driver (interactive game
    /// or headless/RL) drives these over an explicit per-session <see cref="GameSession"/> without
    /// knowing the concrete host. Implemented by <see cref="SessionHost"/>.
    /// </summary>
    public interface ISessionPrimitives
    {
        IEnumerator ComposeSession(GameSession session);
        IEnumerator LoadSector(GameSession session);
        IEnumerator UnloadSector(GameSession session);
        IEnumerator TeardownSession(GameSession session);
        void ApplyLoadout(GameSession session);
    }
}
