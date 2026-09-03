using System.Collections;

namespace Game.Session
{
    /// <summary>
    /// Session lifecycle primitives, kept separate from the driver that paces them: a driver sequences
    /// these over an explicit per-session <see cref="GameSession"/> without knowing the concrete host.
    /// Implemented by <see cref="SessionHost"/>; driven by <see cref="GameDriver"/>.
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
