using System.Collections.Generic;
using Cameras;
using Cameras.Shake;
using Combat.Weapons;
using Ships.Keystones;
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
            foreach (var profile in ShakeProfiles(game))
                yield return profile;
        }

        // Read through serialization, so the camera grows no member for the preview.
        private static FlightCameraProfile Feel(GameHost game)
        {
            using var camera = new SerializedObject(game.observerCamPrefab);
            return (FlightCameraProfile)camera.FindProperty("feel").objectReferenceValue;
        }

        private static IEnumerable<CameraShakeProfile> ShakeProfiles(GameHost game)
        {
            var seen = new HashSet<CameraShakeProfile>();
            foreach (var ship in game.hangarOffer.ships)
            {
                var profile = Read(ship.KeystonePrefab.GetComponentInChildren<KeystoneShake>(true), seen);
                if (profile)
                    yield return profile;
            }

            foreach (var weapon in game.hangarOffer.weapons)
            {
                var profile = Read(weapon.GetComponentInChildren<WeaponShake>(true), seen);
                if (profile)
                    yield return profile;
            }
        }

        private static CameraShakeProfile Read(Component part, HashSet<CameraShakeProfile> seen)
        {
            if (!part)
                return null;
            using var serialized = new SerializedObject(part);
            var profile = serialized.FindProperty("profile").objectReferenceValue as CameraShakeProfile;
            if (!profile || !seen.Add(profile))
                return null;
            return profile;
        }
    }
}
