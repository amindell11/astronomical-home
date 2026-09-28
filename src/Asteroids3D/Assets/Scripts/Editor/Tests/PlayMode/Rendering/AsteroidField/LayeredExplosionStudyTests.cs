#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Asteroids;
using Asteroids.Fragnetics;
using Asteroids.Spawning;
using Asteroids.Visual;
using Capture;
using Damage;
using NUnit.Framework;
using Ships.Registry;
using Tests.PlayMode.Common;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using Utils;
using Object = UnityEngine.Object;

namespace Tests.PlayMode.Rendering.AsteroidField
{
    [Category("Camera"), Category("RequiresGraphics")]
    public sealed class LayeredExplosionStudyTests : PlayModeWorldFixture
    {
        const string Folder = "Assets/Visuals/Vfx/LayeredExplosion/";
        const string Stage = "Assets/Visuals/Studies/DrawnArt/";
        readonly List<Object> resources = new List<Object>();
        GameObject root;

        [UnityTest, Timeout(240000)]
        public IEnumerator FragmentingAsteroid_UsesLayeredExplosion()
        {
            var output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../results/layered-asteroid/" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")));
            Directory.CreateDirectory(output);
            TestContext.Progress.WriteLine("LAYERED_ASTEROID_CAPTURE=" + output);
            var quality = QualitySettings.GetQualityLevel();
            var priorTarget = RenderTexture.active;
            var priorSun = RenderSettings.sun;
            var priorAmbient = RenderSettings.ambientLight;
            var priorMode = RenderSettings.ambientMode;
            var priorSrgb = GL.sRGBWrite;
            var assets = new DrawnFieldStudyAssets();
            try
            {
                QualitySettings.SetQualityLevel(Array.IndexOf(QualitySettings.names, "High Fidelity"), true);
                BuildAssets(assets);
                root = new GameObject("Layered asteroid fragmentation study");
                root.layer = 30;
                var camera = CreateStage();
                var target = new RenderTexture(1440, 810, 24, RenderTextureFormat.ARGBHalf) { antiAliasing = 4 };
                var encoded = new RenderTexture(1440, 810, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                var image = new Texture2D(1440, 810, TextureFormat.RGB24, false);
                resources.AddRange(new Object[] { target, encoded, image });
                camera.targetTexture = target;
                var holder = new GameObject("Fragment spawner");
                holder.transform.SetParent(root.transform);
                holder.SetActive(false);
                var fragger = holder.AddComponent<Fragger>();
                Set(fragger, "asteroidFragAsteroidFragSettings", Load<AsteroidFragSettings>("Assets/Settings/Asteroids/FragSettings.asset"));
                var spawner = holder.AddComponent<AsteroidSpawner>();
                var settings = Load<AsteroidSpawnSettings>(Folder + "Settings/FragmentPreview.asset");
                Set(spawner, "settings", settings);
                var fragments = new List<AsteroidController>();
                void Style(AsteroidController rock)
                {
                    foreach (Transform child in rock.transform.Cast<Transform>().ToArray()) Object.DestroyImmediate(child.gameObject);
                    assets.Apply(rock);
                    foreach (Transform child in rock.GetComponentsInChildren<Transform>()) child.gameObject.layer = 30;
                }
                spawner.OnFragmentSpawned += rock => { Style(rock); fragments.Add(rock); };
                holder.SetActive(true);
                UnityEngine.Random.InitState(713);
                var info = settings.meshInfos[0];
                var scale = 1.65f;
                var attributes = new AsteroidAttributes(info, 0, info.cachedVolume * scale * scale * scale * settings.density,
                    scale, new Vector3(.12f, .04f, 0), new Vector3(.04f, .07f, .12f));
                var parent = spawner.Spawn(new Pose(Vector3.zero, Quaternion.Euler(20, 15, -12)), attributes);
                Style(parent);
                var effectSeen = false;
                var pixelDifference = 0L;
                Color32[] before = null;
                using (CapturePacing.Locked(1))
                {
                    for (var frame = 0; frame < 220; frame++)
                    {
                        if (frame == 45)
                            parent.Damage.TakeDamage(new DamageInfo(parent.Damage.MaxHealth + 1, DamageKind.Collision,
                                ShipId.Invalid, 2, new Vector3(4, 1, 0), parent.transform.position));
                        yield return new WaitForFixedUpdate();
                        if (frame == 49)
                        {
                            var live = Object.FindObjectsByType<PooledVFX>(FindObjectsSortMode.None)
                                .Where(v => v.name.StartsWith("LayeredAsteroidExplosion") && v.gameObject.activeInHierarchy).ToArray();
                            Assert.That(live.Length, Is.EqualTo(1), "Asteroid destruction must spawn its assigned explosion prefab.");
                            Assert.That(live[0].GetComponentsInChildren<ParticleSystem>().Any(p => p.particleCount > 0), Is.True);
                            effectSeen = true;
                        }
                        camera.Render();
                        GL.sRGBWrite = true; Graphics.Blit(target, encoded); RenderTexture.active = encoded;
                        image.ReadPixels(new Rect(0, 0, 1440, 810), 0, 0); image.Apply();
                        File.WriteAllBytes(Path.Combine(output, $"frame-{frame:D4}.png"), image.EncodeToPNG());
                        if (frame == 43) before = image.GetPixels32();
                        if (frame == 51)
                        {
                            var after = image.GetPixels32();
                            for (var i = 0; i < before.Length; i++) pixelDifference += Math.Abs(before[i].r - after[i].r) + Math.Abs(before[i].g - after[i].g) + Math.Abs(before[i].b - after[i].b);
                        }
                    }
                }
                Assert.That(effectSeen, Is.True);
                Assert.That(fragments.Count, Is.InRange(2, 5));
                Assert.That(fragments.All(f => f.CurrentMesh == assets.Rocks[f.MeshIndex]), Is.True);
                Assert.That(fragments.Max(f => f.transform.position.magnitude), Is.GreaterThan(2));
                Assert.That(pixelDifference, Is.GreaterThan(100000), "The burst must visibly change the rendered asteroid.");
                File.WriteAllText(Path.Combine(output, "receipt.json"), "{\"fps\":50,\"frames\":220,\"destructionFrame\":45,\"fragments\":" + fragments.Count + ",\"realDamagePath\":true,\"assignedPrefabSpawned\":true,\"drawnFragmentMeshesVerified\":true,\"pixelDifference\":" + pixelDifference + "}");
            }
            finally
            {
                if (root) Object.DestroyImmediate(root);
                foreach (var effect in Object.FindObjectsByType<PooledVFX>(FindObjectsSortMode.None))
                    if (effect.name.StartsWith("LayeredAsteroidExplosion")) Object.DestroyImmediate(effect.gameObject);
                foreach (var resource in resources) if (resource) Object.DestroyImmediate(resource);
                resources.Clear(); assets.Dispose();
                RenderTexture.active = priorTarget; GL.sRGBWrite = priorSrgb; RenderSettings.sun = priorSun;
                RenderSettings.ambientLight = priorAmbient; RenderSettings.ambientMode = priorMode;
                QualitySettings.SetQualityLevel(quality, true);
            }
        }

        static void BuildAssets(DrawnFieldStudyAssets assets)
        {
            foreach (var name in new[] { "Core", "Smoke", "Spark" })
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(Folder + "Textures/" + name + ".png");
                importer.textureType = TextureImporterType.Default; importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false; importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.npotScale = TextureImporterNPOTScale.None; importer.maxTextureSize = 2048;
                importer.wrapMode = TextureWrapMode.Clamp; importer.SaveAndReimport();
            }
            var coreMaterial = Paint("Core", "Astronomical/Studies/Jagged Explosion Flipbook", .5f);
            coreMaterial.SetFloat("_ZTest", (float)CompareFunction.Always);
            EditorUtility.SetDirty(coreMaterial);
            var smokeMaterial = Paint("Smoke", "Astronomical/Studies/Jagged Explosion Flipbook", 0);
            var sparkMaterial = Paint("Spark", "Astronomical/Studies/Traveling Ember", 0);
            var explosion = new GameObject("Layered asteroid explosion");
            explosion.SetActive(false); explosion.layer = 30;
            var burst = AddParticle(explosion, coreMaterial, .65f, 5.8f);
            Flipbook(burst, false);
            var coreRenderer = burst.GetComponent<ParticleSystemRenderer>(); coreRenderer.sortingOrder = 2;
            var smoke = new GameObject("Stylized smoke"); smoke.transform.SetParent(explosion.transform, false);
            var smokePs = AddParticle(smoke, smokeMaterial, 1.05f, 7.2f);
            var sm = smokePs.main; sm.startDelay = .055f; sm.startColor = new Color(1, 1, 1, .63f);
            Flipbook(smokePs, true);
            var specs = new[] {
                new[] {-2.88f,2350,8.7f,0,.46f,40,.85f}, new[] {-2.22f,3050,11,.004f,.26f,20,.55f},
                new[] {-1.34f,2600,9,0,.55f,86,1}, new[] {-1.12f,1900,9,.003f,.28f,24,.65f},
                new[] {-.31f,3000,8.5f,0,.42f,47,.8f}, new[] {.18f,2000,6.4f,.002f,.68f,104,1.15f},
                new[] {1f,3200,11,.006f,.23f,18,.5f}, new[] {2.21f,2300,8.5f,0,.44f,35,.75f} };
            for (var i = 0; i < specs.Length; i++)
            {
                var p = specs[i];
                var go = new GameObject("Flying spark " + (i + 1)); go.transform.SetParent(explosion.transform, false);
                var ps = AddParticle(go, sparkMaterial, p[4], p[5] / 50);
                var main = ps.main; main.startDelay = p[3]; main.startRotation = p[0];
                main.startSize3D = true; main.startSizeX = p[5] / 50; main.startSizeY = p[5] * p[6] / 50; main.startSizeZ = 1;
                var keys = new Keyframe[17];
                for (var k = 0; k < keys.Length; k++) { var t = k / 16f; keys[k] = new Keyframe(t, Mathf.Exp(-p[2] * p[4] * t)); }
                var curve = new AnimationCurve(keys);
                var velocity = ps.velocityOverLifetime; velocity.enabled = true; velocity.space = ParticleSystemSimulationSpace.World;
                velocity.x = new ParticleSystem.MinMaxCurve(Mathf.Cos(p[0]) * p[1] / 50, curve);
                velocity.y = new ParticleSystem.MinMaxCurve(-Mathf.Sin(p[0]) * p[1] / 50, curve);
                velocity.z = new ParticleSystem.MinMaxCurve(0, curve);
                var renderer = ps.GetComponent<ParticleSystemRenderer>(); renderer.sortingOrder = 3;
                renderer.SetActiveVertexStreams(new List<ParticleSystemVertexStream> { ParticleSystemVertexStream.Position, ParticleSystemVertexStream.Color, ParticleSystemVertexStream.UV, ParticleSystemVertexStream.AgePercent });
            }
            var old = Load<GameObject>("Assets/Prefabs/Particles/SmallExplosion.prefab");
            foreach (var name in new[] { "Embers", "Shockwave" })
            {
                var original = old.GetComponentsInChildren<Transform>(true).Single(t => t.name == name);
                var preserved = Object.Instantiate(original.gameObject, explosion.transform);
                preserved.name = name;
                foreach (var t in preserved.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 30;
            }
            explosion.AddComponent<PooledVFX>();
            foreach (var t in explosion.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 0;
            explosion.SetActive(true);
            var effectPrefab = PrefabUtility.SaveAsPrefabAsset(explosion, Folder + "Prefabs/LayeredAsteroidExplosion.prefab");
            Object.DestroyImmediate(explosion);
            var source = Load<AsteroidSpawnSettings>("Assets/Settings/Asteroids/SpawnSettings.asset");
            var asteroid = Object.Instantiate(source.asteroidPrefab.gameObject);
            asteroid.name = "Fragmenting drawn asteroid";
            Set(asteroid.GetComponent<AsteroidVisual>(), "explosionPrefab", effectPrefab);
            var specimen = assets.Create(0, asteroid.transform);
            var outline = specimen.transform.Find("Outer contour").GetComponent<MeshRenderer>();
            outline.sharedMaterial = Save(new Material(outline.sharedMaterial), "Materials/AsteroidContour.mat");
            asteroid.GetComponent<MeshFilter>().sharedMesh = assets.Rocks[0];
            asteroid.GetComponent<MeshRenderer>().sharedMaterial = specimen.GetComponent<MeshRenderer>().sharedMaterial;
            foreach (var t in specimen.GetComponentsInChildren<Transform>()) t.gameObject.layer = asteroid.layer;
            foreach (var child in specimen.transform.Cast<Transform>().ToArray()) child.SetParent(asteroid.transform, false);
            Object.DestroyImmediate(specimen);
            var asteroidPrefab = PrefabUtility.SaveAsPrefabAsset(asteroid, Folder + "Prefabs/FragmentingDrawnAsteroid.prefab");
            Object.DestroyImmediate(asteroid);
            var settings = Object.Instantiate(source); settings.poolCapacity = 6; settings.maxPoolSize = 12;
            settings.asteroidPrefab = asteroidPrefab.GetComponent<AsteroidController>();
            Save(settings, "Settings/FragmentPreview.asset"); AssetDatabase.SaveAssets();
            var assigned = new SerializedObject(asteroidPrefab.GetComponent<AsteroidVisual>()).FindProperty("explosionPrefab").objectReferenceValue;
            Assert.That(assigned, Is.EqualTo(effectPrefab));
        }

        static ParticleSystem AddParticle(GameObject go, Material material, float life, float size)
        {
            go.layer = 30;
            var ps = go.AddComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main; main.loop = false; main.duration = 1.2f; main.startLifetime = life; main.startSpeed = 0;
            main.startSize = size; main.maxParticles = 1; main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy; main.playOnAwake = true;
            var shape = ps.shape; shape.enabled = false;
            var emission = ps.emission; emission.rateOverTime = 0; emission.SetBursts(new[] { new ParticleSystem.Burst(0, 1) });
            var renderer = ps.GetComponent<ParticleSystemRenderer>(); renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard; renderer.shadowCastingMode = ShadowCastingMode.Off;
            ps.useAutoRandomSeed = false; ps.randomSeed = 713;
            return ps;
        }

        static void Flipbook(ParticleSystem ps, bool linear)
        {
            var frames = ps.textureSheetAnimation; frames.enabled = true; frames.numTilesX = 4; frames.numTilesY = 4; frames.cycleCount = 1;
            var ends = new[] { 0f,16,38,64,96,138,181,225,269,313,358,403,448,493,539,588,650 };
            var keys = new Keyframe[17];
            for (var i = 0; i < keys.Length; i++) keys[i] = new Keyframe(linear ? i / 16f : ends[i] / 650, i / 16f);
            frames.frameOverTime = new ParticleSystem.MinMaxCurve(1, new AnimationCurve(keys));
            ps.GetComponent<ParticleSystemRenderer>().SetActiveVertexStreams(new List<ParticleSystemVertexStream> { ParticleSystemVertexStream.Position, ParticleSystemVertexStream.Color, ParticleSystemVertexStream.UV, ParticleSystemVertexStream.UV2, ParticleSystemVertexStream.AnimBlend });
        }

        Camera CreateStage()
        {
            RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(.22f, .25f, .32f);
            var light = new GameObject("Study key", typeof(Light)).GetComponent<Light>(); light.transform.SetParent(root.transform);
            light.type = LightType.Directional; light.intensity = 1; light.shadows = LightShadows.Soft;
            light.cullingMask = 1 << 30; light.transform.rotation = Quaternion.Euler(25, -35, 0); RenderSettings.sun = light;
            var camera = new GameObject("Study camera", typeof(Camera)).GetComponent<Camera>(); camera.transform.SetParent(root.transform);
            camera.enabled = false; camera.orthographic = true; camera.orthographicSize = 6.8f; camera.transform.position = new Vector3(0, 0, -60);
            camera.allowHDR = true; camera.cullingMask = (1 << 30) | 1; camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.045f, .062f, .105f); camera.nearClipPlane = 30; camera.farClipPlane = 90;
            var data = camera.GetUniversalAdditionalCameraData(); data.renderPostProcessing = true; data.volumeLayerMask = 1 << 30;
            var volume = new GameObject("Bloom", typeof(Volume)).GetComponent<Volume>(); volume.transform.SetParent(root.transform); volume.gameObject.layer = 30;
            volume.isGlobal = true; volume.sharedProfile = Load<VolumeProfile>(Stage + "Lighting/PodBloom.asset");
            var backdrop = GameObject.CreatePrimitive(PrimitiveType.Quad); backdrop.transform.SetParent(root.transform);
            Object.DestroyImmediate(backdrop.GetComponent<Collider>()); backdrop.layer = 30; backdrop.transform.position = new Vector3(0, 0, 12);
            backdrop.transform.localScale = new Vector3(24.18f, 13.6f, 1);
            backdrop.GetComponent<MeshRenderer>().sharedMaterial = Load<Material>(Stage + "Materials/Stage/AsteroidField backdrop.mat");
            return camera;
        }

        static T Load<T>(string path) where T : Object => DrawnFieldStudyAssets.Load<T>(path);
        static void Set(Object target, string name, Object value) { var serialized = new SerializedObject(target); serialized.FindProperty(name).objectReferenceValue = value; serialized.ApplyModifiedPropertiesWithoutUndo(); }
        static T Save<T>(T value, string path) where T : Object
        {
            var old = AssetDatabase.LoadAssetAtPath<T>(Folder + path);
            if (old) { EditorUtility.CopySerialized(value, old); Object.DestroyImmediate(value); EditorUtility.SetDirty(old); return old; }
            AssetDatabase.CreateAsset(value, Folder + path); return value;
        }
        static Material Paint(string name, string shader, float glow)
        {
            var material = new Material(Shader.Find(shader)); material.SetTexture("_BaseMap", Load<Texture2D>(Folder + "Textures/" + name + ".png"));
            if (material.HasProperty("_CoreGlow")) material.SetFloat("_CoreGlow", glow);
            return Save(material, "Materials/" + name + ".mat");
        }
    }
}
#endif
