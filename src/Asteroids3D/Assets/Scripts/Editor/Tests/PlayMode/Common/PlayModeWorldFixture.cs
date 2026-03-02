using Game;
using NUnit.Framework;
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
    }

    /// <summary>
    /// Called after each test. Cleans up the arena, resets GamePlane, and unpauses audio.
    /// Override this method if you need additional cleanup, but remember to call base.TearDown().
    /// </summary>
    [TearDown]
    public virtual void TearDown()
    {
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
