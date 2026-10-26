#if UNITY_EDITOR
using NUnit.Framework;
using RL.Probes;

namespace Tests.EditMode
{
    /// <summary>The matrix sidecar's arithmetic: a draw is half a win, and a weapon's rate against the others skips fights against itself.</summary>
    [Category("AI")]
    public class MatrixSummaryEditModeTests
    {
        [Test]
        public void DrawCountsHalfAndSelfIsExcludedFromAgainstOthers()
        {
            var rows = new[]
            {
                Row("Lasers-Lasers", "Lasers", "Lasers", MatrixProbe.SideA, simSeconds: 10f,
                    firedA: 4, hitsA: 2, damageA: 40f, firedB: 4, hitsB: 1, damageB: 20f),
                Row("Lasers-Missiles", "Lasers", "Missiles", MatrixProbe.Draw, simSeconds: 8f,
                    firedA: 2, hitsA: 1, damageA: 10f, firedB: 1, hitsB: 0, damageB: 0f),
                Row("Lasers-Missiles", "Lasers", "Missiles", MatrixProbe.SideB, simSeconds: 20f,
                    firedA: 3, hitsA: 1, damageA: 5f, firedB: 2, hitsB: 2, damageB: 30f),
            };

            var sidecar = MatrixProbe.Score(rows);

            Assert.AreEqual(MatrixProbe.SummarySchemaId, sidecar.schema);
            var mirror = sidecar.pairs[0];
            Assert.AreEqual(1f, mirror.aWinRate, 1e-5f);
            Assert.AreEqual(1, mirror.decidedFights);
            Assert.AreEqual(10f, mirror.meanTimeToKill, 1e-4f);

            var cross = sidecar.pairs[1];
            Assert.AreEqual(0.25f, cross.aWinRate, 1e-5f, "one draw and one B win: 0.5 of 2");
            Assert.AreEqual(1, cross.decidedFights);
            Assert.AreEqual(20f, cross.meanTimeToKill, 1e-4f, "the draw's time is not a time to kill");

            var lasers = sidecar.weapons[0];
            var missiles = sidecar.weapons[1];
            Assert.AreEqual("Lasers", lasers.weapon);
            Assert.AreEqual("Missiles", missiles.weapon);
            Assert.AreEqual(2, lasers.againstOthers, "the Lasers mirror fight is not against others");
            Assert.AreEqual(0.25f, lasers.winRateAgainstOthers, 1e-5f);
            Assert.AreEqual(2, missiles.againstOthers);
            Assert.AreEqual(0.75f, missiles.winRateAgainstOthers, 1e-5f, "Missiles took the draw's half and the win");

            // Lasers fired 4+4 on the mirror and 2+3 on the cross. Hits 2+1+1+1.
            Assert.AreEqual(13, lasers.fired);
            Assert.AreEqual(5, lasers.hits);
            Assert.AreEqual(5f / 13f, lasers.hitRate, 1e-5f);
            Assert.AreEqual(75f, lasers.damageDealt, 1e-4f);

            var table = MatrixProbe.MarkdownTable(sidecar);
            StringAssert.Contains("| Lasers | 1.00 (1) | 0.25 (2) | 0.25 |", table);
            StringAssert.Contains("| Missiles | 0.75 (2) | - | 0.75 |", table);
        }

        private static MatrixRow Row(string pair, string weaponA, string weaponB, string winner, float simSeconds,
            int firedA, int hitsA, float damageA, int firedB, int hitsB, float damageB) => new()
        {
            schema = MatrixRow.SchemaId,
            pair = pair,
            winner = winner,
            simSeconds = simSeconds,
            tallyA = new MarksmanshipTally { weapon = weaponA, fired = firedA, hits = hitsA, damageDealt = damageA },
            tallyB = new MarksmanshipTally { weapon = weaponB, fired = firedB, hits = hitsB, damageDealt = damageB },
        };
    }
}
#endif
