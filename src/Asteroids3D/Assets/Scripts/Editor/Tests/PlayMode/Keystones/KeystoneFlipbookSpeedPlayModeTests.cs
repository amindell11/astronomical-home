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
        private const string SubsideMaterialPath =
            "Assets/Visuals/Vfx/Keystones/ValisOverdrive/ValisOverdrive_Subside.mat";

        private Ship valis;
        private KeystoneFlipbookFeel feel;
        private MeshRenderer sheet;
        private MeshRenderer endSheet;

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

        [UnityTest]
        public IEnumerator EndSheet_PicksUpTheLaggedTurn_OfTheLayerItFollows()
        {
            void Lagged(SerializedProperty layer) => layer.FindPropertyRelative("turnLag").floatValue = 0.12f;
            yield return GoLive(Lagged, Lagged);

            var body = valis.GetComponent<Rigidbody>();
            // Turn for half a second of physics, whatever the frame rate.
            for (var step = 0; step < 25; step++)
            {
                body.angularVelocity = GamePlane.Normal * 3f;
                yield return new WaitForFixedUpdate();
            }

            yield return null;

            var burnUp = sheet.transform.up;
            Assert.That(Vector3.Angle(burnUp, valis.transform.up), Is.GreaterThan(10f), "the burn trails the turn");

            valis.Keystone.ResetKeystone();
            body.angularVelocity = GamePlane.Normal * 3f;
            yield return null;

            Assert.That(endSheet.enabled, Is.True, "the end sheet draws once the live window ends");
            Assert.That(Vector3.Angle(endSheet.transform.up, burnUp), Is.LessThan(8f),
                "the end sheet carries on from the burn's trail");
        }

        private IEnumerator GoLive(Action<SerializedProperty> authorLayer, Action<SerializedProperty> authorEnd = null)
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
            using (var feelObject = new SerializedObject(feel))
            {
                feelObject.FindProperty("scale").floatValue = 1f;
                feelObject.FindProperty("speed").floatValue = 1f;
                feelObject.FindProperty("framesPerSecond").floatValue = 60f;
                var layers = feelObject.FindProperty("layers");
                layers.arraySize = authorEnd == null ? 1 : 2;
                var layer = layers.GetArrayElementAtIndex(0);
                AuthorSheet(layer, BurnMaterialPath, 9, 3, 27);
                layer.FindPropertyRelative("startsOn").enumValueIndex = (int)KeystoneFlipbookStart.Live;
                layer.FindPropertyRelative("endsWithLive").boolValue = true;
                layer.FindPropertyRelative("loops").boolValue = true;
                layer.FindPropertyRelative("loopFrom").intValue = 3;
                authorLayer(layer);
                if (authorEnd != null)
                {
                    var end = layers.GetArrayElementAtIndex(1);
                    AuthorSheet(end, SubsideMaterialPath, 5, 1, 5);
                    end.FindPropertyRelative("startsOn").enumValueIndex = (int)KeystoneFlipbookStart.LiveEnd;
                    authorEnd(end);
                }

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

            sheet = endSheet = null;
            var sheets = 0;
            foreach (var renderer in valis.Keystone.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (renderer.name != "Flipbook sheet") continue;
                sheets++;
                if (renderer.sharedMaterial.name == "ValisOverdrive_Burn") sheet = renderer;
                else endSheet = renderer;
            }

            Assert.That(sheets, Is.EqualTo(authorEnd == null ? 1 : 2));
        }

        private static void AuthorSheet(SerializedProperty layer, string materialPath, int columns, int rows, int frames)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            Assert.That(material, Is.Not.Null, materialPath);
            layer.FindPropertyRelative("material").objectReferenceValue = material;
            layer.FindPropertyRelative("columns").intValue = columns;
            layer.FindPropertyRelative("rows").intValue = rows;
            layer.FindPropertyRelative("frames").intValue = frames;
            layer.FindPropertyRelative("show").boolValue = true;
            layer.FindPropertyRelative("size").floatValue = 1f;
            layer.FindPropertyRelative("opacity").floatValue = 1f;
            layer.FindPropertyRelative("speed").floatValue = 0.25f;
            layer.FindPropertyRelative("width").floatValue = 0.5f;
            layer.FindPropertyRelative("length").floatValue = 2f;
            layer.FindPropertyRelative("centreAhead").floatValue = -1f;
            layer.FindPropertyRelative("anchorAhead").floatValue = -0.3f;
            layer.FindPropertyRelative("depth").floatValue = 0.4f;
        }
    }
}
