using System.Collections;
using System.IO;
using Asteroids;
using Asteroids.Fragnetics;
using Asteroids.Spawning;
using Combat;
using Combat.Projectiles;
using NUnit.Framework;
using Ships;
using Ships.Registry;
using Substrate;
using Tests.PlayMode.Common;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using Utils;

namespace Tests.PlayMode.Projectiles
{
    [Category("Presentation"), Category("RequiresGraphics")]
    public sealed class TurboRipperImpactRenderCheck : PlayModeWorldFixture
    {
        private sealed class Shooter : MonoBehaviour, IShooter
        {
            public Vector3 Velocity => Vector3.zero;
            public Rigidbody Body => null;
            public ShipId Id => ShipId.Invalid;
        }
        private GameObject root;
        [UnityTest] public IEnumerator TurboShipImpactRenders() => Check("TurboLaser", false);
        [UnityTest] public IEnumerator TurboAsteroidImpactRenders() => Check("TurboLaser", true);
        [UnityTest] public IEnumerator RipperShipImpactRenders() => Check("RipperSlug", false);
        [UnityTest] public IEnumerator RipperAsteroidImpactRenders() => Check("RipperSlug", true);
        public override void TearDown()
        {
            if (root) Object.DestroyImmediate(root);
            foreach (var effect in Object.FindObjectsByType<PooledVFX>(FindObjectsSortMode.None))
                Object.DestroyImmediate(effect.gameObject);
            SimplePool<PooledVFX>.Clear();
            SimplePool<Laser>.Clear();
            base.TearDown();
        }
        private IEnumerator Check(string prefabName, bool asteroid)
        {
            root = new GameObject("Turbo and ripper render check");
            if (asteroid)
            {
                var holder = new GameObject("Asteroid spawner");
                holder.transform.SetParent(root.transform);
                holder.SetActive(false);
                Set(holder.AddComponent<Fragger>(), "asteroidFragAsteroidFragSettings",
                    AssetDatabase.LoadAssetAtPath<AsteroidFragSettings>("Assets/Settings/Asteroids/FragSettings.asset"));
                var settings = AssetDatabase.LoadAssetAtPath<AsteroidSpawnSettings>("Assets/Settings/Asteroids/SpawnSettings.asset");
                var spawner = holder.AddComponent<AsteroidSpawner>();
                Set(spawner, "settings", settings);
                holder.SetActive(true);
                var info = settings.meshInfos[0];
                const float scale = 1.65f;
                var attrs = new AsteroidAttributes(info, 0, info.cachedVolume * scale * scale * scale * settings.density,
                    scale, Vector3.zero, Vector3.zero);
                spawner.Spawn(new Pose(Vector3.zero, Quaternion.identity), attrs, 713);
            }
            else
            {
                var ship = Factory.CreateShip(TestAssets.LoadCrimsonPrefab(), null, 1, 1, Projectiles,
                    Vector3.zero, Quaternion.identity);
                ship.transform.SetParent(root.transform);
            }
            var shooter = new GameObject("Shooter").AddComponent<Shooter>();
            shooter.transform.SetParent(root.transform);
            var laser = SimplePool<Laser>.Get(AssetDatabase.LoadAssetAtPath<Laser>(
                $"Assets/Prefabs/Weapons/Projectiles/{prefabName}.prefab"), new Vector3(0, -5, 0), Quaternion.identity);
            Projectiles.Register(laser, laser.ReturnToPoolImmediate);
            bool hit = false;
            var contact = Vector3.zero;
            laser.Hit += (point, _) => { hit = true; contact = point; };
            laser.Initialize(shooter);
            laser.Launch(Vector3.up, Vector3.zero);
            laser.SetDamageScale(0f);
            for (int i = 0; i < 100 && !hit; i++) yield return new WaitForFixedUpdate();
            Assert.IsTrue(hit, $"real {prefabName} collision reached the target");
            yield return null;
            var effects = Object.FindObjectsByType<PooledVFX>(FindObjectsSortMode.None);
            Assert.AreEqual(1, effects.Length);
            var effect = effects[0];
            Assert.Less(Vector3.Distance(effect.transform.position, contact), .001f);
            Assert.Less(Vector3.Angle(effect.transform.up, Vector3.up), .01f);
            var effectName = prefabName == "TurboLaser" ? "TurboLaserImpact" : "RipperImpact";
            var material = AssetDatabase.LoadAssetAtPath<Material>($"Assets/Visuals/Vfx/{effectName}/Materials/{effectName}.mat");
            Assert.IsTrue(material);
            Assert.AreSame(material, effect.GetComponent<ParticleSystemRenderer>().sharedMaterial);
            var particles = effect.GetComponent<ParticleSystem>();
            particles.Simulate(0.05f, true, true);
            var camera = new GameObject("Camera").AddComponent<Camera>();
            camera.transform.SetParent(root.transform);
            camera.transform.position = new Vector3(0, -0.75f, -8);
            camera.cullingMask = AssetDatabase.LoadAssetAtPath<Camera>("Assets/Prefabs/Cameras/Main Camera.prefab").cullingMask;
            camera.orthographic = true;
            camera.orthographicSize = 3;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.025f, .035f, .06f);
            camera.allowHDR = false;
            camera.allowMSAA = false;
            var light = new GameObject("Light").AddComponent<Light>();
            light.transform.SetParent(root.transform);
            light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(25, -35, 0);
            light.intensity = 1.15f;
            var priorMode = RenderSettings.ambientMode;
            var priorAmbient = RenderSettings.ambientLight;
            var priorTarget = RenderTexture.active;
            var target = new RenderTexture(768, 768, 24);
            var image = new Texture2D(768, 768, TextureFormat.RGB24, false);
            try
            {
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(.22f, .25f, .32f);
                camera.targetTexture = target;
                var output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../results/turbo-ripper-render"));
                Directory.CreateDirectory(output);
                var stem = (prefabName == "TurboLaser" ? "turbo-" : "ripper-") + (asteroid ? "asteroid" : "ship");
                var on = Render(camera, target, image, Path.Combine(output, stem + "-impact.png"));
                effect.GetComponent<ParticleSystemRenderer>().enabled = false;
                var off = Render(camera, target, image, Path.Combine(output, stem + "-without.png"));
                int changed = 0;
                for (int i = 0; i < on.Length; i++)
                    if (Mathf.Abs(on[i].r - off[i].r) + Mathf.Abs(on[i].g - off[i].g) + Mathf.Abs(on[i].b - off[i].b) > 25) changed++;
                Assert.Greater(changed, 30, "the hit effect contributes visible pixels over its real target");
                Debug.Log($"TURBO_RIPPER_IMPACT_RENDER target={stem} changedPixels={changed} contact={effect.transform.position}");
                effect.GetComponent<ParticleSystemRenderer>().enabled = true;
                particles.Simulate(0.30f, true, true);
                var tail = Render(camera, target, image, Path.Combine(output, stem + "-300ms.png"));
                int tailChanged = 0;
                for (int i = 0; i < tail.Length; i++)
                    if (Mathf.Abs(tail[i].r - off[i].r) + Mathf.Abs(tail[i].g - off[i].g) + Mathf.Abs(tail[i].b - off[i].b) > 25) tailChanged++;
                Assert.AreEqual(0, tailChanged, "the impact has visibly faded out by 300 ms");
                Debug.Log($"TURBO_RIPPER_IMPACT_TAIL target={stem} changedPixelsAt300ms={tailChanged}");
            }
            finally
            {
                RenderSettings.ambientMode = priorMode;
                RenderSettings.ambientLight = priorAmbient;
                camera.targetTexture = null;
                RenderTexture.active = priorTarget;
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(image);
            }
        }
        private static Color32[] Render(Camera camera, RenderTexture target, Texture2D image, string path)
        {
            camera.Render();
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, 768, 768), 0, 0);
            image.Apply();
            File.WriteAllBytes(path, image.EncodeToPNG());
            return image.GetPixels32();
        }
        private static void Set(Object owner, string name, Object value)
        {
            var serialized = new SerializedObject(owner);
            serialized.FindProperty(name).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}



