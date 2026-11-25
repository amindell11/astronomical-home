#if UNITY_EDITOR
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Tests.EditMode.Rendering
{
    [Category("Camera")]
    public sealed class HangarPostEditModeTests
    {
        private const string HangarCamPath = "Assets/Prefabs/Cameras/Hangar Camera.prefab";
        private const string MainCamPath = "Assets/Prefabs/Cameras/Main Camera.prefab";
        private const string ProfilePath = "Assets/Settings/Rendering/HangarOverride.asset";

        [Test]
        public void HangarCamera_ZerosMotionBlurAndPaniniOnItsOwnLayer()
        {
            var hangarPost = LayerMask.NameToLayer("HangarPost");
            var postprocess = LayerMask.NameToLayer("Postprocess");
            Assert.That(hangarPost, Is.GreaterThanOrEqualTo(17));
            Assert.That(postprocess, Is.GreaterThanOrEqualTo(0));

            var root = AssetDatabase.LoadAssetAtPath<GameObject>(HangarCamPath);
            var cameraData = root.GetComponent<UniversalAdditionalCameraData>();
            Assert.That(cameraData.stopNaN, Is.False);
            Assert.That(cameraData.volumeLayerMask.value, Is.EqualTo((1 << postprocess) | (1 << hangarPost)));

            var child = root.transform.Find("Hangar Volume");
            Assert.That(child, Is.Not.Null);
            Assert.That(child.gameObject.layer, Is.EqualTo(hangarPost));

            var volume = child.GetComponent<Volume>();
            Assert.That(volume.isGlobal, Is.True);
            Assert.That(volume.priority, Is.EqualTo(20f));
            Assert.That(AssetDatabase.GetAssetPath(volume.sharedProfile), Is.EqualTo(ProfilePath));
            Assert.That(volume.sharedProfile.components, Has.Count.EqualTo(2));

            Assert.That(volume.sharedProfile.TryGet(out MotionBlur blur), Is.True);
            Assert.That(blur.active, Is.True);
            Assert.That(blur.intensity.overrideState, Is.True);
            Assert.That(blur.intensity.value, Is.EqualTo(0f));

            Assert.That(volume.sharedProfile.TryGet(out PaniniProjection panini), Is.True);
            Assert.That(panini.active, Is.True);
            Assert.That(panini.distance.overrideState, Is.True);
            Assert.That(panini.distance.value, Is.EqualTo(0f));
        }

        [Test]
        public void MainCamera_ExcludesTheHangarPostLayer()
        {
            var hangarPost = LayerMask.NameToLayer("HangarPost");
            Assert.That(hangarPost, Is.GreaterThanOrEqualTo(17));
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(MainCamPath);
            var cameraData = root.GetComponent<UniversalAdditionalCameraData>();
            Assert.That(cameraData.volumeLayerMask.value & (1 << hangarPost), Is.EqualTo(0));
        }
    }
}
#endif
