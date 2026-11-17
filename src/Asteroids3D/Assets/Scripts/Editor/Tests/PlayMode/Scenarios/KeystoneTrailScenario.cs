#if UNITY_EDITOR
using System.Collections;
using Capture.Recording;
using Capture.Views;
using NUnit.Framework;
using Ships;
using Ships.Command;
using Substrate;
using Substrate.Sectors;
using Substrate.Services.Locales;
using Substrate.Sessions;
using Tests.PlayMode.Common;
using Tests.PlayMode.Recording;
using UnityEditor;
using UnityEngine;

namespace Tests.PlayMode.Scenarios
{
    /// <summary>Vanguard and Bulwark fly up TuningSector side by side and each fires its keystone twice, filmed with presentation on and no gizmos: the review clip for the keystone trails.</summary>
    public sealed class KeystoneTrailScenario : CaptureScenario
    {
        private const float SimSeconds = 8.5f;
        private const string SectorPrefabPath = "Assets/Prefabs/Sectors/TuningSector.prefab";
        private const string LocalePath = "Assets/Prefabs/Locales/IllustratedBlue.prefab";
        private static readonly (string path, float x, float[] fireAt)[] Pilots =
        {
            ("Assets/Prefabs/Ships/Vanguard.prefab", -3.5f, new[] { 1.2f, 6.4f }),
            ("Assets/Prefabs/Ships/Bulwark.prefab", 3.5f, new[] { 1.2f, 7f }),
        };

        public override GizmoCaptureProfile Profile => GizmoCaptureProfile.None;

        protected override CaptureRequest Request => new("results/capture", GetType().Name, Commit, 1920, 1080);

        protected override DiagnosticViewConfig View => new() { minHalfHeight = 22f, padding = 5f };

        public override SectorEntry SectorEntry
        {
            get
            {
                var prefab = AssetDatabase.LoadAssetAtPath<Sector>(SectorPrefabPath);
                Assert.IsNotNull(prefab, $"Sector prefab missing at {SectorPrefabPath}");
                var locale = AssetDatabase.LoadAssetAtPath<LocaleSky>(LocalePath);
                Assert.IsNotNull(locale, $"Sector locale missing at {LocalePath}");
                return new SectorEntry { prefab = prefab, locale = locale };
            }
        }

        public override IEnumerator Run()
        {
            var ships = new Ship[Pilots.Length];
            for (var i = 0; i < Pilots.Length; i++)
            {
                var template = TestAssets.Load<Ship>(Pilots[i].path);
                Assert.IsNotNull(template, $"Ship prefab missing at {Pilots[i].path}");
                ships[i] = Session.Units.SpawnShip(template, null, i, Session.Frame.Place(new Vector2(Pilots[i].x, -10f)),
                    GamePlane.Rotation, Session.ActiveSector ? Session.ActiveSector.ObstacleField : null);
            }
            yield return Film(ships);

            var steps = Mathf.CeilToInt(SimSeconds / Time.fixedDeltaTime);
            for (var step = 0; step < steps; step++)
            {
                var t = step * Time.fixedDeltaTime;
                for (var i = 0; i < ships.Length; i++)
                {
                    if (!ships[i]) continue;
                    var fire = false;
                    foreach (var at in Pilots[i].fireAt) fire |= t >= at && t < at + Time.fixedDeltaTime;
                    ships[i].Movement.Drive(new PilotCommand
                    {
                        thrust = 0.7f,
                        yawTorque = 0.35f * Mathf.Sin(t * 1.3f + i),
                        boost = fire ? 1f : 0f,
                    });
                }
                yield return new WaitForFixedUpdate();
            }
        }
    }
}
#endif
