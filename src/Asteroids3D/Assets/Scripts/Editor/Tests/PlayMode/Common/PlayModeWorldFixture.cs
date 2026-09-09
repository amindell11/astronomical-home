using AI.Scanning;
using Game.Services;
using NUnit.Framework;
using Tests.Common;
using UnityEngine;

namespace Tests.PlayMode.Common
{

/// <summary>Per-test world fixture: a swappable obstacle field, projectile registry, audio pause, and TestSceneBuilder cleanup.</summary>
public abstract class PlayModeWorldFixture
{
    /// <summary>Override false if a test needs audio.</summary>
    protected virtual bool PauseAudio => true;

    /// <summary>Override true for accelerated simulation; test time budgets must be simulated-time, not wall-clock.</summary>
    protected virtual bool AccelerateTime => false;

    private GameObject arenaHost;
    private float savedTimeScale;
    private float savedMaxDelta;

    /// <summary>The obstacle field wired into AI ships; defaults to <see cref="ObstacleField"/>, and a test may point it elsewhere before wiring.</summary>
    protected IObstacleField Field { get; set; }

    /// <summary>The default obstacle source; set <c>Inner</c> to swap what the ships see mid-test.</summary>
    protected SwappableField ObstacleField { get; private set; }

    /// <summary>Per-test projectile registry rooted at the arena host: pass it wherever firing needs a registry (ship spawns, direct <c>Fire</c>/<c>HandleTrigger</c> calls) and every transient dies with the fixture.</summary>
    protected ProjectileService Projectiles { get; private set; }

    /// <summary>Overrides must call base.SetUp().</summary>
    [SetUp]
    public virtual void SetUp()
    {
        if (PauseAudio)
            AudioListener.pause = true;

        if (AccelerateTime)
        {
            savedTimeScale = Time.timeScale;
            savedMaxDelta = Time.maximumDeltaTime;
            Time.timeScale = 20f;
            Time.maximumDeltaTime = 1f;
        }

        TestSceneBuilder.CreateTestArena();

        arenaHost = new GameObject("[TestArena]");
        ObstacleField = new SwappableField();
        Field = ObstacleField;
        Projectiles = new ProjectileService(arenaHost.transform);
    }

    /// <summary>Overrides must call base.TearDown().</summary>
    [TearDown]
    public virtual void TearDown()
    {
        if (arenaHost) Object.DestroyImmediate(arenaHost);
        Field = null;
        ObstacleField = null;
        Projectiles = null;

        TestSceneBuilder.CleanupTestArena();

        if (AccelerateTime)
        {
            Time.timeScale = savedTimeScale;
            Time.maximumDeltaTime = savedMaxDelta;
        }

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
