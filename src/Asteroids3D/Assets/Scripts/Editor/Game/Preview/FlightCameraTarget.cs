using System.Collections.Generic;
using Cameras;
using UnityEditor;
using UnityEngine;

namespace Game.Preview
{
    internal sealed class FlightCameraTarget : TuningTarget
    {
        public override string Name => "Flight camera";
        public override IReadOnlyList<PreviewEntry> Entries { get; } = new[] { PreviewEntry.Combat };
        public override IEnumerable<ScriptableObject> Profiles(GameHost game) => new ScriptableObject[] { Feel(game) };

        // Read through serialization, so the camera grows no member for the preview.
        private static FlightCameraProfile Feel(GameHost game)
        {
            using var camera = new SerializedObject(game.observerCamPrefab);
            return (FlightCameraProfile)camera.FindProperty("feel").objectReferenceValue;
        }
    }
}
