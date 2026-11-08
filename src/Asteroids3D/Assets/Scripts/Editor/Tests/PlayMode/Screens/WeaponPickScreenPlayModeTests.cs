#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using Combat.Weapons;
using Combat.Weapons.Mods;
using Loot;
using NUnit.Framework;
using Ships;
using Substrate.Sectors.Elements;
using UI.Screens;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Tests.PlayMode.Screens
{
    /// <summary>The weapon pick screen heads with the next sector's ships and weapons; each card names the mount its kind replaces, the held mods it would use and the held mods the swap would idle; one press answers, and the first answer stands.</summary>
    [Category("UI")]
    public class WeaponPickScreenPlayModeTests
    {
        private GameObject root;
        private WeaponPickScreen screen;
        private readonly List<WeaponMod> made = new();
        private WeaponComponent rippers;
        private WeaponComponent lasers;
        private WeaponComponent missiles;
        private WeaponComponent grenades;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("UI");
            rippers = Weapon("Rippers");
            lasers = Weapon("Lasers");
            missiles = Weapon("Missiles");
            grenades = Weapon("Grenades");
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

        private WeaponPickScreen Open(SectorThreat? threat = null, WeaponComponent ordnance = null)
        {
            var stack = new LullModStack();
            var hot = Mod("Hot rounds", WeaponModField.Damage);
            stack.Add(hot);
            stack.Add(hot);
            stack.Add(Mod("Coolant", WeaponModField.Cooling));
            stack.Add(Mod("Deep magazine", WeaponModField.Magazine, rippers));
            stack.Add(Mod("Extra missile", WeaponModField.Magazine, missiles));
            return WeaponPickScreen.Create(root.transform, 2, threat, new[] { lasers, grenades }, rippers, ordnance, stack);
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
            screen = Open(new SectorThreat(new[] { vanguard }, new[] { lasers, missiles }), missiles);
            yield return null;

            Assert.AreEqual("SECTOR 02 SENDS", Text("Next"));
            Assert.AreEqual("SHIPS   VANGUARD", Text("Ships"));
            Assert.AreEqual($"WEAPONS   {lasers.DisplayName.ToUpperInvariant()} · {missiles.DisplayName.ToUpperInvariant()}",
                Text("Weapons"));
            Assert.AreEqual("1", Text("Offers/Cards/Card1/KeyCap/Key"));
            Assert.AreEqual(lasers.DisplayName.ToUpperInvariant(), Text("Offers/Cards/Card1/Name"));
            Assert.AreEqual("HOT ROUNDS ×2 · COOLANT", Text("Offers/Cards/Card1/Uses"));
            Assert.AreEqual("HOT ROUNDS ×2", Text("Offers/Cards/Card2/Uses"), "the concussion has no heat");
            Assert.IsNull(screen.transform.Find("Offers/Cards/Card3"), "two offers build two cards");
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
        public IEnumerator EachCard_NamesTheMountItsKindReplaces_AndTheModsThatSwapIdles()
        {
            screen = Open(ordnance: missiles);
            yield return null;

            Assert.AreEqual($"REPLACES  {rippers.DisplayName.ToUpperInvariant()}", Text("Offers/Cards/Card1/Replaces"),
                "a weapon replaces the weapon slot's mount");
            Assert.AreEqual("DEEP MAGAZINE", Text("Offers/Cards/Card1/Idles"));
            Assert.AreEqual($"REPLACES  {missiles.DisplayName.ToUpperInvariant()}", Text("Offers/Cards/Card2/Replaces"),
                "an ordnance replaces the ordnance slot's mount");
            Assert.AreEqual("EXTRA MISSILE", Text("Offers/Cards/Card2/Idles"));
        }

        [UnityTest]
        public IEnumerator ACardForAnEmptySlot_ReplacesNothing_AndIdlesNothing()
        {
            screen = Open();
            yield return null;

            Assert.AreEqual("REPLACES  EMPTY", Text("Offers/Cards/Card2/Replaces"));
            Assert.AreEqual("NOTHING", Text("Offers/Cards/Card2/Idles"));
        }

        [UnityTest]
        public IEnumerator ChooseOffer_AnswersAtOnce_AndTheFirstAnswerStands()
        {
            screen = Open();
            yield return null;

            screen.ChooseOffer(1);
            Assert.AreEqual(1, screen.Choice, "picking an offer is the answer");

            screen.ChooseSkip();
            screen.ChooseOffer(0);
            Assert.AreEqual(1, screen.Choice, "the first answer stands");
            Assert.Throws<ArgumentOutOfRangeException>(() => screen.ChooseOffer(2), "no third card");
        }

        [UnityTest]
        public IEnumerator ChooseSkip_Skips_AndTheSkipStands()
        {
            screen = Open();
            yield return null;

            screen.ChooseSkip();
            screen.ChooseOffer(0);

            Assert.AreEqual(WeaponPickScreen.Skip, screen.Choice);
        }

        private Button ButtonAt(string path) => screen.transform.Find(path).GetComponent<Button>();

        [UnityTest]
        public IEnumerator Click_ACard_AnswersLikeItsKey()
        {
            screen = Open();
            yield return null;

            var card = ButtonAt("Offers/Cards/Card2");
            Assert.IsTrue(card.targetGraphic.raycastTarget, "the card's overlay takes the click");
            Assert.AreEqual(Navigation.Mode.None, card.navigation.mode, "no gamepad focus to submit a card by accident");
            card.onClick.Invoke();

            Assert.AreEqual(1, screen.Choice);
        }

        [UnityTest]
        public IEnumerator Click_Skip_AnswersLikeSpace()
        {
            screen = Open();
            yield return null;

            ButtonAt("Offers/Skip").onClick.Invoke();

            Assert.AreEqual(WeaponPickScreen.Skip, screen.Choice);
        }
    }
}
#endif
