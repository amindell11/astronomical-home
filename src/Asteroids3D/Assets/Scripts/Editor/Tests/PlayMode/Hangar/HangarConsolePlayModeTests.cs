#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using Ships;
using Ships.Loadout;
using UI.Hangar.Displays;
using UI.Hangar.Hardware;
using UI.Hangar.Selection;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.PlayMode.Hangar
{
    [Category("UI")]
    [Category("RequiresGraphics")]
    public sealed class HangarConsolePlayModeTests
    {
        private const string ConsolePath = "Assets/Prefabs/UI/HangarConsole.prefab";
        private const string VanguardPath = "Assets/Prefabs/Ships/Vanguard.prefab";
        private const string ValisPath = "Assets/Prefabs/Ships/Valis.prefab";
        private const float SettleTimeout = 2f;

        private HangarConsole console;
        private ItemSubset offer;
        private Quaternion knobRest;

        [TearDown]
        public void TearDown()
        {
            if (console) Object.Destroy(console.gameObject);
            if (offer) Object.Destroy(offer);
        }

        [UnityTest]
        public IEnumerator Show_FillsBothDisplays_AndTheKnobEasesToTheSelectedSlotsDetent()
        {
            var ship = Open(_ => true, out _);
            yield return null;

            var main = console.GetComponentInChildren<HangarMainDisplayView>();
            Assert.AreEqual(ship.name, main.Title);
            Assert.IsFalse(main.Locked);
            Assert.IsTrue(main.Bracketed(HangarSlot.Ship), "the ship slot opens bracketed");
            var detail = console.GetComponentInChildren<HangarDetailDisplayView>();
            Assert.AreEqual(ship.name, detail.Title);
            Assert.AreEqual(HangarDescriptions.Describe(ship), detail.Body);
            AssertDetent(HangarSlot.Ship);

            console.Selection.Select(HangarSlot.Weapon);
            Assert.IsFalse(console.KnobSettled, "a new slot sends the knob on its way");
            yield return Settled(() => console.KnobSettled);
            AssertDetent(HangarSlot.Weapon);
            Assert.IsTrue(main.Bracketed(HangarSlot.Weapon));
            Assert.IsFalse(main.Bracketed(HangarSlot.Ship));

            console.Selection.Select(HangarSlot.Ordnance);
            yield return Settled(() => console.KnobSettled);
            AssertDetent(HangarSlot.Ordnance);
        }

        [UnityTest]
        public IEnumerator Bars_FillAgainstTheOffersBest_WithTheFloor_AndShowTheExactValue()
        {
            var vanguard = Open(_ => true, out var valis);
            var main = console.GetComponentInChildren<HangarMainDisplayView>();
            var ships = new[] { vanguard, valis };
            yield return Settled(() => BarsSettled(main));

            for (var i = 0; i < HangarStats.Count; i++)
            {
                var stat = (HangarStat)i;
                var value = HangarStats.Value(vanguard, stat);
                var best = HangarStats.Best(ships, stat);
                var bar = main.Bar(stat);
                Assert.That(bar.Fill, Is.EqualTo(HangarStats.Floor + (1f - HangarStats.Floor) * value / best).Within(1e-4f), stat.ToString());
                Assert.That(bar.Fill, Is.GreaterThanOrEqualTo(HangarStats.Floor), stat.ToString());
                StringAssert.StartsWith(value.ToString("0.#"), bar.Number, stat.ToString());
            }

            console.Selection.Step(1);
            Assert.AreSame(valis, console.Selection.Ship, "test premise: the offer has a second ship");
            var hull = main.Bar(HangarStat.Hull);
            var movedBeforeSettling = false;
            var start = Time.unscaledTime;
            while (!BarsSettled(main) && Time.unscaledTime - start < SettleTimeout)
            {
                if (!Mathf.Approximately(hull.Fill, hull.Target)) movedBeforeSettling = true;
                yield return null;
            }
            Assert.IsTrue(BarsSettled(main), "bars settle after a ship change");
            Assert.IsTrue(movedBeforeSettling || HangarStats.Value(vanguard, HangarStat.Hull) == HangarStats.Value(valis, HangarStat.Hull),
                "a changed hull eases to its new fill rather than snapping");
            Assert.That(hull.Fill, Is.EqualTo(HangarStats.Fill(HangarStats.Value(valis, HangarStat.Hull), HangarStats.Best(ships, HangarStat.Hull))).Within(1e-4f));
        }

        [UnityTest]
        public IEnumerator PressedControls_ReturnToRest_AndLaunchReadsUnavailableWhileTheShipIsLocked()
        {
            Open(ship => ship.name == "Vanguard", out var valis);
            yield return null;
            Assert.IsTrue(console.LaunchLegendLit, "the starter can launch, so the legend is lit");

            var next = console.NextButton.GetComponent<HangarControl>();
            var rest = console.NextButton.localPosition;
            next.Press();
            Assert.AreSame(valis, console.Selection.Ship, "the press steps the ship");
            Assert.IsTrue(console.Selection.ShipLocked, "test premise: the second ship is locked");
            Assert.IsFalse(console.LaunchLegendLit, "a locked ship dims the launch legend");
            yield return Settled(() => Vector3.Distance(console.NextButton.localPosition, rest) > next.Travel * .5f);
            yield return Settled(() => !next.Moving);
            Assert.That(Vector3.Distance(console.NextButton.localPosition, rest), Is.LessThan(1e-4f), "the button is back at rest");

            var main = console.GetComponentInChildren<HangarMainDisplayView>();
            Assert.IsTrue(main.Locked, "the display shows the ship as locked");
            console.PreviousButton.GetComponent<HangarControl>().Press();
            Assert.IsTrue(console.LaunchLegendLit, "back on the starter the legend lights again");
        }

        private Ship Open(System.Func<Ship, bool> earned, out Ship second)
        {
            var vanguard = AssetDatabase.LoadAssetAtPath<Ship>(VanguardPath);
            second = AssetDatabase.LoadAssetAtPath<Ship>(ValisPath);
            offer = ScriptableObject.CreateInstance<ItemSubset>();
            offer.starter = vanguard;
            offer.ships = new[] { vanguard, second };
            console = Object.Instantiate(AssetDatabase.LoadAssetAtPath<HangarConsole>(ConsolePath));
            knobRest = console.SelectorKnob.localRotation;
            console.Show(offer, new ShipLoadout(vanguard, null, null), earned, null);
            return vanguard;
        }

        private static bool BarsSettled(HangarMainDisplayView main)
        {
            for (var i = 0; i < HangarStats.Count; i++)
            {
                var bar = main.Bar((HangarStat)i);
                if (!Mathf.Approximately(bar.Fill, bar.Target)) return false;
            }
            return true;
        }

        private static IEnumerator Settled(System.Func<bool> done)
        {
            var start = Time.unscaledTime;
            while (!done() && Time.unscaledTime - start < SettleTimeout) yield return null;
            Assert.IsTrue(done(), $"settled within {SettleTimeout} s");
        }

        private void AssertDetent(HangarSlot slot)
        {
            var expected = knobRest * Quaternion.AngleAxis(((int)slot - 1) * console.KnobDetentDegrees, console.KnobAxis);
            Assert.That(Quaternion.Angle(console.SelectorKnob.localRotation, expected), Is.LessThan(.05f), slot.ToString());
            Assert.IsTrue(console.KnobSettled, slot.ToString());
        }
    }
}
#endif
