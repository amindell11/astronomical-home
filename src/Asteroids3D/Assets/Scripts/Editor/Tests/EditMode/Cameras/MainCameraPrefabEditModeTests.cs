using Cameras;
using NUnit.Framework;
using UnityEditor;

namespace Tests.EditMode.Cameras
{
    [Category("Camera")]
    public class MainCameraPrefabEditModeTests
    {
        [Test]
        public void ShippedCamera_LocksOnThePlayer_WithAFlightCameraProfile()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<ObserverCam>("Assets/Prefabs/Cameras/Main Camera.prefab");
            using var serialized = new SerializedObject(prefab);
            Assert.IsTrue(prefab.LockCameraToSubject, "the camera locks on the player");
            Assert.IsTrue(serialized.FindProperty("lockZoomToSubject").boolValue, "the zoom locks with it");
            Assert.IsInstanceOf<FlightCameraProfile>(serialized.FindProperty("feel").objectReferenceValue);
        }
    }
}
