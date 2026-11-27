#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using Combat.Weapons;
using NUnit.Framework;
using Ships;
using Ships.Loadout;
using UI.Comms;
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
        private int launches;

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
            var primary = console.PrimaryDisplay.GetComponent<HangarWeaponDisplayView>();
            var ordnance = console.OrdnanceDisplay.GetComponent<HangarWeaponDisplayView>();
            Assert.AreEqual(ship.name, main.Title);
            Assert.IsFalse(main.Locked);
            Assert.IsTrue(main.Bracketed, "the ship slot opens bracketed");
            Assert.IsFalse(primary.Bracketed);
            Assert.IsFalse(ordnance.Bracketed);
            var detail = console.GetComponentInChildren<HangarDetailDisplayView>();
            Assert.AreEqual(ship.name, detail.Title);
            Assert.AreEqual(HangarDescriptions.Describe(ship), detail.Body);
            AssertDetent(HangarSlot.Ship);

            console.Selection.Select(HangarSlot.Weapon);
            Assert.IsFalse(console.KnobSettled, "a new slot sends the knob on its way");
            yield return Settled(() => console.KnobSettled);
            AssertDetent(HangarSlot.Weapon);
            Assert.IsTrue(primary.Bracketed);
            Assert.IsFalse(main.Bracketed);
            Assert.IsFalse(ordnance.Bracketed);
            Assert.AreEqual(ship.name, detail.Title, "the detail display stays on the ship");
            Assert.AreEqual(HangarDescriptions.Describe(ship), detail.Body);

            console.Selection.Select(HangarSlot.Ordnance);
            yield return Settled(() => console.KnobSettled);
            AssertDetent(HangarSlot.Ordnance);
            Assert.IsTrue(ordnance.Bracketed);
            Assert.IsFalse(main.Bracketed);
            Assert.IsFalse(primary.Bracketed);
            Assert.AreEqual(ship.name, detail.Title, "the detail display stays on the ship");
            Assert.AreEqual(HangarDescriptions.Describe(ship), detail.Body);
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

        [UnityTest]
        public IEnumerator Launch_LocksInput_PressesTheButton_PowersTheScreensDown_AndHandsOffOnce()
        {
            Open(_ => true, out _);
            yield return null;
            Assert.IsFalse(console.InputLocked);

            console.Launch();
            Assert.IsTrue(console.InputLocked, "an accepted launch locks the keys and the pointer");
            Assert.IsFalse(console.GetComponent<HangarConsoleRaycaster>().enabled, "the pointer no longer reaches the console");
            Assert.IsTrue(console.LaunchButton.GetComponent<HangarControl>().Moving, "the launch button plays its press");
            Assert.IsTrue(console.MainDisplay.PoweringDown && console.DetailDisplay.PoweringDown
                && console.PrimaryDisplay.PoweringDown && console.OrdnanceDisplay.PoweringDown,
                "the screens power down");
            Assert.AreEqual(0, launches, "the hand-off waits for the power-down");
            console.Launch();

            var start = Time.unscaledTime;
            var mainLit = float.MaxValue;
            while (launches == 0 && Time.unscaledTime - start < SettleTimeout)
            {
                mainLit = Mathf.Min(mainLit, console.MainDisplay.Level);
                yield return null;
            }
            Assert.AreEqual(1, launches, "the callback fires once the power-down is over");
            Assert.That(Time.unscaledTime - start, Is.GreaterThanOrEqualTo(HangarDisplay.PowerDownSeconds * .8f), "not before the screens are dark");
            Assert.That(console.MainDisplay.Level, Is.LessThan(.01f), "the main screen ends dark");
            Assert.That(console.DetailDisplay.Level, Is.LessThan(.01f), "the detail screen ends dark");
            Assert.That(console.PrimaryDisplay.Level, Is.LessThan(.01f), "the primary screen ends dark");
            Assert.That(console.OrdnanceDisplay.Level, Is.LessThan(.01f), "the ordnance screen ends dark");
            console.Launch();
            yield return new WaitForSecondsRealtime(HangarDisplay.PowerDownSeconds + .2f);
            Assert.AreEqual(1, launches, "later launches do nothing");
        }

        [UnityTest]
        public IEnumerator Launch_ShowsTheLampsArmed_ThenPressedOrBlack()
        {
            Open(_ => true, out _);
            yield return null;
            var data = new SerializedObject(console);
            var profile = (HangarLightProfile)data.FindProperty("lightProfile").objectReferenceValue;
            var lamps = data.FindProperty("launchLamps");
            Assert.That(lamps.arraySize, Is.GreaterThan(0), "the console wires its launch lamps");
            var emission = Shader.PropertyToID("_ConstantEmission");
            var block = new MaterialPropertyBlock();
            for (var i = 0; i < lamps.arraySize; i++)
            {
                var lamp = (MeshRenderer)lamps.GetArrayElementAtIndex(i).objectReferenceValue;
                lamp.GetPropertyBlock(block);
                Assert.IsTrue(block.GetColor(emission) == lamp.sharedMaterial.GetColor(emission), lamp.name);
            }

            console.Launch();
            yield return null;
            var pressed = profile.LaunchPressedColor;
            for (var i = 0; i < lamps.arraySize; i++)
            {
                var lamp = (MeshRenderer)lamps.GetArrayElementAtIndex(i).objectReferenceValue;
                lamp.GetPropertyBlock(block);
                var color = block.GetColor(emission);
                Assert.IsTrue(color == pressed || color == Color.black, lamp.name);
            }
        }

        [UnityTest]
        public IEnumerator StatusLamps_EachBlinkOnItsOwn()
        {
            Open(_ => true, out _);
            yield return null;
            var data = new SerializedObject(console);
            var lamps = data.FindProperty("statusLamps");
            var glows = data.FindProperty("statusLampGlows");
            Assert.AreEqual(6, lamps.arraySize, "six status lamps");
            Assert.AreEqual(6, glows.arraySize, "one glow per status lamp");

            for (var i = 0; i < glows.arraySize; i++)
                Assert.IsTrue(glows.GetArrayElementAtIndex(i).FindPropertyRelative("card").objectReferenceValue,
                    $"status lamp {i + 1} has a glow card");

            // Noise can hold a lamp steady for a while, so sample until each has changed, within a bound.
            var seen = new HashSet<Color>[lamps.arraySize];
            for (var i = 0; i < seen.Length; i++) seen[i] = new HashSet<Color>();
            var differed = false;
            var start = Time.unscaledTime;
            while (Time.unscaledTime - start < 1.5f ||
                   (Time.unscaledTime - start < 6f && (!differed || System.Array.Exists(seen, values => values.Count < 2))))
            {
                yield return null;
                var levels = new float[seen.Length];
                for (var i = 0; i < seen.Length; i++)
                {
                    var color = LampColor(lamps, i);
                    seen[i].Add(color);
                    levels[i] = color.maxColorComponent /
                                glows.GetArrayElementAtIndex(i).FindPropertyRelative("color").colorValue.maxColorComponent;
                    if (Mathf.Abs(levels[i] - levels[0]) > .01f) differed = true;
                }
            }
            for (var i = 0; i < seen.Length; i++)
                Assert.That(seen[i].Count, Is.GreaterThan(1), $"status lamp {i + 1} blinks");
            Assert.IsTrue(differed, "the status lamps are not all alike in some frame");
        }

        [UnityTest]
        public IEnumerator Launch_DoesNothing_WhileTheShipIsLocked()
        {
            Open(ship => ship.name == "Vanguard", out _);
            yield return null;
            console.Selection.Step(1);
            Assert.IsTrue(console.Selection.ShipLocked, "test premise: the second ship is locked");

            console.Launch();
            Assert.IsFalse(console.InputLocked, "a refused launch leaves the console live");
            Assert.IsFalse(console.MainDisplay.PoweringDown);
            Assert.IsFalse(console.PrimaryDisplay.PoweringDown);
            Assert.IsFalse(console.OrdnanceDisplay.PoweringDown);
            yield return new WaitForSecondsRealtime(HangarDisplay.PowerDownSeconds + .2f);
            Assert.AreEqual(0, launches);
            Assert.That(console.MainDisplay.Level, Is.GreaterThan(.8f), "the screen stays lit");
            Assert.That(console.PrimaryDisplay.Level, Is.GreaterThan(.8f), "the primary screen stays lit");
            Assert.That(console.OrdnanceDisplay.Level, Is.GreaterThan(.8f), "the ordnance screen stays lit");
        }

        [UnityTest]
        public IEnumerator SideKeys_StepTheirOwnSlot_AndSelectIt()
        {
            var catalog = ShowCatalog(onlyStarter: false);
            yield return null;
            var primaries = Kind(catalog, ordnance: false);
            var ordnance = Kind(catalog, ordnance: true);
            Assert.That(primaries.Length, Is.GreaterThan(1), "test premise: the weapon offer has a step");
            Assert.That(ordnance.Length, Is.GreaterThan(1), "test premise: the ordnance offer has a step");

            Press(console.PrimaryNextButton);
            Assert.AreEqual(HangarSlot.Weapon, console.Selection.Selected);
            Assert.AreSame(primaries[0], console.Selection.Weapon);
            Press(console.PrimaryNextButton);
            Assert.AreSame(primaries[1], console.Selection.Weapon);
            Assert.AreEqual(HangarSlot.Weapon, console.Selection.Selected);
            Press(console.PrimaryPreviousButton);
            Assert.AreSame(primaries[0], console.Selection.Weapon);
            Assert.AreEqual(HangarSlot.Weapon, console.Selection.Selected);

            Press(console.OrdnanceNextButton);
            Assert.AreEqual(HangarSlot.Ordnance, console.Selection.Selected);
            Assert.AreSame(ordnance[0], console.Selection.Ordnance);
            Assert.AreSame(primaries[0], console.Selection.Weapon);
            Press(console.OrdnanceNextButton);
            Assert.AreSame(ordnance[1], console.Selection.Ordnance);
            Press(console.OrdnancePreviousButton);
            Assert.AreSame(ordnance[0], console.Selection.Ordnance);
            Assert.AreEqual(HangarSlot.Ordnance, console.Selection.Selected);
        }

        [UnityTest]
        public IEnumerator NextKey_StepsTheShip_WhileTheWeaponSlotIsSelected()
        {
            Open(_ => true, out var second);
            yield return null;
            console.Selection.Select(HangarSlot.Weapon);
            Assert.AreEqual(HangarSlot.Weapon, console.Selection.Selected, "test premise: the weapon slot is selected");
            Press(console.NextButton);
            Assert.AreSame(second, console.Selection.Ship, "the next key steps the ship");
            Assert.AreEqual(HangarSlot.Ship, console.Selection.Selected, "the ship slot is selected");
        }

        [UnityTest]
        public IEnumerator LockedShip_DimsBothWeaponScreens_AndThePrimaryScreenReadsEachWeapon()
        {
            var catalog = ShowCatalog(onlyStarter: true);
            yield return null;
            var primary = console.PrimaryDisplay.GetComponent<HangarWeaponDisplayView>();
            var ordnanceView = console.OrdnanceDisplay.GetComponent<HangarWeaponDisplayView>();
            Assert.IsFalse(primary.Dimmed, "the starter leaves the weapon screens lit");
            Assert.IsFalse(ordnanceView.Dimmed);

            var primaries = Kind(catalog, ordnance: false);
            Assert.That(primaries.Length, Is.GreaterThan(0), "test premise: the offer holds weapons");
            console.Selection.Select(HangarSlot.Weapon);
            for (var i = 0; i < primaries.Length; i++)
            {
                console.Selection.Step(1);
                var weapon = primaries[i];
                Assert.AreSame(weapon, console.Selection.Weapon, weapon.name);
                Assert.AreEqual(weapon.DisplayName, primary.Title, weapon.name);
                Assert.AreEqual(weapon.HangarDescription, primary.Description, weapon.name);
                var (number, caption) = HangarDescriptions.Damage(weapon);
                Assert.AreEqual(number, primary.Damage, weapon.name);
                Assert.AreEqual(caption, primary.DamageCaption, weapon.name);
                Assert.AreEqual(weapon.HangarTags.Count, primary.TagCount, weapon.name);
                for (var tag = 0; tag < weapon.HangarTags.Count; tag++)
                    Assert.AreEqual(weapon.HangarTags[tag].ToUpperInvariant(), primary.Tag(tag), weapon.name);
            }

            Assert.That(catalog.ships.Length, Is.GreaterThan(1), "test premise: a second ship exists to lock");
            console.Selection.Select(HangarSlot.Ship);
            console.Selection.Step(1);
            Assert.IsTrue(console.Selection.ShipLocked, "test premise: the other ship is locked");
            Assert.IsTrue(primary.Dimmed && ordnanceView.Dimmed, "a locked ship dims both weapon screens");
            console.Selection.Step(-1);
            Assert.IsFalse(console.Selection.ShipLocked);
            Assert.IsFalse(primary.Dimmed || ordnanceView.Dimmed, "the starter leaves both weapon screens lit");
        }

        [UnityTest]
        public IEnumerator Show_PaintsAtLeastSixLightsOnEveryPart()
        {
            Open(_ => true, out _);
            yield return null;
            var painted = new SerializedObject(console).FindProperty("paintedParts");
            var block = new MaterialPropertyBlock();
            var countId = Shader.PropertyToID("_HangarLightCount");
            for (var i = 0; i < painted.arraySize; i++)
            {
                var part = (MeshRenderer)painted.GetArrayElementAtIndex(i).objectReferenceValue;
                part.GetPropertyBlock(block);
                Assert.That(block.GetFloat(countId), Is.GreaterThanOrEqualTo(6f), part.name);
            }
        }

        [UnityTest]
        public IEnumerator ShowWithTutorial_ShowsTheChecklist_HintsTheBracketedScreen_AndGreetsOnce()
        {
            var greeted = new List<CommLine>();
            Open(_ => true, out _, tutorial: true, greeted.Add);
            yield return null;

            var main = console.GetComponentInChildren<HangarMainDisplayView>();
            var primary = console.PrimaryDisplay.GetComponent<HangarWeaponDisplayView>();
            var ordnance = console.OrdnanceDisplay.GetComponent<HangarWeaponDisplayView>();
            Assert.IsTrue(console.ChecklistShown, "the bay checklist is up");
            Assert.IsTrue(main.Hinted, "the bracketed ship screen names its keys");
            Assert.IsFalse(primary.Hinted);
            Assert.IsFalse(ordnance.Hinted);

            console.Selection.Select(HangarSlot.Weapon);
            Assert.IsFalse(main.Hinted);
            Assert.IsTrue(primary.Hinted, "the hint follows the bracket");

            yield return Elapsed(GreetWait);
            Assert.AreEqual(1, greeted.Count, "the bay greets once per visit");
            Assert.AreSame(console.CommLine, greeted[0], "the greeting is handed the bay's comm line");
        }

        [UnityTest]
        public IEnumerator ShowWithoutTutorial_ShowsNoChecklistOrHint_AndNeverGreets()
        {
            var greeted = new List<CommLine>();
            Open(_ => true, out _, tutorial: false, greeted.Add);
            yield return null;

            Assert.IsFalse(console.ChecklistShown);
            Assert.IsFalse(console.GetComponentInChildren<HangarMainDisplayView>().Hinted);
            Assert.IsFalse(console.PrimaryDisplay.GetComponent<HangarWeaponDisplayView>().Hinted);
            yield return Elapsed(GreetWait);
            Assert.IsEmpty(greeted, "no tutorial, no greeting");
        }

        private const float GreetWait = 2.5f;

        private static IEnumerator Elapsed(float seconds)
        {
            var start = Time.unscaledTime;
            while (Time.unscaledTime - start < seconds) yield return null;
        }

        private Ship Open(System.Func<Ship, bool> earned, out Ship second, bool tutorial = false,
            System.Action<CommLine> greet = null)
        {
            var vanguard = AssetDatabase.LoadAssetAtPath<Ship>(VanguardPath);
            second = AssetDatabase.LoadAssetAtPath<Ship>(ValisPath);
            offer = ScriptableObject.CreateInstance<ItemSubset>();
            offer.starter = vanguard;
            offer.ships = new[] { vanguard, second };
            console = Object.Instantiate(AssetDatabase.LoadAssetAtPath<HangarConsole>(ConsolePath));
            knobRest = console.SelectorKnob.localRotation;
            launches = 0;
            console.Show(offer, new ShipLoadout(vanguard, null, null), earned, tutorial, greet, () => launches++);
            return vanguard;
        }

        private ItemSubset ShowCatalog(bool onlyStarter)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<Game.GameHost>("Assets/Prefabs/Game/GameHost.prefab").hangarOffer;
            console = Object.Instantiate(AssetDatabase.LoadAssetAtPath<HangarConsole>(ConsolePath));
            launches = 0;
            System.Func<Ship, bool> earned = _ => true;
            if (onlyStarter) earned = ship => ship == catalog.starter;
            console.Show(catalog, new ShipLoadout(catalog.starter, null, null), earned, tutorial: false, greet: null,
                () => launches++);
            return catalog;
        }

        private static void Press(Transform part) => part.GetComponent<HangarControl>().Press();

        private static Color LampColor(SerializedProperty lamps, int index)
        {
            var lamp = (MeshRenderer)lamps.GetArrayElementAtIndex(index).objectReferenceValue;
            var block = new MaterialPropertyBlock();
            lamp.GetPropertyBlock(block);
            return block.GetColor(Shader.PropertyToID("_ConstantEmission"));
        }

        private static WeaponComponent[] Kind(ItemSubset catalog, bool ordnance)
        {
            var list = new List<WeaponComponent>();
            foreach (var weapon in catalog.weapons)
                if (weapon && weapon.IsOrdnance == ordnance) list.Add(weapon);
            return list.ToArray();
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
