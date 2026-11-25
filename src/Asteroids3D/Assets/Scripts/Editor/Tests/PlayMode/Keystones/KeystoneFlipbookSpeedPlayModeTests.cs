using System;
using System.Collections;
using NUnit.Framework;
using Ships;
using Ships.Command;
using Ships.Keystones.Tells;
using Substrate;
using Tests.PlayMode.Common;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Tests.PlayMode.Keystones
{
    [Category("Ships")]
    public class KeystoneFlipbookSpeedPlayModeTests : PlayModeWorldFixture
    {
        private const string ValisPath = "Assets/Prefabs/Ships/Valis.prefab";
        private const string BurnMaterialPath =
            "Assets/Visuals/Vfx/Keystones/ValisOverdrive/ValisOverdrive_Burn.mat";

        private Ship valis;
        private KeystoneFlipbookFeel feel;
        private MeshRenderer sheet;

        public override void TearDown()
        {
            ShipTestFactory.DestroyShip(valis);
            if (feel) Object.DestroyImmediate(feel);
            base.TearDown();
        }

        [UnityTest]
        public IEnumerator FollowsTravel_PointsAlongVelocity_WhileTheHeadingIsSideways()
        {
            yield return GoLive(layer => layer.FindPropertyRelative("followsTravel").boolValue = true);

            var body = valis.GetComponent<Rigidbody>();
            for (var frame = 0; frame < 2; frame++)
            {
                body.linearVelocity = valis.transform.right * 20f;
                yield return null;
            }

            Assert.That(Vector3.Angle(sheet.transform.up, valis.transform.right), Is.LessThan(1f));
            Assert.That(Vector3.Angle(sheet.transform.up, valis.transform.up), Is.EqualTo(90f).Within(1f));
        }

        [UnityTest]
        public IEnumerator LengthAtRest_IsShorterAtRestThanAtSpeed()
        {
            yield return GoLive(layer =>
            {
                layer.FindPropertyRelative("lengthAtRest").floatValue = 0.4f;
                layer.FindPropertyRelative("lengthFullAtSpeed").floatValue = 40f;
            });

            var body = valis.GetComponent<Rigidbody>();
            for (var frame = 0; frame < 3; frame++)
            {
                body.linearVelocity = Vector3.zero;
                yield return null;
            }

            var rest = sheet.transform.lossyScale.y;
            for (var frame = 0; frame < 3; frame++)
            {
                body.linearVelocity = valis.transform.up * 48f;
                yield return null;
            }

            var fast = sheet.transform.lossyScale.y;
            Assert.That(rest / fast, Is.EqualTo(0.4f).Within(0.02f));
        }

        private IEnumerator GoLive(Action<SerializedProperty> authorLayer)
        {
            valis = Object.Instantiate(TestAssets.Load<Ship>(ValisPath), Vector3.zero, GamePlane.Rotation);
            valis.Initialize(0, 0, Projectiles);

            Object.DestroyImmediate(valis.Keystone.GetComponent<KeystoneFlipbook>());
            var root = valis.Keystone.transform;
            for (var i = root.childCount - 1; i >= 0; i--)
            {
                var child = root.GetChild(i);
                if (child.name == "Flipbook sheet")
                    Object.DestroyImmediate(child.gameObject);
            }

            valis.Keystone.gameObject.SetActive(false);
            var flipbook = valis.Keystone.gameObject.AddComponent<KeystoneFlipbook>();
            feel = ScriptableObject.CreateInstance<KeystoneFlipbookFeel>();
            var material = AssetDatabase.LoadAssetAtPath<Material>(BurnMaterialPath);
            Assert.That(material, Is.Not.Null, "a sheet material to build the layer");

            using (var feelObject = new SerializedObject(feel))
            {
                feelObject.FindProperty("scale").floatValue = 1f;
                feelObject.FindProperty("speed").floatValue = 1f;
                feelObject.FindProperty("framesPerSecond").floatValue = 60f;
                var layers = feelObject.FindProperty("layers");
                layers.arraySize = 1;
                var layer = layers.GetArrayElementAtIndex(0);
                layer.FindPropertyRelative("material").objectReferenceValue = material;
                layer.FindPropertyRelative("columns").intValue = 9;
                layer.FindPropertyRelative("rows").intValue = 3;
                layer.FindPropertyRelative("frames").intValue = 27;
                layer.FindPropertyRelative("show").boolValue = true;
                layer.FindPropertyRelative("size").floatValue = 1f;
                layer.FindPropertyRelative("opacity").floatValue = 1f;
                layer.FindPropertyRelative("speed").floatValue = 0.25f;
                layer.FindPropertyRelative("width").floatValue = 0.5f;
                layer.FindPropertyRelative("length").floatValue = 2f;
                layer.FindPropertyRelative("centreAhead").floatValue = -1f;
                layer.FindPropertyRelative("anchorAhead").floatValue = -0.3f;
                layer.FindPropertyRelative("depth").floatValue = 0.4f;
                layer.FindPropertyRelative("startsOn").enumValueIndex = (int)KeystoneFlipbookStart.Live;
                layer.FindPropertyRelative("endsWithLive").boolValue = true;
                layer.FindPropertyRelative("loops").boolValue = true;
                layer.FindPropertyRelative("loopFrom").intValue = 3;
                authorLayer(layer);
                feelObject.ApplyModifiedPropertiesWithoutUndo();
            }

            using (var part = new SerializedObject(flipbook))
            {
                part.FindProperty("keystone").objectReferenceValue = valis.Keystone;
                part.FindProperty("shipLength").floatValue = 2.71f;
                part.FindProperty("feel").objectReferenceValue = feel;
                part.ApplyModifiedPropertiesWithoutUndo();
            }

            valis.Keystone.gameObject.SetActive(true);
            // MovementController.Start resets the command, so press after it has run.
            yield return null;
            valis.Movement.Drive(new PilotCommand { boost = 1f });
            yield return new WaitForFixedUpdate();
            valis.Movement.Drive(default);
            Assert.That(valis.Keystone.Active, Is.True);

            sheet = null;
            var sheets = 0;
            foreach (var renderer in valis.Keystone.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (renderer.name != "Flipbook sheet") continue;
                sheets++;
                sheet = renderer;
            }

            Assert.That(sheets, Is.EqualTo(1));
        }
    }
}
