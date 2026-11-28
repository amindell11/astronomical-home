using System.Collections;
using System.Linq;
using NUnit.Framework;
using Ships.Command;
using Ships.Presentation;
using Ships.Visuals.Wings;
using Tests.PlayMode.Common;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.PlayMode.Presentation.Wings
{
    [Category("Ships")]
    public sealed class WingVisualsPlayModeTests : PlayModeWorldFixture
    {
        private GameObject rig;
        private WingVisuals wings;
        private PilotCommand command;
        private Transform main;
        private Transform lower;

        public override void SetUp()
        {
            base.SetUp();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Ships/Valis.prefab");
            rig = Object.Instantiate(prefab.GetComponentInChildren<ShipVisualRig>(true).gameObject);
            wings = rig.GetComponentInChildren<WingVisuals>(true);
            main = rig.GetComponentsInChildren<Transform>(true).Single(t => t.name == "Upper wing right");
            lower = rig.GetComponentsInChildren<Transform>(true).Single(t => t.name == "Lower wing right");
            command = default;
        }

        public override void TearDown()
        {
            Object.DestroyImmediate(rig);
            base.TearDown();
        }

        [UnityTest]
        public IEnumerator UnboundHangarRig_RemainsAtAuthoredIdle()
        {
            yield return new WaitForSeconds(.6f);
            Assert.That(wings.enabled, Is.False);
            AssertAngle(main, 20f);
            AssertAngle(lower, 20f);
        }

        [UnityTest]
        public IEnumerator ThrustCommands_SweepCompleteWings_AndBrakeAtIdlePose()
        {
            wings.Bind(new ShipView(rig.transform, null, () => command, null, false));
            var surface = main.GetComponentInChildren<MeshFilter>(true);
            var hull = rig.GetComponentsInChildren<MeshFilter>(true).Single(f => f.name == "Hull");
            Vector3 Tip() => surface.transform.TransformPoint(surface.sharedMesh.bounds.max);
            Vector3 Body() => hull.transform.TransformPoint(hull.sharedMesh.bounds.max);
            var idle = Tip();
            var body = Body();
            command = new PilotCommand { thrust = 1f };
            yield return new WaitForSeconds(.2f);
            Assert.That(Quaternion.Angle(Quaternion.identity, main.localRotation), Is.InRange(2f, 18f));
            yield return new WaitForSeconds(.4f);
            AssertAngle(main, 0f);
            AssertAngle(lower, 0f);
            Assert.That(Vector3.Distance(idle, Tip()), Is.GreaterThan(.01f));
            Assert.That(Vector3.Distance(body, Body()), Is.LessThan(.00001f));

            command.thrust = -1f;
            yield return new WaitForSeconds(.2f);
            Assert.That(Quaternion.Angle(Quaternion.identity, main.localRotation), Is.InRange(1f, 18f));
            yield return new WaitForSeconds(.4f);
            AssertAngle(main, 20f);
            AssertAngle(lower, 20f);
            Assert.That(Vector3.Distance(idle, Tip()), Is.LessThan(.00001f));

            command = new PilotCommand { strafe = 1f, yawTorque = 1f };
            yield return new WaitForSeconds(.6f);
            AssertAngle(main, 20f);
            AssertAngle(lower, 20f);
            Assert.That(hull.GetComponent<Renderer>().sharedMaterials, Has.Length.EqualTo(8));
        }

        [UnityTest]
        public IEnumerator LiveKeystone_SweepsWingsPastTheThrustPose_AndReturnsWhenItEnds()
        {
            var serialized = new SerializedObject(wings);
            serialized.FindProperty("liveSweepDegrees").floatValue = 8f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var live = false;
            var rest = main.localRotation;
            wings.Bind(new ShipView(rig.transform, null, () => command, null, false, null, () => live));

            live = true;
            yield return new WaitForSeconds(.6f);
            AssertAngle(main, 8f);
            AssertAngle(lower, 8f);
            Assert.That(Quaternion.Angle(rest, main.localRotation), Is.EqualTo(28f).Within(.04f),
                "the sweep carries on past the thrust pose, away from rest");

            live = false;
            yield return new WaitForSeconds(.6f);
            AssertAngle(main, 20f);
            AssertAngle(lower, 20f);
        }

        private static void AssertAngle(Transform bone, float degrees) =>
            Assert.That(Quaternion.Angle(Quaternion.identity, bone.localRotation), Is.EqualTo(degrees).Within(.04f));
    }
}

