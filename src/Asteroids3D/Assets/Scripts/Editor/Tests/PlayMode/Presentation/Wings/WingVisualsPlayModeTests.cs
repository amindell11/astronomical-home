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
        private Transform fin;

        public override void SetUp()
        {
            base.SetUp();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Ships/Valis.prefab");
            rig = Object.Instantiate(prefab.GetComponentInChildren<ShipVisualRig>(true).gameObject);
            wings = rig.GetComponentInChildren<WingVisuals>(true);
            main = rig.GetComponentsInChildren<Transform>(true).Single(t => t.name == "10 upper swept wings right");
            fin = rig.GetComponentsInChildren<Transform>(true).Single(t => t.name == "30 forward lower fins right");
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
            AssertAngle(main, 1.547583f);
            AssertAngle(fin, 61.86656f);
        }

        [UnityTest]
        public IEnumerator ThrustCommands_ReachApprovedPoses_AndReverseSmoothly()
        {
            wings.Bind(new ShipView(rig.transform, null, () => command, null, false));
            var renderer = rig.GetComponentInChildren<SkinnedMeshRenderer>(true);
            var baked = new Mesh();
            try
            {
                renderer.BakeMesh(baked);
                var idle = baked.vertices;
                command = new PilotCommand { thrust = 1f };
                yield return new WaitForSeconds(.2f);
                Assert.That(Quaternion.Angle(Quaternion.identity, main.localRotation), Is.InRange(2f, 18f));
                yield return new WaitForSeconds(.4f);
                AssertAngle(main, 19.34479f);
                AssertAngle(fin, 123.73312f);
                renderer.BakeMesh(baked);
                var forward = baked.vertices;
                Assert.That(Vector3.Distance(idle[4034], forward[4034]), Is.GreaterThan(.01f));
                var weights = renderer.sharedMesh.boneWeights;
                var rest = renderer.sharedMesh.vertices;
                for (var i = 0; i < rest.Length; i++)
                    if (weights[i].boneIndex0 == 0)
                        Assert.That(Vector3.Distance(rest[i], forward[i]), Is.LessThan(.00001f));

                command.thrust = -1f;
                yield return new WaitForSeconds(.2f);
                Assert.That(Quaternion.Angle(Quaternion.identity, main.localRotation), Is.InRange(1f, 18f));
                yield return new WaitForSeconds(.4f);
                AssertAngle(main, 0f);
                AssertAngle(fin, 0f);
                renderer.BakeMesh(baked);
                var reverse = baked.vertices;
                for (var i = 0; i < rest.Length; i++)
                    Assert.That(Vector3.Distance(rest[i], reverse[i]), Is.LessThan(.00001f));

                command = new PilotCommand { strafe = 1f, yawTorque = 1f };
                yield return new WaitForSeconds(.6f);
                AssertAngle(main, 1.547583f);
                AssertAngle(fin, 61.86656f);
                Assert.That(renderer.sharedMaterials, Has.Length.EqualTo(8));
            }
            finally { Object.DestroyImmediate(baked); }
        }

        private static void AssertAngle(Transform bone, float degrees) =>
            Assert.That(Quaternion.Angle(Quaternion.identity, bone.localRotation), Is.EqualTo(degrees).Within(.04f));
    }
}

