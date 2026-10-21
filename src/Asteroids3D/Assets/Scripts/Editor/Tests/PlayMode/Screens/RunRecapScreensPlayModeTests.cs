#if UNITY_EDITOR
using System;
using System.Collections;
using Damage;
using Game.Player;
using Game.Runs;
using NUnit.Framework;
using Ships.Registry;
using UI.Screens;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Tests.PlayMode.Screens
{
    [Category("UI")]
    public class RunRecapScreensPlayModeTests
    {
        private sealed class FixedTally : IRunTally
        {
            public int Kills => 7;
            public float SecondsSurvived => 83.4f;
        }

        private GameObject root;

        [TearDown]
        public void TearDown()
        {
            if (root) Object.Destroy(root);
        }

        private static void Record(DamageLedger ledger, float time, DamageKind kind, int attacker,
            float shield, float hull)
        {
            var hit = new DamageInfo(shield + hull, kind, new ShipId(attacker), 0f, Vector3.zero, Vector3.zero);
            ledger.Record(new AppliedDamage(hit, shield, hull), time);
        }

        private static string TextAt(Component screen, string path) =>
            screen.transform.Find("Dim/Panel/" + path).GetComponent<Text>().text;

        [UnityTest]
        public IEnumerator DeathRecap_GroupsWholeRunHullDamageByWeapon_AndContinues()
        {
            root = new GameObject("RecapRoot");
            var screen = DeathRecapScreen.Create(root.transform);
            var ledger = new DamageLedger();
            Record(ledger, 1f, DamageKind.Laser, 7, 40f, 20f);
            Record(ledger, 100f, DamageKind.Laser, 9, 0f, 10f);
            Record(ledger, 101f, DamageKind.Missile, 7, 50f, 50f);
            Record(ledger, 102f, DamageKind.Collision, 0, 0f, 5f);
            Record(ledger, 103f, DamageKind.Railgun, 9, 100f, 0f);
            var board = new Scoreboard(Array.Empty<Scoreboard.Row>(), new Scoreboard.Row(1, 7, 83.4f, DateTime.UtcNow));
            var continued = false;

            screen.Show(ledger, new FixedTally(), 1, board, () => continued = true);
            yield return null;

            StringAssert.Contains("1 sector cleared", TextAt(screen, "Tally"));
            StringAssert.Contains("7 kills", TextAt(screen, "Tally"));
            StringAssert.Contains("1:23", TextAt(screen, "Tally"));
            StringAssert.Contains("190", TextAt(screen, "DamageTotals/Shield"));
            StringAssert.Contains("85", TextAt(screen, "DamageTotals/Hull"));
            Assert.AreEqual("30 pts", TextAt(screen, "HullBreakdown/Legend/Laser/Points"));
            Assert.AreEqual("50 pts", TextAt(screen, "HullBreakdown/Legend/Missile/Points"));
            Assert.AreEqual("5 pts", TextAt(screen, "HullBreakdown/Legend/Collision/Points"));
            Assert.IsNull(screen.transform.Find("Dim/Panel/HullBreakdown/Legend/Railgun"));
            var renderer = screen.transform.Find("Dim/Panel/HullBreakdown/Pie").GetComponent<CanvasRenderer>();
            Assert.IsNotNull(renderer, "The pie needs a renderer to appear in the recap.");
            Canvas.ForceUpdateCanvases();
            var mesh = renderer.GetMesh();
            Assert.Greater(mesh.vertexCount, 0, "The hull breakdown must draw visible slices.");
            Assert.AreEqual("Rank 1 of 1", TextAt(screen, "Footer/Rank"));
            screen.transform.Find("Dim/Panel/Footer/Continue").GetComponent<Button>().onClick.Invoke();
            Assert.IsTrue(continued);
        }

        [UnityTest]
        public IEnumerator Extraction_ShieldOnlyRunShowsNoHullDamageWithoutAPie()
        {
            root = new GameObject("WinRoot");
            var screen = RunWonScreen.Create(root.transform);
            var ledger = new DamageLedger();
            Record(ledger, 1f, DamageKind.Missile, 7, 150f, 0f);
            var continued = false;

            screen.Show(new FixedTally(), 3, ledger, new Scoreboard(Array.Empty<Scoreboard.Row>(), null),
                () => continued = true);
            yield return null;

            Assert.AreEqual("EXTRACTED", TextAt(screen, "Title"));
            StringAssert.Contains("3 sectors cleared", TextAt(screen, "Tally"));
            StringAssert.Contains("150", TextAt(screen, "DamageTotals/Shield"));
            Assert.AreEqual("No hull damage taken", TextAt(screen, "NoHullDamage"));
            Assert.IsNull(screen.transform.Find("Dim/Panel/HullBreakdown"));
            Assert.AreEqual("Not ranked", TextAt(screen, "Footer/Rank"));
            screen.transform.Find("Dim/Panel/Footer/Continue").GetComponent<Button>().onClick.Invoke();
            Assert.IsTrue(continued);
        }
    }
}
#endif
