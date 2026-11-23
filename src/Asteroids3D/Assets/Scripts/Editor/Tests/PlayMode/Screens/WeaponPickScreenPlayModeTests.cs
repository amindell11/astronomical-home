#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using Combat.Weapons;
using Combat.Weapons.Mods;
using Loot;
using NUnit.Framework;
using UI.Screens;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Tests.PlayMode.Screens
{
    /// <summary>The weapon pick screen builds a window per offer; each names the mount its kind replaces, the held mods it would use and the held mods the swap would idle; one press answers, and the first answer stands.</summary>
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

        private WeaponPickScreen Open(WeaponComponent ordnance = null)
        {
            var stack = new LullModStack();
            var hot = Mod("Hot rounds", WeaponModField.Damage);
            stack.Add(hot);
            stack.Add(hot);
            stack.Add(Mod("Coolant", WeaponModField.Cooling));
            stack.Add(Mod("Deep magazine", WeaponModField.Magazine, rippers));
            stack.Add(Mod("Extra missile", WeaponModField.Magazine, missiles));
            return WeaponPickScreen.Create(root.transform, new[] { lasers, grenades }, rippers, ordnance, stack, null);
        }

        private string Text(string path)
        {
            var node = screen.transform.Find(path);
            Assert.IsNotNull(node, $"screen has {path}");
            return node.GetComponent<Text>().text;
        }

        [UnityTest]
        public IEnumerator Create_BuildsOneWindowPerOffer_ShowingTheModsEachOfferUses()
        {
            screen = Open(missiles);
            yield return null;

            Assert.AreEqual("WEAPON 01", Text("Offers/Cards/Card1/Title"));
            Assert.AreEqual(lasers.DisplayName, Text("Offers/Cards/Card1/Body/Name"));
            Assert.AreSame(lasers.HudIcon, screen.transform.Find("Offers/Cards/Card1/Body/Icon").GetComponent<Image>().sprite);
            Assert.AreEqual("+ HOT ROUNDS ×2", Text("Offers/Cards/Card1/Body/Uses/Chip1/Label"));
            Assert.AreEqual("+ COOLANT", Text("Offers/Cards/Card1/Body/Uses/Chip2/Label"));
            Assert.AreEqual("+ HOT ROUNDS ×2", Text("Offers/Cards/Card2/Body/Uses/Chip1/Label"));
            Assert.IsNull(screen.transform.Find("Offers/Cards/Card2/Body/Uses/Chip2"), "the concussion has no heat");
            Assert.IsNull(screen.transform.Find("Offers/Cards/Card3"), "two offers build two cards");
            Assert.AreEqual("Michroma", screen.transform.Find("Title").GetComponent<Text>().font.name);
            Assert.IsFalse(screen.Choice.HasValue);
        }

        [UnityTest]
        public IEnumerator EachCard_NamesTheMountItsKindReplaces_AndTheModsThatSwapIdles()
        {
            screen = Open(missiles);
            yield return null;

            Assert.AreEqual(rippers.DisplayName.ToUpperInvariant(), Text("Offers/Cards/Card1/Body/Replaces"),
                "a weapon replaces the weapon slot's mount");
            Assert.AreEqual("− DEEP MAGAZINE", Text("Offers/Cards/Card1/Body/Idles/Chip1/Label"));
            Assert.AreEqual(missiles.DisplayName.ToUpperInvariant(), Text("Offers/Cards/Card2/Body/Replaces"),
                "an ordnance replaces the ordnance slot's mount");
            Assert.AreEqual("− EXTRA MISSILE", Text("Offers/Cards/Card2/Body/Idles/Chip1/Label"));
        }

        [UnityTest]
        public IEnumerator ACardForAnEmptySlot_ReplacesNothing_AndIdlesNothing()
        {
            screen = Open();
            yield return null;

            Assert.AreEqual("EMPTY", Text("Offers/Cards/Card2/Body/Replaces"));
            Assert.AreEqual("NOTHING", Text("Offers/Cards/Card2/Body/Idles/None"));
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
        public IEnumerator Click_Skip_AnswersLikeEscape()
        {
            screen = Open();
            yield return null;

            ButtonAt("Offers/Skip").onClick.Invoke();

            Assert.AreEqual(WeaponPickScreen.Skip, screen.Choice);
        }
    }
}
#endif
