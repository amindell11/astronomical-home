using System;
using System.Collections;
using System.Collections.Generic;
using Combat.Weapons.Mods;
using Loot;
using NUnit.Framework;
using UI.Screens;
using UI.Screens.Windows;
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
                mod => ((WeaponMod)mod).Field == WeaponModField.Magazine ? "Missiles" : "Lasers · Missiles",
                _ => null, null);
            yield return null;

            Assert.AreEqual("MOD 01", Text("Cards/Card1/Title"));
            Assert.AreEqual("Rapid fire", Text("Cards/Card1/Body/Name"));
            Assert.AreEqual("+25% fire rate", Text("Cards/Card1/Body/Effect"));
            Assert.AreEqual("LASERS · MISSILES", Text("Cards/Card1/Body/Landing"));
            Assert.AreEqual("MOD 02", Text("Cards/Card2/Title"));
            Assert.AreEqual("+1 round", Text("Cards/Card2/Body/Effect"));
            Assert.AreEqual("MISSILES", Text("Cards/Card2/Body/Landing"));
            Assert.AreEqual("Orbitron-Medium", screen.transform.Find("Cards/Card1/Body/Name").GetComponent<Text>().font.name);
            Assert.AreEqual("Rajdhani-Medium", screen.transform.Find("Cards/Card1/Title").GetComponent<Text>().font.name);
            Assert.IsNull(screen.transform.Find("Cards/Card3"), "two offers build two cards");
            Assert.IsNull(screen.transform.Find("Cards/Card1/Body/LandingIcon"), "no landing glyph, no icon");
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
            screen = LullPickScreen.Create(root.transform, offers, _ => "Lasers", _ => null, null);
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
            screen = LullPickScreen.Create(root.transform, offers, _ => "Lasers", _ => null, null);
            yield return null;

            var card = screen.transform.Find("Cards/Card2").GetComponent<Button>();
            Assert.IsNotNull(card, "each card is a button");
            Assert.IsTrue(card.targetGraphic.raycastTarget, "the card's overlay takes the click");
            Assert.AreEqual(Navigation.Mode.None, card.navigation.mode, "no gamepad focus to submit a card by accident");
            card.onClick.Invoke();
            Assert.AreEqual(1, screen.Choice, "clicking the second card picks it");

            Object.Destroy(screen.gameObject);
            screen = LullPickScreen.Create(root.transform, offers, _ => "Lasers", _ => null, null);
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
                _ => "Lasers", _ => null, null);
            yield return null;

            screen.Choose(LullPickScreen.Skip);
            Assert.AreEqual(LullPickScreen.Skip, screen.Choice);
        }

        [UnityTest]
        public IEnumerator AnOpeningWindow_KeepsItsCornersCut_AsDeepAsItsOpenHeightAllows()
        {
            screen = LullPickScreen.Create(root.transform, new List<WeaponMod> { Mod("a", WeaponModField.Damage, 0.25f) },
                _ => "Lasers", _ => null, null);
            yield return null;
            var card = (RectTransform)screen.transform.Find("Cards/Card1");
            var window = card.GetComponent<ScreenWindow>();
            var clip = card.GetComponent<RectMask2D>();
            var frame = card.Find("Frame").GetComponent<ChamferedShape>();
            var back = card.Find("Back").GetComponent<ChamferedShape>();
            var bar = card.Find("Bar").GetComponent<ChamferedShape>();
            var slope = Mathf.Tan(40f * Mathf.Deg2Rad);

            foreach (var open in new[] { .1f, .26f, .4f, .59f, 1f })
            {
                window.Open = open;
                var shown = frame.rectTransform.rect;
                Assert.That(shown.height, Is.EqualTo(card.rect.height - clip.padding.y - clip.padding.w).Within(.01f),
                    $"at {open} the outline is the opening, not a clip of the full window");
                Assert.That(shown.width, Is.EqualTo(card.rect.width - clip.padding.x - clip.padding.z).Within(.01f));
                Assert.That(frame.DrawnChamfer, Is.GreaterThan(0f), $"at {open} the corners are cut");
                Assert.That(frame.DrawnChamfer, Is.EqualTo(Mathf.Min(frame.Chamfer, shown.height * .5f / slope))
                    .Within(.01f), $"at {open} the cuts on one side meet at most");
                Assert.AreEqual(shown, back.rectTransform.rect, "the body fills the outline");
                Assert.AreEqual(frame.DrawnChamfer, back.DrawnChamfer);
                Assert.That(bar.rectTransform.anchoredPosition, Is.EqualTo(frame.rectTransform.anchoredPosition),
                    "the bar fills down from the outline's top");
                Assert.That(bar.rectTransform.rect.width, Is.EqualTo(shown.width).Within(.01f));
                Assert.That(bar.rectTransform.rect.height, Is.LessThanOrEqualTo(shown.height));
            }

            window.Open = .26f;
            Assert.That(frame.DrawnChamfer, Is.LessThan(frame.Chamfer), "early in the unfold the cut is shallower");
            window.Open = .4f;
            Assert.AreEqual(frame.Chamfer, frame.DrawnChamfer, "a tall enough opening has the full cut");
        }

        [UnityTest]
        public IEnumerator Create_ShowsTheLandingGlyph_BesideTheLandingLine()
        {
            var glyph = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 4f, 4f), Vector2.one * .5f);
            var offers = new List<WeaponMod> { Mod("a", WeaponModField.Damage, 0.25f) };
            screen = LullPickScreen.Create(root.transform, offers, _ => "Lasers", _ => glyph, null);
            yield return null;

            var icon = screen.transform.Find("Cards/Card1/Body/LandingIcon");
            Assert.IsNotNull(icon, "a landing glyph shows");
            Assert.AreSame(glyph, icon.GetComponent<Image>().sprite);
            var landing = (RectTransform)screen.transform.Find("Cards/Card1/Body/LandingLabel");
            Assert.That(landing.anchoredPosition.x, Is.GreaterThan(((RectTransform)icon).anchoredPosition.x),
                "the landing line sits after the glyph");
            Object.Destroy(glyph);
        }

#if UNITY_EDITOR
        [UnityTest]
        public IEnumerator EveryPooledEffect_WrapsInsideItsCard()
        {
            var pool = UnityEditor.AssetDatabase.LoadAssetAtPath<LullModPool>(
                "Assets/Settings/Weapons/Mods/LullMods.asset");
            Assert.IsNotNull(pool, "the lull pool loads");
            foreach (var mod in pool.mods)
            {
                if (screen) Object.Destroy(screen.gameObject);
                screen = LullPickScreen.Create(root.transform, new List<LullMod> { mod }, _ => "Lasers", _ => null,
                    null);
                yield return null;

                var effect = screen.transform.Find("Cards/Card1/Body/Effect").GetComponent<Text>();
                Assert.AreEqual(HorizontalWrapMode.Wrap, effect.horizontalOverflow, $"{mod.DisplayName} wraps");
                Assert.That(effect.preferredHeight, Is.LessThanOrEqualTo(effect.rectTransform.rect.height),
                    $"{mod.DisplayName}'s effect fits above the rule");
            }
        }
#endif

        [Test]
        public void Create_RefusesAnEmptyOrOversizedOffer()
        {
            Assert.Throws<ArgumentException>(() =>
                LullPickScreen.Create(root.transform, new List<WeaponMod>(), _ => "", _ => null, null));
            var four = new List<WeaponMod>();
            for (var i = 0; i < 4; i++) four.Add(Mod($"m{i}", WeaponModField.Damage, 0.25f));
            Assert.Throws<ArgumentException>(() =>
                LullPickScreen.Create(root.transform, four, _ => "", _ => null, null));
        }
    }
}
