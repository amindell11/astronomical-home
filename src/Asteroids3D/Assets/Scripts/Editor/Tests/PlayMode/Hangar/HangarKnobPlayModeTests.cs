#if UNITY_EDITOR
using NUnit.Framework;
using UI.Hangar.Hardware;
using UnityEngine;

namespace Tests.PlayMode.Hangar
{
    [Category("UI")]
    public sealed class HangarKnobPlayModeTests
    {
        private static readonly float[] FrameSeconds = { .016f, .05f, .1f };

        private GameObject knobObject;

        [TearDown]
        public void TearDown()
        {
            if (knobObject) Object.Destroy(knobObject);
        }

        [Test]
        public void TheTurnStaysBoundedAndSettles_AtAnyFrameTime([ValueSource(nameof(FrameSeconds))] float dt)
        {
            knobObject = new GameObject("Knob");
            var knob = knobObject.AddComponent<HangarKnob>();
            knob.enabled = false;
            knob.Initialize(Vector3.up);
            knob.Snap(0f);
            knob.Turn(40f);

            var worst = 0f;
            for (var t = 0f; t < 2f; t += dt)
            {
                knob.Step(dt);
                worst = Mathf.Max(worst, Mathf.Abs(knob.Angle - 40f));
            }
            Assert.That(worst, Is.LessThan(40f), $"the knob never swings past where it started at {dt * 1000f:0} ms frames");
            Assert.That(knob.Angle, Is.EqualTo(40f).Within(.5f), $"the knob settles on the detent at {dt * 1000f:0} ms frames");
            Assert.IsTrue(knob.Settled, $"the knob reports settled at {dt * 1000f:0} ms frames");
        }
    }
}
#endif
