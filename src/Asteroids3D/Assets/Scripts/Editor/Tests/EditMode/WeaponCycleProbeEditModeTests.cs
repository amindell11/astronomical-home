using System.Collections.Generic;
using Balance;
using NUnit.Framework;
using Ships.Loadout;

namespace Tests.EditMode
{
    /// <summary>Splitting a shot log into a weapon cycle mode, on made-up shot logs so a retune never trips it.</summary>
    [Category("Weapons")]
    public class WeaponCycleProbeEditModeTests
    {
        private const float Tolerance = 0.0001f;

        private static List<(float time, float damage)> Bursts(int bursts, int shotsPerBurst, float interval,
            float recovery, float damage)
        {
            var shots = new List<(float, float)>();
            var t = 0f;
            for (var b = 0; b < bursts; b++)
            {
                for (var s = 0; s < shotsPerBurst; s++)
                {
                    shots.Add((t, damage));
                    t += interval;
                }
                t += recovery - interval;
            }
            return shots;
        }

        // The probe stops on the step of the shot that opens a burst, so no silence follows it.
        private static WeaponCycleMode Split(List<(float time, float damage)> shots, float silenceAfter = 0f)
        {
            var mode = WeaponCycleProbe.FromShots("made-up", shots, shots[shots.Count - 1].time + silenceAfter);
            Assert.IsTrue(mode.HasValue, "a shot log yields a mode");
            return mode.Value;
        }

        private static void AssertMode(WeaponCycleMode mode, float opening, float openingSeconds,
            float magazine, float dump, float recovery)
        {
            Assert.AreEqual(opening, mode.OpeningDamage, Tolerance, "opening damage");
            Assert.AreEqual(openingSeconds, mode.OpeningSeconds, Tolerance, "opening seconds");
            Assert.AreEqual(magazine, mode.MagazineDamage, Tolerance, "magazine");
            Assert.AreEqual(dump, mode.DumpSeconds, Tolerance, "dump");
            Assert.AreEqual(recovery, mode.RecoverySeconds, Tolerance, "recovery");
        }

        [Test]
        public void Mode_DerivesCycleSustainedDpsAndStakes()
        {
            var mode = new WeaponCycleMode("made-up", openingDamage: 90f, openingSeconds: 1f,
                magazineDamage: 60f, dumpSeconds: 2f, recoverySeconds: 4f);
            var ship = new ResolvedShipStats { maxHealth = 80f, maxShield = 40f };

            Assert.AreEqual(6f, mode.CycleSeconds, Tolerance);
            Assert.AreEqual(10f, mode.SustainedDps, Tolerance);
            Assert.AreEqual(120f, ship.ShipResourcePool, Tolerance);
            Assert.AreEqual(0.75f, mode.Stakes(ship.ShipResourcePool), Tolerance, "stakes reads the opening burst");
        }

        [Test]
        public void RepeatingBursts_SplitIntoMagazineDumpAndRecovery()
        {
            var mode = Split(Bursts(bursts: 5, shotsPerBurst: 4, interval: 0.25f, recovery: 2f, damage: 5f));

            AssertMode(mode, opening: 20f, openingSeconds: 0.75f, magazine: 20f, dump: 0.75f, recovery: 2f);
            Assert.AreEqual(20f / 2.75f, mode.SustainedDps, Tolerance);
        }

        [Test]
        public void OpeningBurst_ThatIsNotRepeated_IsKeptApartFromTheRepeatingOne()
        {
            // Three quick shots from cold, then one shot every second: a gunner holding back from overheat.
            var shots = new List<(float, float)> { (0f, 10f), (0.25f, 10f), (0.5f, 10f) };
            for (var t = 1.5f; t < 7f; t += 1f)
                shots.Add((t, 10f));

            AssertMode(Split(shots), opening: 30f, openingSeconds: 0.5f, magazine: 10f, dump: 0f, recovery: 1f);
        }

        [Test]
        public void UniformFire_IsOneShotBursts()
        {
            var mode = Split(Bursts(bursts: 8, shotsPerBurst: 1, interval: 0.5f, recovery: 0.5f, damage: 7f));

            AssertMode(mode, opening: 7f, openingSeconds: 0f, magazine: 7f, dump: 0f, recovery: 0.5f);
            Assert.AreEqual(14f, mode.SustainedDps, Tolerance);
        }

        [Test]
        public void SlightlyUnevenGaps_StayInOneBurst()
        {
            // A fire interval that lands on 5 or 6 fixed steps is still one burst.
            var shots = new List<(float, float)>();
            var t = 0f;
            for (var b = 0; b < 4; b++)
            {
                for (var s = 0; s < 4; s++)
                {
                    shots.Add((t, 1f));
                    t += s % 2 == 0 ? 0.1f : 0.12f;
                }
                t += 1.5f;
            }

            Assert.AreEqual(4f, Split(shots).MagazineDamage, Tolerance);
        }

        [Test]
        public void TwoBursts_RepeatTheFirst()
        {
            var mode = Split(Bursts(bursts: 2, shotsPerBurst: 3, interval: 1f, recovery: 10f, damage: 4f));

            AssertMode(mode, opening: 12f, openingSeconds: 2f, magazine: 12f, dump: 2f, recovery: 10f);
        }

        [Test]
        public void OneBurst_NeverRecoversAndSustainsNothing()
        {
            // Evenly spaced, then silent until the run's time cap: a magazine that never refills.
            var mode = Split(Bursts(bursts: 1, shotsPerBurst: 3, interval: 1f, recovery: 0f, damage: 40f),
                silenceAfter: 60f);

            Assert.AreEqual(120f, mode.OpeningDamage, Tolerance);
            Assert.IsTrue(float.IsPositiveInfinity(mode.RecoverySeconds));
            Assert.AreEqual(0f, mode.SustainedDps);
        }

        [Test]
        public void NoShots_YieldNoMode()
        {
            Assert.IsFalse(WeaponCycleProbe.FromShots("made-up", new List<(float, float)>(), 120f).HasValue);
        }
    }
}
