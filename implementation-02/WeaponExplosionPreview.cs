#if UNITY_EDITOR
using System.Collections;
using Capture;
using Combat.Projectiles;
using Tests.PlayMode.Common;
using UnityEditor;
using UnityEngine;
using Utils;

public sealed class WeaponExplosionPreview : CaptureScenario
{
    public override GizmoCaptureProfile Profile => GizmoCaptureProfile.None;
    public override CaptureConfig Config => new()
    {
        clipName = "weapon-explosions",
        width = 1024,
        height = 576,
        everyFixedSteps = 1,
        minHalfHeight = 18f,
        padding = 2f
    };

    public override IEnumerator Run()
    {
        var (left, leftPilot) = SpawnCombatShip(new Vector2(-24f, 0f), 0f, 0);
        var (right, rightPilot) = SpawnCombatShip(new Vector2(24f, 0f), 0f, 0);
        leftPilot.enabled = false;
        rightPilot.enabled = false;
        left.Body.constraints = RigidbodyConstraints.FreezeAll;
        right.Body.constraints = RigidbodyConstraints.FreezeAll;
        var missile = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Visuals/Vfx/WeaponExplosions/Prefabs/MissileExplosion.prefab").GetComponent<PooledVFX>();
        var wavePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Weapons/Projectiles/ConcussionWave.prefab").GetComponent<ConcussionWave>();
        Film(left, right);
        for (var step = 0; step < 180; step++)
        {
            if (step == 10 || step == 90)
                SimplePool<PooledVFX>.Get(missile, Session.Frame.Place(new Vector2(-12f, 0f)), Quaternion.identity);
            if (step == 20 || step == 100)
            {
                var wave = SimplePool<ConcussionWave>.Get(wavePrefab, Session.Frame.Place(new Vector2(8f, 0f)), Quaternion.identity);
                Session.Projectiles.Register(wave, wave.ReturnToPoolImmediate);
                wave.Begin(left.Id);
            }
            yield return new WaitForFixedUpdate();
            FilmStep();
        }
    }
}
#endif
