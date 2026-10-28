#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using Combat.Weapons;
using Combat.Weapons.Mods;
using NUnit.Framework;
using Ships;
using Ships.Command;
using Substrate.Sectors.Elements;
using UI.Screens;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Tests.PlayMode.Screens
{
    /// <summary>The weapon pick screen heads with the next sector's ships and weapons, shows the held mods each offer would use, opens a mount step naming what each slot replaces and idles, steps back on Space, and the first answer stands.</summary>
    [Category("UI")]
    public class WeaponPickScreenPlayModeTests
    {
        private GameObject root;
        private WeaponPickScreen screen;
        private readonly List<WeaponMod> made = new();
        private WeaponComponent rippers;
        private WeaponComponent lasers;
        private WeaponComponent missiles;
        private WeaponComponent railgun;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("UI");
            rippers = Weapon("Rippers");
            lasers = Weapon("Lasers");
            missiles = Weapon("Missiles");
            railgun = Weapon("Railgun");
        }

        [TearDown]
        public void TearDown()
        {
            if (screen) Object.Destroy(screen.gameObject);
            Object.Destroy(root);
            foreach (var mod in made) Object.DestroyImmediate(mod);
            made.Clear();
        }

        private static WeaponComponent Weapon(string name) =>
            AssetDatabase.LoadAssetAtPath<WeaponComponent>($"Assets/Prefabs/Weapons/{name}.prefab");

        private WeaponMod Mod(string name, WeaponModField field, params WeaponComponent[] fits)
        {
            var mod = ScriptableObject.CreateInstance<WeaponMod>();
            mod.name = name;
            mod.Configure(name, field, field == WeaponModField.Magazine ? 6f : 0.25f, fits);
            made.Add(mod);
            return mod;
        }

        // Rippers primary, lasers secondary; held: hot rounds twice, coolant, a rippers-only magazine.
        private WeaponPickScreen Open(SectorThreat? threat = null)
        {
            var stack = new WeaponModStack();
            var hot = Mod("Hot rounds", WeaponModField.Damage);
            stack.Add(hot);
            stack.Add(hot);
            stack.Add(Mod("Coolant", WeaponModField.Cooling));
            stack.Add(Mod("Deep magazine", WeaponModField.Magazine, rippers));
            return WeaponPickScreen.Create(root.transform, 2, threat, new[] { missiles, railgun }, rippers, lasers, stack);
        }

        private string Text(string path)
        {
            var node = screen.transform.Find(path);
            Assert.IsNotNull(node, $"screen has {path}");
            return node.GetComponent<Text>().text;
        }

        [UnityTest]
        public IEnumerator Create_HeadsWithTheThreat_AndCardsShowTheModsEachOfferUses()
        {
            var vanguard = AssetDatabase.LoadAssetAtPath<Ship>("Assets/Prefabs/Ships/Vanguard.prefab");
            screen = Open(new SectorThreat(new[] { vanguard }, new[] { lasers, missiles }));
            yield return null;

            Assert.AreEqual("SECTOR 02 SENDS", Text("Next"));
            Assert.AreEqual("SHIPS   VANGUARD", Text("Ships"));
            Assert.AreEqual($"WEAPONS   {lasers.DisplayName.ToUpperInvariant()} · {missiles.DisplayName.ToUpperInvariant()}",
                Text("Weapons"));
            Assert.AreEqual("1", Text("Offers/Cards/Card1/KeyCap/Key"));
            Assert.AreEqual(missiles.DisplayName.ToUpperInvariant(), Text("Offers/Cards/Card1/Name"));
            Assert.AreEqual("HOT ROUNDS ×2", Text("Offers/Cards/Card1/Uses"));
            Assert.AreEqual("HOT ROUNDS ×2", Text("Offers/Cards/Card2/Uses"), "the railgun has no heat");
            Assert.IsNull(screen.transform.Find("Offers/Cards/Card3"), "two offers build two cards");
            Assert.IsNull(screen.transform.Find("Mount"), "the mount step waits for an offer");
            Assert.IsFalse(screen.Choice.HasValue);
        }

        [UnityTest]
        public IEnumerator Create_WithNoThreat_ShowsNoShipsOrWeaponsLine()
        {
            screen = Open();
            yield return null;

            Assert.AreEqual("SECTOR 02 SENDS", Text("Next"));
            Assert.IsNull(screen.transform.Find("Ships"));
            Assert.IsNull(screen.transform.Find("Weapons"));
        }

        [UnityTest]
        public IEnumerator ChooseOffer_OpensTheMountStep_NamingWhatEachSlotReplacesAndIdles()
        {
            screen = Open();
            yield return null;

            screen.ChooseOffer(0);
            yield return null;

            Assert.IsFalse(screen.transform.Find("Offers").gameObject.activeSelf);
            Assert.AreEqual("PRIMARY · LEFT MOUSE", Text("Mount/Cards/Card1/Name"));
            Assert.AreEqual($"REPLACES  {rippers.DisplayName.ToUpperInvariant()}", Text("Mount/Cards/Card1/Replaces"));
            Assert.AreEqual("DEEP MAGAZINE", Text("Mount/Cards/Card1/Idles"));
            Assert.AreEqual("SECONDARY · RIGHT MOUSE", Text("Mount/Cards/Card2/Name"));
            Assert.AreEqual("COOLANT", Text("Mount/Cards/Card2/Idles"));
            Assert.IsFalse(screen.Choice.HasValue, "picking an offer is not an answer yet");

            screen.ChooseSlot(WeaponSlot.Secondary);
            screen.SkipOrBack();
            screen.ChooseSlot(WeaponSlot.Primary);
            Assert.AreEqual(0, screen.Choice);
            Assert.AreEqual(WeaponSlot.Secondary, screen.Slot, "the first answer stands");
        }

        [UnityTest]
        public IEnumerator SkipOrBack_StepsBackFromTheMount_ThenSkips()
        {
            screen = Open();
            yield return null;

            screen.ChooseOffer(1);
            screen.SkipOrBack();
            yield return null;

            Assert.IsTrue(screen.transform.Find("Offers").gameObject.activeSelf, "back to the offers");
            Assert.IsNull(screen.transform.Find("Mount"));
            Assert.IsFalse(screen.Choice.HasValue, "going back is not a skip");
            Assert.Throws<ArgumentOutOfRangeException>(() => screen.ChooseOffer(2), "no third card");

            screen.SkipOrBack();
            Assert.AreEqual(WeaponPickScreen.Skip, screen.Choice);
        }

        [UnityTest]
        public IEnumerator Click_ACardThenASlot_AnswersLikeTheKeys()
        {
            screen = Open();
            yield return null;

            screen.transform.Find("Offers/Cards/Card2").GetComponent<Button>().onClick.Invoke();
            yield return null;
            screen.transform.Find("Mount/Cards/Card1").GetComponent<Button>().onClick.Invoke();

            Assert.AreEqual(1, screen.Choice);
            Assert.AreEqual(WeaponSlot.Primary, screen.Slot);
        }
    }
}
#endif
