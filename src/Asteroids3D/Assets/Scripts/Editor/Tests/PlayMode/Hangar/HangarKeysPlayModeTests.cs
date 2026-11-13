#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using Ships;
using Ships.Loadout;
using UI.Hangar.Hardware;
using UI.Hangar.Selection;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using Tests.PlayMode.Common.SimulatedInput;

namespace Tests.PlayMode.Hangar
{
    [Category("UI")]
    [Category("RequiresGraphics")]
    public sealed class HangarKeysPlayModeTests : PlayModeInputTestFixture
    {
        private const string ConsolePath = "Assets/Prefabs/UI/HangarConsole.prefab";
        private const string ValisPath = "Assets/Prefabs/Ships/Valis.prefab";
        private const string NightshadePath = "Assets/Prefabs/Ships/Nightshade.prefab";

        private HangarConsole console;
        private ItemSubset offer;

        [TearDown]
        public void TearDown()
        {
            if (console) Object.Destroy(console.gameObject);
            if (offer) Object.Destroy(offer);
        }

        [UnityTest]
        public IEnumerator RightDownAndEnter_StepTurnAndLaunch()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var valis = AssetDatabase.LoadAssetAtPath<Ship>(ValisPath);
            var nightshade = AssetDatabase.LoadAssetAtPath<Ship>(NightshadePath);
            offer = ScriptableObject.CreateInstance<ItemSubset>();
            offer.starter = valis;
            offer.ships = new[] { valis, nightshade };
            console = Object.Instantiate(AssetDatabase.LoadAssetAtPath<HangarConsole>(ConsolePath));
            var launched = false;
            console.Show(offer, new ShipLoadout(valis, null, null), _ => true, () => launched = true);
            yield return null;

            Press(keyboard.rightArrowKey);
            InputSystem.Update();
            yield return null;
            Assert.AreSame(nightshade, console.Selection.Ship, "Right steps the selected ship");

            Press(keyboard.downArrowKey);
            InputSystem.Update();
            yield return null;
            Assert.AreEqual(HangarSlot.Weapon, console.Selection.Selected, "Down moves to the next slot");

            Press(keyboard.enterKey);
            InputSystem.Update();
            yield return null;
            Assert.IsTrue(console.InputLocked, "Enter locks the keys");
            Press(keyboard.leftArrowKey);
            InputSystem.Update();
            yield return null;
            Assert.AreSame(nightshade, console.Selection.Ship, "a key after Enter steps nothing");
            var start = Time.unscaledTime;
            while (!launched && Time.unscaledTime - start < 3f) yield return null;
            Assert.IsTrue(launched, "Enter launches once the screens have powered down");
        }
    }
}
#endif
