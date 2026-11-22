using System;
using System.Collections.Generic;
using Cameras;
using Cameras.Shake;
using UnityEditor;
using UnityEngine;

namespace Game.Preview.Targets
{
    internal sealed class FlightCameraTarget : TuningTarget
    {
        public override string Name => "Flight camera";
        public override IReadOnlyList<PreviewEntry> Entries { get; } = new[] { PreviewEntry.Combat };

        public override IEnumerable<ScriptableObject> Profiles(GameHost game)
        {
            yield return Feel(game);
            foreach (var profile in ShakeProfiles())
                yield return profile;
        }

        // Read through serialization, so the camera grows no member for the preview.
        private static FlightCameraProfile Feel(GameHost game)
        {
            using var camera = new SerializedObject(game.observerCamPrefab);
            return (FlightCameraProfile)camera.FindProperty("feel").objectReferenceValue;
        }

        private static IEnumerable<CameraShakeProfile> ShakeProfiles()
        {
            var guids = AssetDatabase.FindAssets("t:CameraShakeProfile");
            var paths = new string[guids.Length];
            for (var i = 0; i < guids.Length; i++)
                paths[i] = AssetDatabase.GUIDToAssetPath(guids[i]);
            Array.Sort(paths, StringComparer.Ordinal);
            foreach (var path in paths)
                yield return AssetDatabase.LoadAssetAtPath<CameraShakeProfile>(path);
        }
    }
}
