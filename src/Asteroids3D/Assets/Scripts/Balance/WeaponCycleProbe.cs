using System;
using System.Collections;
using System.Collections.Generic;
using Combat.Projectiles;
using Combat.Weapons;
using Damage;
using Substrate;
using Substrate.Services.Projectiles;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Balance
{
    /// <summary>
    /// Measures a weapon's cycle modes by firing a fresh, unmounted instance of it under each
    /// trigger pattern, one fixed step at a time, and splitting the shots into bursts. The game
    /// is the only model: no weapon or limiter states its cycle. Needs play mode (Awake,
    /// physics, pooling). Patterns that fire identically report once, the held trigger first.
    /// </summary>
    public static class WeaponCycleProbe
    {
        public const float DefaultMaxSeconds = 120f;

        private const int BurstsToObserve = 6;
        private const float TargetDistance = 30f;

        private enum Pattern { Hold, Tap, Ai }

        private static readonly string[] Labels = { "hold", "tap", "AI" };

        // One ahead, one behind: every shipped firing envelope holds at least one.
        private static readonly TargetingContext[] Contexts =
        {
            new() { distanceToTarget = 5f, angleToTarget = 0f, hasLineOfSight = true },
            new() { distanceToTarget = 5f, angleToTarget = 180f, hasLineOfSight = true },
        };

        private static readonly TargetingContext OutOfEnvelope = new() { distanceToTarget = float.PositiveInfinity };

        /// <summary>Appends the weapon's modes to <paramref name="modes"/>. Launched transients stay registered with <paramref name="projectiles"/>.</summary>
        public static IEnumerator Measure(WeaponComponent prefab, IProjectileService projectiles,
            List<WeaponCycleMode> modes, float maxSeconds = DefaultMaxSeconds)
        {
            if (!prefab) throw new ArgumentNullException(nameof(prefab));
            if (projectiles == null) throw new ArgumentNullException(nameof(projectiles));
            if (modes == null) throw new ArgumentNullException(nameof(modes));

            var savedTimeScale = Time.timeScale;
            var savedCaptureDelta = Time.captureDeltaTime;
            // One Update per fixed step, so Update-ticked limiters advance in step with the trigger.
            Time.timeScale = 1f;
            Time.captureDeltaTime = Time.fixedDeltaTime;
            try
            {
                foreach (Pattern pattern in Enum.GetValues(typeof(Pattern)))
                {
                    var run = new Run();
                    yield return Fire(prefab, pattern, projectiles, run, maxSeconds);
                    var mode = FromShots(Labels[(int)pattern], run.Shots, run.EndSeconds);
                    if (mode is { } found && !modes.Exists(m => SameCycle(m, found)))
                        modes.Add(found);
                }
            }
            finally
            {
                Time.timeScale = savedTimeScale;
                Time.captureDeltaTime = savedCaptureDelta;
            }
        }

        private sealed class Run
        {
            public readonly List<(float time, float damage)> Shots = new();
            public float EndSeconds;
        }

        private static IEnumerator Fire(WeaponComponent prefab, Pattern pattern, IProjectileService projectiles,
            Run run, float maxSeconds)
        {
            var weapon = Object.Instantiate(prefab);
            weapon.gameObject.SetActive(true);
            var target = HitRecorder.Create(weapon.firePoint.position + weapon.firePoint.up * TargetDistance);
            var launches = new LaunchRecorder(projectiles);
            var context = Array.Find(Contexts, c => weapon.InEnvelope(in c));

            try
            {
                var start = Time.fixedTime;
                var prevHeld = false;
                while (Time.fixedTime - start < maxSeconds && LongGaps(run.Shots) < BurstsToObserve - 1)
                {
                    yield return new WaitForFixedUpdate();

                    var held = pattern switch
                    {
                        Pattern.Hold => true,
                        Pattern.Tap => !weapon.CanFire(),
                        // Ready again under a spent press: the target steps out so its re-entry is a fresh edge.
                        _ => weapon.ShouldFire(prevHeld && !weapon.AutoFire && weapon.CanFire() ? OutOfEnvelope : context),
                    };
                    // The AI presses on the rising edge, as the Gunner does.
                    var pressed = pattern == Pattern.Ai ? held && !prevHeld : held;
                    prevHeld = held;
                    var hitBefore = target.Damage;
                    weapon.HandleTrigger(pressed, held, launches);
                    var damage = launches.TakeDamage() + target.Damage - hitBefore;
                    run.EndSeconds = Time.fixedTime - start;
                    if (damage > 0f)
                        run.Shots.Add((run.EndSeconds, damage));
                }
            }
            finally
            {
                Object.Destroy(weapon.gameObject);
                Object.Destroy(target.gameObject);
            }
        }

        /// <summary>
        /// Splits shots into bursts at gaps over 1.5x the shortest, the silence before
        /// <paramref name="endSeconds"/> counting as a gap. The repeating burst averages all but the
        /// cold opening and the unfinished last; with only two, the opening stands in. No shot
        /// after the first burst means it never recovers.
        /// </summary>
        internal static WeaponCycleMode? FromShots(string label, IReadOnlyList<(float time, float damage)> shots,
            float endSeconds)
        {
            if (shots.Count == 0) return null;

            var bursts = Split(shots, endSeconds - shots[shots.Count - 1].time);
            var opening = bursts[0];
            if (bursts.Count == 1)
                return new WeaponCycleMode(label, opening.Damage, opening.Span, opening.Damage, opening.Span,
                    float.PositiveInfinity);

            var first = bursts.Count == 2 ? 0 : 1;
            var last = bursts.Count - 2;
            float damage = 0f, dump = 0f, recovery = 0f;
            for (var i = first; i <= last; i++)
            {
                damage += bursts[i].Damage;
                dump += bursts[i].Span;
                recovery += bursts[i + 1].Start - bursts[i].End;
            }
            var count = last - first + 1;
            return new WeaponCycleMode(label, opening.Damage, opening.Span, damage / count, dump / count,
                recovery / count);
        }

        private struct Burst
        {
            public float Start, End, Damage;
            public float Span => End - Start;
        }

        private static List<Burst> Split(IReadOnlyList<(float time, float damage)> shots, float trailingSilence)
        {
            var threshold = BurstGapThreshold(shots, trailingSilence);
            var bursts = new List<Burst>();
            var current = new Burst { Start = shots[0].time, End = shots[0].time, Damage = shots[0].damage };
            for (var i = 1; i < shots.Count; i++)
            {
                if (shots[i].time - shots[i - 1].time > threshold)
                {
                    bursts.Add(current);
                    current = new Burst { Start = shots[i].time, End = shots[i].time };
                }
                current.End = shots[i].time;
                current.Damage += shots[i].damage;
            }
            bursts.Add(current);
            return bursts;
        }

        // Uniform fire has no gap over the threshold, so every shot is then its own burst.
        private static float BurstGapThreshold(IReadOnlyList<(float time, float damage)> shots, float trailingSilence)
        {
            var shortest = float.PositiveInfinity;
            var longest = trailingSilence;
            for (var i = 1; i < shots.Count; i++)
            {
                var gap = shots[i].time - shots[i - 1].time;
                shortest = Mathf.Min(shortest, gap);
                longest = Mathf.Max(longest, gap);
            }
            var threshold = shortest * 1.5f + 0.001f;
            return longest > threshold ? threshold : 0f;
        }

        private static int LongGaps(List<(float time, float damage)> shots)
        {
            if (shots.Count < 2) return 0;
            var threshold = BurstGapThreshold(shots, 0f);
            if (threshold <= 0f) return 0;
            var count = 0;
            for (var i = 1; i < shots.Count; i++)
                if (shots[i].time - shots[i - 1].time > threshold) count++;
            return count;
        }

        // A step is 1/50 s; two modes within one step and 1% of damage fire the same way.
        private static bool SameCycle(WeaponCycleMode a, WeaponCycleMode b) =>
            Mathf.Abs(a.OpeningDamage - b.OpeningDamage) <= 0.01f * a.OpeningDamage
            && Mathf.Abs(a.MagazineDamage - b.MagazineDamage) <= 0.01f * a.MagazineDamage
            && Mathf.Abs(a.CycleSeconds - b.CycleSeconds) <= 0.021f;

        /// <summary>Counts damage at launch; a grenade's is its blast at the centre. Unmounted, no launch can hit anything.</summary>
        private sealed class LaunchRecorder : IProjectileService
        {
            private readonly IProjectileService inner;
            private readonly List<ProjectileBase> launched = new();

            public LaunchRecorder(IProjectileService inner) => this.inner = inner;

            public int ActiveCount => inner.ActiveCount;

            public void Register(MonoBehaviour instance, Action returnToPool)
            {
                inner.Register(instance, returnToPool);
                var projectile = instance as ProjectileBase;
                if (projectile)
                    launched.Add(projectile);
            }

            public void ReturnAllToPool() => inner.ReturnAllToPool();

            public void ForEachLive(Action<MonoBehaviour> visit) => inner.ForEachLive(visit);

            // Read after the trigger step: a charge weapon scales its shot once registered.
            public float TakeDamage()
            {
                var damage = 0f;
                foreach (var projectile in launched)
                {
                    var grenade = projectile as Grenade;
                    damage += grenade ? grenade.WavePrefab.MaxDamage : projectile.Damage * projectile.DamageScale;
                }
                launched.Clear();
                return damage;
            }
        }

        /// <summary>Stands in the line of fire for hitscan weapons, which launch nothing.</summary>
        private sealed class HitRecorder : MonoBehaviour, IDamageable
        {
            public float Damage { get; private set; }

            public void TakeDamage(in DamageInfo hit) => Damage += hit.Amount;

            public static HitRecorder Create(Vector3 position)
            {
                var go = new GameObject("CycleProbeTarget") { layer = LayerIds.Ship };
                go.transform.position = position;
                go.AddComponent<BoxCollider>().size = Vector3.one * 2f;
                return go.AddComponent<HitRecorder>();
            }
        }
    }
}
