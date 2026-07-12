using Game;
using Game.Services;
using Movement.MPC.Field;
using NUnit.Framework;
using Tests.Common;
using UnityEngine;

namespace Tests.PlayMode.Common
{

/// <summary>
/// Shared PlayMode test fixture that ensures deterministic world state,
/// proper isolation between tests, and centralized cleanup of global/static state.
/// Inherit from this class to get automatic setup/teardown of:
/// - Test arena with reference plane
/// - GamePlane static state reset
/// - AudioListener pause/unpause
/// - TestSceneBuilder cleanup
/// </summary>
public abstract class PlayModeWorldFixture
{
    /// <summary>
    /// Whether to pause the AudioListener during tests (default: true).
    /// Override in derived classes if audio is needed for the test.
    /// </summary>
    protected virtual bool PauseAudio => true;

    private GameObject arenaHost;

    /// <summary>The per-test world-frame handle wired into AI ships (registry + NavField sibling).</summary>
    protected ArenaContext Arena { get; private set; }

    /// <summary>The NavField sibling backing <see cref="Arena"/>, for tests that drive it directly.</summary>
    protected NavFieldService NavField { get; private set; }

    /// <summary>
    /// Called before each test. Creates the test arena and pauses audio.
    /// Override this method if you need additional setup, but remember to call base.SetUp().
    /// </summary>
    [SetUp]
    public virtual void SetUp()
    {
        if (PauseAudio)
            AudioListener.pause = true;

        TestSceneBuilder.CreateTestArena();
        GamePlane.Configure(PlaneAxis.Y);

        arenaHost = new GameObject("[TestArena]");
        Arena = TestArena.On(arenaHost);
        NavField = Arena.NavField;
    }

    /// <summary>
    /// Called after each test. Cleans up the arena, resets GamePlane, and unpauses audio.
    /// Override this method if you need additional cleanup, but remember to call base.TearDown().
    /// </summary>
    [TearDown]
    public virtual void TearDown()
    {
        if (arenaHost) Object.DestroyImmediate(arenaHost);
        Arena = null;
        NavField = null;

        TestSceneBuilder.CleanupTestArena();

        if (PauseAudio)
            AudioListener.pause = false;
    }

    /// <summary>
    /// Destroys a GameObject immediately, handling null gracefully.
    /// Use this helper to clean up test objects in derived class TearDown methods.
    /// </summary>
    protected void DestroyTestObject(GameObject obj)
    {
        if (obj != null)
            Object.DestroyImmediate(obj);
    }

    /// <summary>
    /// Destroys a Component's GameObject immediately, handling null gracefully.
    /// Use this helper to clean up test components in derived class TearDown methods.
    /// </summary>
    protected void DestroyTestObject(Component comp)
    {
        if (comp != null && comp.gameObject != null)
            Object.DestroyImmediate(comp.gameObject);
    }
}

} // namespace Tests.PlayMode.Common
