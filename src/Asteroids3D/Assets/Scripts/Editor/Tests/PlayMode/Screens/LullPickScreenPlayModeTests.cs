using System;
using System.Collections;
using System.Collections.Generic;
using Combat.Weapons.Mods;
using NUnit.Framework;
using UI.Screens;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Tests.PlayMode.Screens
{
    /// <summary>The lull pick screen builds one window per offer with its number, name, effect and landing weapon, and the first answer stands.</summary>
    [Category("UI")]
    public class LullPickScreenPlayModeTests
    {
        private GameObject root;
        private LullPickScreen screen;
        private readonly List<WeaponMod> made = new();

        [SetUp]
        public void SetUp() => root = new GameObject("UI");

        [TearDown]
        public void TearDown()
        {
            if (screen) Object.Destroy(screen.gameObject);
            Object.Destroy(root);
            foreach (var mod in made) Object.DestroyImmediate(mod);
            made.Clear();
        }

        private WeaponMod Mod(string name, WeaponModField field, float amount)
        {
            var mod = ScriptableObject.CreateInstance<WeaponMod>();
            mod.name = name;
#if UNITY_EDITOR
            mod.Configure(name, field, amount);
#endif
            made.Add(mod);
            return mod;
        }

        private string Text(string path)
        {
            var node = screen.transform.Find(path);
            Assert.IsNotNull(node, $"screen has {path}");
            return node.GetComponent<Text>().text;
        }

        [UnityTest]
        public IEnumerator Create_BuildsOneWindowPerOffer_WithNumberNameEffectAndLanding()
        {
            var offers = new List<WeaponMod>
            {
                Mod("Rapid fire", WeaponModField.FireRate, 0.25f),
                Mod("Extra missile", WeaponModField.Magazine, 1f),
            };
            screen = LullPickScreen.Create(root.transform, offers,
                mod => ((WeaponMod)mod).Field == WeaponModField.Magazine ? "Missiles" : "Lasers · Missiles", null);
            yield return null;

            Assert.AreEqual("MOD 01", Text("Cards/Card1/Title"));
            Assert.AreEqual("Rapid fire", Text("Cards/Card1/Body/Name"));
            Assert.AreEqual("+25%", Text("Cards/Card1/Body/Effect"));
            Assert.AreEqual("FIRE RATE", Text("Cards/Card1/Body/Unit"));
            Assert.AreEqual("LASERS · MISSILES", Text("Cards/Card1/Body/Landing"));
            Assert.AreEqual("MOD 02", Text("Cards/Card2/Title"));
            Assert.AreEqual("+1", Text("Cards/Card2/Body/Effect"));
            Assert.AreEqual("ROUND", Text("Cards/Card2/Body/Unit"));
            Assert.AreEqual("MISSILES", Text("Cards/Card2/Body/Landing"));
            Assert.AreEqual("Orbitron-Medium", screen.transform.Find("Cards/Card1/Body/Name").GetComponent<Text>().font.name);
            Assert.AreEqual("Rajdhani-Medium", screen.transform.Find("Cards/Card1/Title").GetComponent<Text>().font.name);
            Assert.IsNull(screen.transform.Find("Cards/Card3"), "two offers build two cards");
            Assert.IsFalse(screen.Choice.HasValue, "unanswered until a key");
        }

        [UnityTest]
        public IEnumerator Choose_FirstAnswerStands()
        {
            var offers = new List<WeaponMod>
            {
                Mod("a", WeaponModField.Damage, 0.25f),
                Mod("b", WeaponModField.Damage, 0.25f),
                Mod("c", WeaponModField.Damage, 0.25f),
            };
            screen = LullPickScreen.Create(root.transform, offers, _ => "Lasers", null);
            yield return null;

            screen.Choose(1);
            screen.Choose(LullPickScreen.Skip);
            Assert.AreEqual(1, screen.Choice, "the second answer is ignored");
            Assert.Throws<ArgumentOutOfRangeException>(() => screen.Choose(3), "no fourth card");
        }

        [UnityTest]
        public IEnumerator Click_ACardOrTheSkip_AnswersLikeItsKey()
        {
            var offers = new List<WeaponMod>
            {
                Mod("a", WeaponModField.Damage, 0.25f),
                Mod("b", WeaponModField.Damage, 0.25f),
            };
            screen = LullPickScreen.Create(root.transform, offers, _ => "Lasers", null);
            yield return null;

            var card = screen.transform.Find("Cards/Card2").GetComponent<Button>();
            Assert.IsNotNull(card, "each card is a button");
            Assert.IsTrue(card.targetGraphic.raycastTarget, "the card's overlay takes the click");
            Assert.AreEqual(Navigation.Mode.None, card.navigation.mode, "no gamepad focus to submit a card by accident");
            card.onClick.Invoke();
            Assert.AreEqual(1, screen.Choice, "clicking the second card picks it");

            Object.Destroy(screen.gameObject);
            screen = LullPickScreen.Create(root.transform, offers, _ => "Lasers", null);
            yield return null;

            var skip = screen.transform.Find("Skip").GetComponent<Button>();
            Assert.IsNotNull(skip, "the skip is a button");
            skip.onClick.Invoke();
            Assert.AreEqual(LullPickScreen.Skip, screen.Choice, "clicking skip skips");
        }

        [UnityTest]
        public IEnumerator Choose_Skip_ReadsAsSkip()
        {
            screen = LullPickScreen.Create(root.transform, new List<WeaponMod> { Mod("a", WeaponModField.Damage, 0.25f) },
                _ => "Lasers", null);
            yield return null;

            screen.Choose(LullPickScreen.Skip);
            Assert.AreEqual(LullPickScreen.Skip, screen.Choice);
        }

        [Test]
        public void Create_RefusesAnEmptyOrOversizedOffer()
        {
            Assert.Throws<ArgumentException>(() => LullPickScreen.Create(root.transform, new List<WeaponMod>(), _ => "", null));
            var four = new List<WeaponMod>();
            for (var i = 0; i < 4; i++) four.Add(Mod($"m{i}", WeaponModField.Damage, 0.25f));
            Assert.Throws<ArgumentException>(() => LullPickScreen.Create(root.transform, four, _ => "", null));
        }
    }
}
