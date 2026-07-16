using Game;
using Game.Services;
using Movement.MPC.Field;
using NUnit.Framework;
using Tests.Common;
using UnityEngine;

namespace Tests.PlayMode.Common
{

/// <summary>Per-test world fixture: arena (registry + NavField), projectile registry, audio pause, and TestSceneBuilder cleanup.</summary>
public abstract class PlayModeWorldFixture
{
    /// <summary>Override false if a test needs audio.</summary>
    protected virtual bool PauseAudio => true;

    private GameObject arenaHost;

    /// <summary>The per-test world-frame handle wired into AI ships (registry + NavField sibling).</summary>
    protected ArenaContext Arena { get; private set; }

    /// <summary>The NavField sibling backing <see cref="Arena"/>, for tests that drive it directly.</summary>
    protected NavFieldService NavField { get; private set; }

    /// <summary>Per-test projectile registry rooted at the arena host: pass it wherever firing needs a registry (ship spawns, direct <c>Fire</c>/<c>HandleTrigger</c> calls) and every transient dies with the fixture.</summary>
    protected ProjectileService Projectiles { get; private set; }

    /// <summary>Overrides must call base.SetUp().</summary>
    [SetUp]
    public virtual void SetUp()
    {
        if (PauseAudio)
            AudioListener.pause = true;

        TestSceneBuilder.CreateTestArena();

        arenaHost = new GameObject("[TestArena]");
        Arena = TestArena.On(arenaHost);
        NavField = Arena.NavField;
        Projectiles = new ProjectileService(arenaHost.transform);
    }

    /// <summary>Overrides must call base.TearDown().</summary>
    [TearDown]
    public virtual void TearDown()
    {
        if (arenaHost) Object.DestroyImmediate(arenaHost);
        Arena = null;
        NavField = null;
        Projectiles = null;

        TestSceneBuilder.CleanupTestArena();

        if (PauseAudio)
            AudioListener.pause = false;
    }

    /// <summary>Destroys a GameObject immediately, null-tolerant (for derived TearDowns).</summary>
    protected void DestroyTestObject(GameObject obj)
    {
        if (obj != null)
            Object.DestroyImmediate(obj);
    }

    /// <summary>Destroys a Component's GameObject immediately, null-tolerant (for derived TearDowns).</summary>
    protected void DestroyTestObject(Component comp)
    {
        if (comp != null && comp.gameObject != null)
            Object.DestroyImmediate(comp.gameObject);
    }
}

}
