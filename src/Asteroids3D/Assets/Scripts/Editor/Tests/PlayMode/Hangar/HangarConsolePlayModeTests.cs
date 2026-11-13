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
using UnityEngine.UI;

namespace Tests.PlayMode.Hangar
{
    [Category("UI")]
    [Category("RequiresGraphics")]
    public sealed class HangarConsolePlayModeTests
    {
        private const string ConsolePath = "Assets/Prefabs/UI/HangarConsole.prefab";

        private HangarConsole console;
        private ItemSubset offer;

        [TearDown]
        public void TearDown()
        {
            if (console) Object.Destroy(console.gameObject);
            if (offer) Object.Destroy(offer);
        }

        [UnityTest]
        public IEnumerator Show_FillsBothDisplays_AndTheKnobFollowsTheSelectedSlot()
        {
            var ship = AssetDatabase.LoadAssetAtPath<Ship>("Assets/Prefabs/Ships/Vanguard.prefab");
            offer = ScriptableObject.CreateInstance<ItemSubset>();
            offer.starter = ship;
            offer.ships = new[] { ship };
            console = Object.Instantiate(AssetDatabase.LoadAssetAtPath<HangarConsole>(ConsolePath));
            var rest = console.SelectorKnob.localRotation;
            console.Show(offer, new ShipLoadout(ship, null, null), _ => true, null);
            yield return null;

            var main = console.GetComponentInChildren<HangarMainDisplayView>();
            Assert.AreEqual($"Ship\n{ship.name}", main[HangarSlot.Ship].GetComponentInChildren<Text>().text);
            var detail = console.GetComponentInChildren<HangarDetailDisplayView>();
            Assert.AreEqual(ship.name, detail.Title);
            Assert.AreEqual(HangarDescriptions.Describe(ship), detail.Body);
            AssertAngle(rest, HangarSlot.Ship);
            console.Selection.Select(HangarSlot.Weapon);
            AssertAngle(rest, HangarSlot.Weapon);
            console.Selection.Select(HangarSlot.Ordnance);
            AssertAngle(rest, HangarSlot.Ordnance);
        }

        private void AssertAngle(Quaternion rest, HangarSlot slot)
        {
            var expected = rest * Quaternion.AngleAxis(((int)slot - 1) * console.KnobDetentDegrees, console.KnobAxis);
            Assert.That(Quaternion.Angle(console.SelectorKnob.localRotation, expected), Is.LessThan(.05f), slot.ToString());
        }
    }
}
#endif
