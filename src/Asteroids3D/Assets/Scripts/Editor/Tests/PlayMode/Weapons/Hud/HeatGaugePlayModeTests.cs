#if UNITY_EDITOR
using System.Collections;
using Combat.Weapons.Conditions;
using NUnit.Framework;
using UI.Weapons;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Tests.PlayMode.Weapons.Hud
{
    [Category("UI")]
    public sealed class HeatGaugePlayModeTests
    {
        [UnityTest]
        public IEnumerator LockoutPulsesCoralThroughoutCooling_ThenRestoresNormalFill()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<HeatGaugeUI>("Assets/Prefabs/UI/Readouts/HeatGauge.prefab");
            var gauge = Object.Instantiate(prefab);
            var weapon = new GameObject("Heat source");
            try
            {
                var heat = weapon.AddComponent<Heat>();
                heat.enabled = false;
                heat.Configure(100f, 100f, 20f, 0f, 0f);
                var image = gauge.GetComponent<Image>();
                gauge.Initialize(heat);
                var normal = image.color;
                Assert.That(normal.r, Is.EqualTo(.1733818f).Within(.001f));
                Assert.That(normal.g, Is.EqualTo(1f));
                Assert.That(normal.b, Is.EqualTo(1f));
                heat.ProcessFire();
                heat.Tick(1f);
                Assert.That(heat.Overheated, Is.True);
                Assert.That(image.fillAmount, Is.EqualTo(.8f).Within(.001f));

                var minimum = 1f;
                var maximum = 0f;
                var until = Time.realtimeSinceStartup + .8f;
                while (Time.realtimeSinceStartup < until)
                {
                    yield return null;
                    var color = image.color;
                    Assert.That(color.g / color.r, Is.EqualTo(.41f / .98f).Within(.001f));
                    Assert.That(color.b / color.r, Is.EqualTo(.41f / .98f).Within(.001f));
                    Assert.That(color.a, Is.EqualTo(1f));
                    minimum = Mathf.Min(minimum, color.r);
                    maximum = Mathf.Max(maximum, color.r);
                }
                Assert.That(maximum - minimum, Is.GreaterThan(.2f));
                Assert.That(minimum, Is.GreaterThanOrEqualTo(.98f * .65f - .001f));
                Assert.That(maximum, Is.LessThanOrEqualTo(.98f + .001f));

                heat.Tick(5f);
                Assert.That(heat.Overheated, Is.False);
                Assert.That(image.fillAmount, Is.Zero);
                Assert.That(image.color, Is.EqualTo(normal));
                yield return null;
                Assert.That(image.color, Is.EqualTo(normal));
            }
            finally
            {
                Object.DestroyImmediate(gauge.gameObject);
                Object.DestroyImmediate(weapon);
            }
        }
    }
}
#endif
