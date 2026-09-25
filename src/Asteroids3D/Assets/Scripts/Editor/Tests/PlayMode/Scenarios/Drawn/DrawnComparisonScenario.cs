#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Asteroids;
using Asteroids.Spawning;
using Capture;
using Damage;
using NUnit.Framework;
using Ships;
using Ships.Command;
using Ships.Loadout;
using Ships.Presentation;
using Ships.Registry;
using Substrate;
using Substrate.Sectors;
using Substrate.Sessions;
using Tests.PlayMode.Common;
using UI;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Tests.PlayMode.Scenarios.Drawn
{
    public abstract class DrawnComparisonScenario : CaptureScenario
    {
        private const string Folder = "Assets/Scripts/Editor/Tests/PlayMode/Scenarios/Drawn/";
        private const string PaintedRockFolder = "Assets/Visuals/Environment/Asteroids/DrawnStudy/";
        private readonly List<Object> owned = new();
        protected abstract int Treatment { get; }
        public override GizmoCaptureProfile Profile => GizmoCaptureProfile.None;
        public override CaptureConfig Config => new()
        {
            clipName = GetType().Name, width = 1920, height = 1080,
            everyFixedSteps = 1, minHalfHeight = 14, padding = 0,
            configureView = ConfigureView
        };

        public override SectorEntry SectorEntry => new()
        {
            prefab = Load<Sector>(Folder + "DrawnComparisonSector.prefab"),
            config = Load<SectorSettings>("Assets/Settings/Game/DefaultSectorConfig.asset")
        };

        public override IEnumerator Run()
        {
            var quality = QualitySettings.GetQualityLevel();
            var random = UnityEngine.Random.state;
            var root = new GameObject("Drawn comparison");
            DrawnInkStudy ink = null;
            try
            {
                var high = Array.IndexOf(QualitySettings.names, "High Fidelity");
                Assert.That(high, Is.GreaterThanOrEqualTo(0));
                QualitySettings.SetQualityLevel(high, true);
                if (Treatment == 3 || Treatment == 4) ink = new DrawnInkStudy(Treatment == 4 ? 1 : 0);
                UnityEngine.Random.InitState(685);
                var template = Load<Ship>("Assets/Prefabs/Ships/Ship_1.prefab");
                var ship = Session.Units.SpawnShip(template, null, 0, Vector3.zero, GamePlane.Rotation, null);
                var hull = ship.GetComponentInChildren<ShipVisualRig>().transform.Find("Model");
                Assert.That(hull, Is.Not.Null, "The comparison requires Ship_1's committed hull.");
                ApplyTreatment(hull.GetComponent<MeshRenderer>());

                var settings = Load<AsteroidSpawnSettings>("Assets/Settings/Asteroids/SpawnSettings.asset");
                var rock = Object.Instantiate(settings.asteroidPrefab, root.transform);
                rock.transform.position = GamePlane.PlanePointToWorld(new Vector2(5, 9));
                rock.Initialize(null, null, settings.meshInfos[0], 0, 20, 1.4f,
                    Vector3.zero, new Vector3(0.36f, 0.53f, 0.21f));
                var rockMesh = Treatment == 5
                    ? Load<GameObject>(PaintedRockFolder + "AsteroidPaintStudy.fbx").GetComponentInChildren<MeshFilter>().sharedMesh
                    : rock.CurrentMesh;
                rock.GetComponent<MeshFilter>().sharedMesh = rockMesh;
                ApplyTreatment((MeshRenderer)rock.Renderer, true);
                var initialRockRotation = rock.transform.rotation;

                var volume = root.AddComponent<Volume>();
                volume.isGlobal = true;
                volume.priority = 100;
                volume.sharedProfile = Load<VolumeProfile>("Assets/Settings/Rendering/HighRes.asset");
                var profile = volume.profile;
                owned.Add(profile);
                foreach (var component in profile.components) owned.Add(component);
                if (profile.TryGet<FilmGrain>(out var grain)) grain.active = false;
                if (profile.TryGet<ChromaticAberration>(out var fringe)) fringe.active = false;
                if (profile.TryGet<MotionBlur>(out var blur)) blur.active = false;

                var canvas = new GameObject("Comparison labels", typeof(Canvas)).GetComponent<Canvas>();
                canvas.transform.SetParent(root.transform);
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 100;
                var label = Label(canvas.transform, "", new Vector2(32, -28), 26);
                var inspection = new GameObject("Inspection", typeof(RectTransform), typeof(Image));
                inspection.transform.SetParent(canvas.transform, false);
                var panel = inspection.GetComponent<Image>();
                panel.color = new Color(.015f, .025f, .055f);
                panel.rectTransform.anchorMin = Vector2.zero;
                panel.rectTransform.anchorMax = Vector2.one;
                panel.rectTransform.offsetMin = panel.rectTransform.offsetMax = Vector2.zero;
                label.transform.SetAsLastSibling();
                var stage = HangarPreviewStage.Create(false, root.transform);
                stage.Show(new ShipLoadout(template, template.Engine, template.Shield, null, null));
                foreach (var mesh in stage.GetComponentsInChildren<MeshRenderer>()) ApplyTreatment(mesh);
                Picture(inspection.transform, stage.Texture, new Vector2(-430, 0), new Vector2(760, 760));
                var rockPreview = new GameObject("Asteroid inspection", typeof(MeshFilter), typeof(MeshRenderer));
                rockPreview.transform.SetParent(root.transform);
                rockPreview.transform.position = new Vector3(1000, -1000, 0);
                rockPreview.layer = LayerMask.NameToLayer("ShipPreview");
                rockPreview.GetComponent<MeshFilter>().sharedMesh = rockMesh;
                rockPreview.GetComponent<MeshRenderer>().sharedMaterial = rock.Renderer.sharedMaterial;
                if (Treatment >= 2) AddContour(rockPreview.GetComponent<MeshRenderer>());
                var rockCamera = new GameObject("Asteroid inspection camera", typeof(Camera)).GetComponent<Camera>();
                rockCamera.transform.SetParent(root.transform);
                rockCamera.transform.position = rockPreview.transform.position + new Vector3(0, 0, 6);
                rockCamera.transform.LookAt(rockPreview.transform.position);
                rockCamera.cullingMask = 1 << rockPreview.layer;
                rockCamera.clearFlags = CameraClearFlags.SolidColor;
                rockCamera.backgroundColor = new Color(.015f, .025f, .055f);
                var texture = new RenderTexture(768, 768, 24);
                owned.Add(texture);
                rockCamera.targetTexture = texture;
                Picture(inspection.transform, texture, new Vector2(430, 0), new Vector2(760, 760));
                inspection.SetActive(false);
                yield return null;
                Film(ship);

                var trace = new StringBuilder("step,time,bank,x,y,rockX,rockY,rockZ,rockW,health\n");
                var minBank = 0f;
                var maxBank = 0f;
                var steps = Mathf.RoundToInt(18 / Time.fixedDeltaTime);
                for (var step = 0; step < steps; step++)
                {
                    var time = step * Time.fixedDeltaTime;
                    var strafe = time < 1 ? 0 : time < 2.5f ? 1 : time < 4 ? -1 : 0;
                    ship.Movement.Drive(new PilotCommand { strafe = strafe });
                    if (step == Mathf.RoundToInt(6 / Time.fixedDeltaTime))
                        ship.Damage.TakeDamage(new DamageInfo(ship.Damage.Shield.CurrentValue + ship.maxHealth * .6f,
                            DamageKind.Laser, ShipId.Invalid, 1, Vector3.zero, ship.transform.position));
                    inspection.SetActive(time >= 9);
                    rockPreview.transform.rotation = rock.transform.rotation;
                    label.text = $"{(Treatment == 0 ? "CURRENT" : Treatment == 1 ? "A  ·  DRAWN SURFACE" : Treatment == 2 ? "B  ·  DRAWN SURFACE + CONTOUR" : Treatment == 3 ? "OUTER CONTOUR ONLY" : Treatment == 4 ? "INK STUDY  ·  SILHOUETTES + OVERLAPS" : "ASTEROID STUDY  ·  SCULPTED FORMS + PAINT")}     /     " +
                                 (time < 6 ? "FLIGHT · BANK / SETTLE" : time < 9 ? "HULL DAMAGE / HIT FLASH" : "HANGAR + ASTEROID INSPECTION");
                    yield return new WaitForFixedUpdate();
                    FilmStep();
                    var bank = ship.Kinematics.bank;
                    minBank = Mathf.Min(minBank, bank);
                    maxBank = Mathf.Max(maxBank, bank);
                    var q = rock.transform.rotation;
                    trace.AppendLine(FormattableString.Invariant($"{step},{time},{bank},{ship.Kinematics.pos.x},{ship.Kinematics.pos.y},{q.x},{q.y},{q.z},{q.w},{ship.HealthPct}"));
                }
                Assert.That(minBank, Is.LessThan(-10), "Left strafe must produce real banking.");
                Assert.That(maxBank, Is.GreaterThan(10), "Right strafe must produce real banking.");
                Assert.That(Quaternion.Angle(initialRockRotation, rock.transform.rotation), Is.GreaterThan(10));
                Assert.That(ship.HealthPct, Is.EqualTo(.4f).Within(.01f));
                var block = new MaterialPropertyBlock();
                hull.GetComponent<Renderer>().GetPropertyBlock(block);
                Assert.That(block.GetFloat("_DetailAlbedoMapScale"), Is.EqualTo(1.2f).Within(.02f));
                File.WriteAllText(Path.Combine(Capture.FrameDir, "motion.csv"), trace.ToString());
                File.WriteAllText(Path.Combine(Capture.FrameDir, "rendering.txt"),
                    $"GPU: {SystemInfo.graphicsDeviceName}\nQuality: {QualitySettings.names[QualitySettings.GetQualityLevel()]}\n" +
                    $"Fixed timestep: {Time.fixedDeltaTime.ToString(CultureInfo.InvariantCulture)}\nInspection RT: 768x768\n" +
                    "Environment_2; HighRes without grain, chromatic aberration or motion blur.\nFrame-dump timings are not performance evidence.\n");
            }
            finally
            {
                ink?.Dispose();
                Object.DestroyImmediate(root);
                foreach (var item in owned) if (item) Object.DestroyImmediate(item);
                QualitySettings.SetQualityLevel(quality, true);
                UnityEngine.Random.state = random;
            }
        }

        private static void ConfigureView(Camera camera, Light light)
        {
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.allowHDR = true;
            camera.allowMSAA = false;
            camera.cullingMask &= ~(1 << LayerMask.NameToLayer("ShipPreview"));
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            light.enabled = false;
        }

        private void ApplyTreatment(MeshRenderer renderer, bool asteroid = false)
        {
            if (Treatment == 0) return;
            if (asteroid && Treatment == 5)
            {
                renderer.sharedMaterial = Load<Material>(PaintedRockFolder + "AsteroidPaint.mat");
                AddContour(renderer);
                return;
            }
            var original = renderer.sharedMaterial;
            var shader = Shader.Find("Astronomical/Comparison/Drawn Surface");
            Assert.That(shader && shader.isSupported, Is.True, "Drawn surface shader must compile.");
            var material = new Material(shader);
            owned.Add(material);
            foreach (var property in new[] { "_BaseMap", "_DetailAlbedoMap", "_DetailMask", "_EmissionMap" })
            {
                material.SetTexture(property, original.GetTexture(property));
                material.SetTextureScale(property, original.GetTextureScale(property));
                material.SetTextureOffset(property, original.GetTextureOffset(property));
            }
            material.SetColor("_BaseColor", original.GetColor("_BaseColor"));
            material.SetColor("_EmissionColor", original.GetColor("_EmissionColor"));
            material.SetFloat("_DetailAlbedoMapScale", 0);
            if (Treatment >= 3)
            {
                if (!asteroid)
                {
                    var filter = renderer.GetComponent<MeshFilter>();
                    filter.sharedMesh = SmoothVisualNormals(filter.sharedMesh);
                }
                material.SetColor("_PaperColor", asteroid ? new Color(.65f, .59f, .64f) : new Color(.95f, .93f, .89f));
                material.SetFloat("_TextureStrength", asteroid ? .08f : .05f);
                material.SetFloat("_PigmentPreservation", asteroid ? 0 : 1);
                material.SetFloat("_PaletteLighting", 1);
                material.SetFloat("_AmbientStrength", .2f);
                material.SetFloat("_LineThreshold", .008f);
                material.SetFloat("_LineSoftness", .015f);
                material.SetFloat("_LineStrength", asteroid ? .20f : .98f);
                material.SetColor("_ShadowColor", asteroid ? new Color(.40f, .36f, .52f) : new Color(.34f, .33f, .48f));
                material.SetFloat("_ShadowThreshold", asteroid ? .05f : -.10f);
                material.SetFloat("_ShadowSoftness", .18f);
                material.SetFloat("_SpecularStrength", .015f);
                material.SetFloat("_EmissionStrength", .06f);
            }
            renderer.sharedMaterial = material;
            if (Treatment >= 2) AddContour(renderer);
        }

        private Mesh SmoothVisualNormals(Mesh source)
        {
            using var meshData = MeshUtility.AcquireReadOnlyMeshData(source);
            var data = meshData[0];
            Assert.That(data.subMeshCount, Is.EqualTo(1), "Comparison meshes require one surface material.");
            using var vertexData = new NativeArray<Vector3>(data.vertexCount, Allocator.Temp);
            using var uvData = new NativeArray<Vector2>(data.vertexCount, Allocator.Temp);
            using var indexData = new NativeArray<int>(data.GetSubMesh(0).indexCount, Allocator.Temp);
            data.GetVertices(vertexData);
            data.GetUVs(0, uvData);
            data.GetIndices(indexData, 0);
            var vertices = vertexData.ToArray();
            var uv = uvData.ToArray();
            var triangles = indexData.ToArray();
            var mesh = new Mesh
            {
                name = source.name + " Drawn",
                indexFormat = source.indexFormat,
                vertices = vertices,
                uv = uv,
                triangles = triangles,
                bounds = source.bounds
            };
            owned.Add(mesh);
            var normals = new Vector3[vertices.Length];
            var sums = new Dictionary<Vector3, Vector3>();
            for (var i = 0; i < triangles.Length; i += 3)
            {
                var a = triangles[i];
                var b = triangles[i + 1];
                var c = triangles[i + 2];
                var normal = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
                for (var corner = 0; corner < 3; corner++)
                {
                    var index = triangles[i + corner];
                    sums.TryGetValue(vertices[index], out var sum);
                    sums[vertices[index]] = sum + normal;
                }
            }
            for (var i = 0; i < normals.Length; i++)
                normals[i] = sums[vertices[i]].normalized;
            mesh.normals = normals;
            return mesh;
        }

        private void AddContour(MeshRenderer renderer)
        {
            var shader = Shader.Find("Astronomical/Comparison/Drawn Contour");
            Assert.That(shader && shader.isSupported, Is.True, "Drawn contour shader must compile.");
            var shell = new GameObject("Selective contour", typeof(MeshFilter), typeof(MeshRenderer));
            shell.transform.SetParent(renderer.transform, false);
            shell.layer = renderer.gameObject.layer;
            shell.GetComponent<MeshFilter>().sharedMesh = renderer.GetComponent<MeshFilter>().sharedMesh;
            var outline = shell.GetComponent<MeshRenderer>();
            var material = new Material(shader);
            owned.Add(material);
            if (Treatment >= 3)
            {
                material.SetFloat("_ContourPixels", 4.5f);
                material.SetFloat("_ContourMinimum", .6f);
                material.SetColor("_ContourColor", new Color(.003f, .004f, .009f));
            }
            outline.sharedMaterial = material;
            outline.shadowCastingMode = ShadowCastingMode.Off;
            outline.receiveShadows = false;
        }

        private static T Load<T>(string path) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.That(asset, Is.Not.Null, path);
            return asset;
        }

        private static Text Label(Transform parent, string text, Vector2 position, int size)
        {
            var label = new GameObject("Label", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            label.transform.SetParent(parent, false);
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = size;
            label.color = new Color(.86f, .9f, 1);
            label.text = text;
            label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(0, 1);
            label.rectTransform.pivot = new Vector2(0, 1);
            label.rectTransform.anchoredPosition = position;
            label.rectTransform.sizeDelta = new Vector2(1840, 60);
            return label;
        }

        private static void Picture(Transform parent, Texture texture, Vector2 position, Vector2 size)
        {
            var image = new GameObject("Inspection view", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            image.transform.SetParent(parent, false);
            image.texture = texture;
            image.rectTransform.anchoredPosition = position;
            image.rectTransform.sizeDelta = size;
        }
    }
}
#endif
