using System.Collections;
using System.Collections.Generic;
using Combat.Weapons.Mods;
using Loot;
using NUnit.Framework;
using Ships.Mods;
using UI.PlayerState;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif
using Object = UnityEngine.Object;

namespace Tests.PlayMode.PlayerState
{
    [Category("UI")]
    public class HeldModsReadoutPlayModeTests
    {
        private GameObject root;
        private HeldModsReadout readout;
        private readonly List<Object> made = new();

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Held mods readout");
            var canvas = new GameObject("HUD", typeof(RectTransform)).AddComponent<Canvas>();
            canvas.transform.SetParent(root.transform, false);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            readout = HeldModsReadout.Create(canvas.transform,
                Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"), null, null, () => true);
        }

        [TearDown]
        public void TearDown()
        {
            if (root) Object.DestroyImmediate(root);
            foreach (var item in made)
                if (item) Object.DestroyImmediate(item);
            made.Clear();
        }

        [UnityTest]
        public IEnumerator PickingAMod_AddsItsBadge_AndASecondCopyShowsTimesTwo()
        {
            var stack = new LullModStack();
            readout.Initialize(stack);
            Assert.AreEqual(0, ActiveCount(readout.transform), "an empty stack shows no badges");

            var hot = Weapon("Hot rounds");
            stack.Add(hot);
            var badge = Active(readout.transform, "Hot rounds");
            Assert.AreEqual(1, ActiveCount(readout.transform));
            Assert.AreEqual("HR", badge.transform.Find("Initials").GetComponent<Text>().text);
            Assert.IsNull(badge.transform.Find("Count"));

            stack.Add(hot);
            badge = Active(readout.transform, "Hot rounds");
            Assert.AreEqual(1, ActiveCount(readout.transform), "a second copy stays one badge");
            Assert.AreEqual("×2", badge.transform.Find("Count").GetComponent<Text>().text);

            stack.Add(Ship("Hull plating"));
            Assert.AreEqual(2, ActiveCount(readout.transform));
            Assert.IsNotNull(Active(readout.transform, "Hull plating"));
            yield return null;
        }

        [UnityTest]
        public IEnumerator AStackHeldBeforeBinding_ShowsAtOnce_AndClearEmptiesIt()
        {
            var stack = new LullModStack();
            stack.Add(Weapon("Hot rounds"));
            stack.Add(Ship("Hull plating"));
            readout.Initialize(stack);
            Assert.IsNotNull(Active(readout.transform, "Hot rounds"));
            Assert.IsNotNull(Active(readout.transform, "Hull plating"));

            stack.Clear();
            Assert.AreEqual(0, ActiveCount(readout.transform));
            yield return null;
        }

        [UnityTest]
        public IEnumerator ModArt_ReplacesTheInitials()
        {
#if UNITY_EDITOR
            var mod = Weapon("Coolant");
            var sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 4f, 4f), Vector2.zero);
            made.Add(sprite);
            var serialized = new SerializedObject(mod);
            serialized.FindProperty("icon").objectReferenceValue = sprite;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var stack = new LullModStack();
            stack.Add(mod);
            readout.Initialize(stack);

            var badge = Active(readout.transform, "Coolant");
            Assert.AreSame(sprite, badge.transform.Find("Icon").GetComponent<Image>().sprite);
            Assert.IsNull(badge.transform.Find("Initials"));
            yield return null;
#else
            Assert.Ignore("Assigning mod art uses the editor serializer.");
            yield break;
#endif
        }

        [Test]
        public void Initials_TakeTwoWordsOrTwoLetters()
        {
            Assert.AreEqual("HR", HeldModsReadout.Initials("Hot rounds"));
            Assert.AreEqual("CO", HeldModsReadout.Initials("Coolant"));
            Assert.AreEqual("GY", HeldModsReadout.Initials("Gyros"));
        }

        private WeaponMod Weapon(string name)
        {
            var mod = ScriptableObject.CreateInstance<WeaponMod>();
            mod.Configure(name, WeaponModField.Damage, 0.25f);
            made.Add(mod);
            return mod;
        }

        private ShipMod Ship(string name)
        {
            var mod = ScriptableObject.CreateInstance<ShipMod>();
            mod.Configure(name, ShipModField.Hull, 0.25f);
            made.Add(mod);
            return mod;
        }

        private static GameObject Active(Transform parent, string name)
        {
            for (var i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (child.gameObject.activeSelf && child.name == name) return child.gameObject;
            }

            return null;
        }

        private static int ActiveCount(Transform parent)
        {
            var count = 0;
            for (var i = 0; i < parent.childCount; i++)
                if (parent.GetChild(i).gameObject.activeSelf) count++;
            return count;
        }
    }
}
