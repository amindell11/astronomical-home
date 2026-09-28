#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Asteroids;
using Asteroids.Fields;
using Asteroids.Spawning;
using Capture;
using NUnit.Framework;
using Ships;
using Ships.Command;
using Substrate;
using Substrate.Sectors;
using Substrate.Sessions;
using Tests.PlayMode.Common;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Tests.PlayMode.Rendering.AsteroidField
{
    [Category("Camera"), Category("RequiresGraphics")]
    public sealed class DrawnFieldPlayModeTests : PlayModeWorldFixture
    {
        private Session session;
        private GameObject root;

        [UnityTearDown]
        public IEnumerator UnloadStudy()
        {
            try
            {
                if (session != null) yield return session.Teardown();
            }
            finally
            {
                session = null;
                Object.DestroyImmediate(root);
            }
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator TenShapes_RenderInMovingGameField()
        {
            var output = Path.GetFullPath(Path.Combine(Application.dataPath,
                "../../../results/asteroid-field-study/" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")));
            Directory.CreateDirectory(output);
            TestContext.Progress.WriteLine("ASTEROID_FIELD_STUDY=" + output);
            var quality = QualitySettings.GetQualityLevel();
            var priorSun = RenderSettings.sun;
            var priorTarget = RenderTexture.active;
            root = new GameObject("Drawn field study");
            var resources = new List<Object>();
            var lights = new List<Light>();
            var assets = new DrawnFieldStudyAssets();
            session = TestSession.Create(root, new SessionProfile
            {
                sectorEntry = new SectorEntry
                {
                    prefab = DrawnFieldStudyAssets.Load<Sector>("Assets/Scripts/Editor/Tests/PlayMode/Scenarios/Drawn/DrawnComparisonSector.prefab"),
                    config = DrawnFieldStudyAssets.Load<SectorSettings>("Assets/Settings/Game/DefaultSectorConfig.asset")
                },
                presentation = true
            });
            try
            {
                QualitySettings.SetQualityLevel(Array.IndexOf(QualitySettings.names, "High Fidelity"), true);
                yield return session.Compose();
                yield return session.LoadSector();
                foreach (var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                    if (light.enabled && light.type == LightType.Directional) { lights.Add(light); light.enabled = false; }
                var key = new GameObject("Study key", typeof(Light)).GetComponent<Light>();
                key.transform.SetParent(root.transform, false);
                key.type = LightType.Directional;
                key.intensity = 1;
                key.shadows = LightShadows.Soft;
                key.shadowBias = .025f;
                key.shadowNormalBias = .1f;
                key.transform.rotation = Quaternion.Euler(20, -65, 0);
                RenderSettings.sun = key;
                var camera = new GameObject("Game-plane camera", typeof(Camera)).GetComponent<Camera>();
                camera.transform.SetParent(root.transform, false);
                camera.enabled = false;
                camera.orthographic = true;
                camera.allowHDR = false;
                camera.allowMSAA = false;
                camera.clearFlags = CameraClearFlags.Skybox;
                camera.cullingMask &= ~LayerMask.GetMask("Minimap", "Minimap_Ship", "Minimap_Enemy", "ShipPreview");
                var target = new RenderTexture(1600, 900, 24);
                var image = new Texture2D(1600, 900, TextureFormat.RGB24, false);
                resources.Add(target); resources.Add(image);
                camera.targetTexture = target;
                var config = new CaptureConfig { width = 1600, height = 900, minHalfHeight = 26, padding = 0 };
                Color32[] Read(string name)
                {
                    camera.Render();
                    RenderTexture.active = target;
                    image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                    image.Apply();
                    File.WriteAllBytes(Path.Combine(output, name + ".png"), image.EncodeToPNG());
                    return image.GetPixels32();
                }

                var settings = DrawnFieldStudyAssets.Load<AsteroidSpawnSettings>("Assets/Settings/Asteroids/SpawnSettings.asset");
                var inspection = new GameObject("Ten shapes");
                inspection.transform.SetParent(root.transform, false);
                var alignment = new StringBuilder("shape,sourceX,sourceY,sourceZ,drawnX,drawnY,drawnZ\n");
                for (var i = 0; i < 10; i++)
                {
                    var originalMesh = settings.meshInfos[i].mesh;
                    using var sourceData = MeshUtility.AcquireReadOnlyMeshData(originalMesh);
                    using var vertices = new NativeArray<Vector3>(sourceData[0].vertexCount, Allocator.Temp);
                    sourceData[0].GetVertices(vertices);
                    var converted = assets.Rocks[i].vertices;
                    var worst = 0f;
                    for (var vertex = 0; vertex < vertices.Length; vertex += 13)
                    {
                        var nearest = float.MaxValue;
                        foreach (var point in converted) nearest = Mathf.Min(nearest, (point - vertices[vertex]).sqrMagnitude);
                        worst = Mathf.Max(worst, Mathf.Sqrt(nearest));
                    }
                    Assert.That(worst, Is.LessThan(originalMesh.bounds.size.magnitude * .025f),
                        $"Shape {i + 1} must preserve source coordinates and silhouette.");
                    var original = originalMesh.bounds.size;
                    var drawn = assets.Rocks[i].bounds.size;
                    alignment.AppendLine(FormattableString.Invariant($"{i+1},{original.x},{original.y},{original.z},{drawn.x},{drawn.y},{drawn.z}"));
                    var specimen = assets.Create(i, inspection.transform);
                    specimen.transform.position = GamePlane.PlanePointToWorld(new Vector2((i % 5 - 2) * 10, i < 5 ? 5 : -5));
                    specimen.transform.localScale = Vector3.one * (7 / Mathf.Max(drawn.x, drawn.y, drawn.z));
                    specimen.transform.rotation = Quaternion.Euler(20, 15, -12);
                }
                File.WriteAllText(Path.Combine(output, "alignment.csv"), alignment.ToString());
                CaptureFraming.Apply(camera, config, new[] { Vector2.zero });
                config.minHalfHeight = 15;
                CaptureFraming.Apply(camera, config, new[] { Vector2.zero });
                Read("all-ten-front");
                foreach (Transform specimen in inspection.transform) specimen.Rotate(0, 140, 0, Space.Self);
                Read("all-ten-reverse");
                inspection.SetActive(false);

                var template = DrawnFieldStudyAssets.Load<Ship>("Assets/Prefabs/Ships/Ship_1.prefab");
                var ship = session.Units.SpawnShip(template, null, 0, Vector3.zero, GamePlane.Rotation, null);
                var fieldHolder = new GameObject("Study field");
                fieldHolder.transform.SetParent(root.transform, false);
                fieldHolder.SetActive(false);
                var field = Object.Instantiate(DrawnFieldStudyAssets.Load<UpdatingAsteroidField>(
                    "Assets/Prefabs/Asteroid/HarnessAsteroidField.prefab"), fieldHolder.transform);
                var fieldSettings = Object.Instantiate(DrawnFieldStudyAssets.Load<AsteroidFieldSettings>("Assets/Settings/Asteroids/BigFieldSettings.asset"));
                resources.Add(fieldSettings);
                fieldSettings.loadRadius = 65;
                fieldSettings.fieldRadius = 100;
                fieldSettings.maxSpawnsPerFrame = 60;
                var serialized = new SerializedObject(field);
                serialized.FindProperty("settings").objectReferenceValue = fieldSettings;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                field.SetLayoutSeed(685);
                field.SetAnchor(ship.transform);
                field.SetStartPoint(Vector2.zero);
                field.SetPresentation(true);
                fieldHolder.SetActive(true);
                for (var frame = 0; frame < 30; frame++) yield return null;
                var styled = new Dictionary<AsteroidController, int>();
                var counts = new int[10];
                void StyleField()
                {
                    foreach (var rock in field.GetComponentsInChildren<AsteroidController>())
                    {
                        if (styled.TryGetValue(rock, out var epoch) && epoch == rock.SpawnEpoch) continue;
                        if (styled.ContainsKey(rock))
                        {
                            Object.DestroyImmediate(rock.transform.Find("Crease drawing").gameObject);
                            Object.DestroyImmediate(rock.transform.Find("Outer contour").gameObject);
                        }
                        assets.Apply(rock);
                        styled[rock] = rock.SpawnEpoch;
                        counts[rock.MeshIndex]++;
                    }
                }
                var spawner = field.GetComponent<AsteroidSpawner>();
                var attributes = new AsteroidAttributes(settings.meshInfos[0], 0,
                    settings.meshInfos[0].cachedVolume * settings.density, 1, Vector3.zero, Vector3.zero);
                var pose = new Pose(GamePlane.PlanePointToWorld(new Vector2(200, 200)), Quaternion.identity);
                var probe = spawner.Spawn(pose, attributes);
                StyleField();
                spawner.Despawn(probe);
                var recycled = spawner.Spawn(pose, attributes);
                Assert.That(recycled, Is.SameAs(probe), "The pool probe must exercise reuse of the same instance.");
                StyleField();
                Assert.That(recycled.CurrentMesh, Is.SameAs(assets.Rocks[0]), "Same-shape reuse must restore the drawn surface.");
                var drawingLayers = 0;
                foreach (Transform child in recycled.transform)
                    if (child.name == "Crease drawing" || child.name == "Outer contour") drawingLayers++;
                Assert.That(drawingLayers, Is.EqualTo(2), "Reuse must not duplicate drawing layers.");
                spawner.Despawn(recycled);
                Object.DestroyImmediate(recycled.transform.Find("Crease drawing").gameObject);
                Object.DestroyImmediate(recycled.transform.Find("Outer contour").gameObject);
                styled.Remove(recycled);
                counts[0] -= 2;
                Assert.That(styled.Count, Is.GreaterThan(60));
                for (var i = 0; i < 10; i++) Assert.That(counts[i], Is.GreaterThan(0), "Every source shape must occur in the field.");
                config.minHalfHeight = 34;
                CaptureFraming.Apply(camera, config, new[] { ship.Kinematics.pos + Vector2.up * 14 });
                var overview = Read("field-overview");
                var variation = 0;
                for (var i = 1; i < overview.Length; i++)
                    if (Math.Abs(overview[i].r - overview[i - 1].r) + Math.Abs(overview[i].g - overview[i - 1].g) > 40) variation++;
                Assert.That(variation, Is.GreaterThan(10000), "Field render must contain visible surface detail.");
                key.enabled = false;
                foreach (var light in lights) light.enabled = true;
                RenderSettings.sun = priorSun;
                Read("field-scene-lights");
                foreach (var light in lights) light.enabled = false;
                key.enabled = true; RenderSettings.sun = key;
                config.minHalfHeight = 20;
                var initial = ship.Kinematics.pos;
                var initialInstances = styled.Count;
                var trace = new StringBuilder("frame,x,y,bank,health,styled\n");
                using (CapturePacing.Locked(2))
                    for (var frame = 0; frame < 240; frame++)
                    {
                        ship.Movement.Drive(new PilotCommand { thrust = frame < 160 ? .25f : .04f, strafe = frame > 80 && frame < 130 ? .08f : 0 });
                        yield return new WaitForFixedUpdate();
                        yield return new WaitForFixedUpdate();
                        StyleField();
                        CaptureFraming.Apply(camera, config, new[] { ship.Kinematics.pos + Vector2.up * 6 });
                        Read("f_" + frame.ToString("D5"));
                        trace.AppendLine(FormattableString.Invariant($"{frame},{ship.Kinematics.pos.x},{ship.Kinematics.pos.y},{ship.Kinematics.bank},{ship.HealthPct},{styled.Count}"));
                    }
                Assert.That(Vector2.Distance(initial, ship.Kinematics.pos), Is.GreaterThan(5), "Flight must move the real ship.");
                File.WriteAllText(Path.Combine(output, "flight.csv"), trace.ToString());
                File.WriteAllText(Path.Combine(output, "manifest.json"), "{\"width\":1600,\"height\":900,\"suggestedFps\":25,\"steps\":240}");
                File.WriteAllText(Path.Combine(output, "shapes.json"), "{\"initialInstances\":" + initialInstances + ",\"shapeCounts\":[" + string.Join(",", counts) + "]}");
            }
            finally
            {
                RenderTexture.active = priorTarget;
                RenderSettings.sun = priorSun;
                foreach (var light in lights) if (light) light.enabled = true;
                root.SetActive(false);
                assets.Dispose();
                foreach (var resource in resources) Object.DestroyImmediate(resource);
                QualitySettings.SetQualityLevel(quality, true);
            }
        }
    }
}
#endif
