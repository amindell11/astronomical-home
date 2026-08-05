using System.Collections.Generic;
using Combat.Projectile;
using Game.Presentation;
using Game.Services;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode
{
    [Category("Presentation")]
    public class PresentationApplierEditModeTests
    {
        private readonly List<GameObject> tempObjects = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in tempObjects)
                if (go != null)
                    UnityEngine.Object.DestroyImmediate(go);
            tempObjects.Clear();
        }

        private GameObject Track(GameObject go)
        {
            tempObjects.Add(go);
            return go;
        }

        private sealed class RecordingPart : MonoBehaviour, IPresentationPart
        {
            public bool? LastApplied;

            public void ApplyPresentation(bool visible)
            {
                LastApplied = visible;
                enabled = visible;
            }
        }

        /// <summary>Root renderer + child particle system + child audio source + one behaviour part.</summary>
        private GameObject BuildTransient()
        {
            var root = Track(new GameObject("Transient"));
            root.AddComponent<MeshRenderer>();
            root.AddComponent<RecordingPart>();

            var particleChild = new GameObject("Trail");
            particleChild.transform.SetParent(root.transform);
            particleChild.AddComponent<ParticleSystem>();

            var audioChild = new GameObject("Audio");
            audioChild.transform.SetParent(root.transform);
            audioChild.AddComponent<AudioSource>();

            return root;
        }

        [Test]
        public void ApplyOff_DisablesPartsRenderersParticlesAndAudio()
        {
            var root = BuildTransient();

            PresentationApplier.Apply(root, false);

            Assert.IsFalse(root.GetComponent<RecordingPart>().enabled);
            Assert.AreEqual(false, root.GetComponent<RecordingPart>().LastApplied);
            Assert.IsFalse(root.GetComponent<MeshRenderer>().enabled);
            Assert.IsFalse(root.GetComponentInChildren<ParticleSystem>(true).isPlaying);
            Assert.IsFalse(root.GetComponentInChildren<AudioSource>(true).enabled);
        }

        [Test]
        public void ApplyOnAfterOff_RestoresEverything()
        {
            var root = BuildTransient();
            var parts = PresentationApplier.Capture(root);

            PresentationApplier.Apply(parts, false);
            PresentationApplier.Apply(parts, true);

            Assert.IsTrue(root.GetComponent<RecordingPart>().enabled);
            Assert.AreEqual(true, root.GetComponent<RecordingPart>().LastApplied);
            Assert.IsTrue(root.GetComponent<MeshRenderer>().enabled);
            Assert.IsTrue(root.GetComponentInChildren<AudioSource>(true).enabled);
        }

        /// <summary>Pool-free ProjectileBase so Register works without touching SimplePool statics.</summary>
        private class TestProjectile : ProjectileBase
        {
            protected override global::Damage.DamageKind Kind => global::Damage.DamageKind.Laser;

            protected override void ReturnToPool()
            {
                gameObject.SetActive(false);
                RaiseReturnedToPool();
            }

            public void Return() => ReturnToPool();
        }

        [Test]
        public void ProjectileService_AppliesItsSessionPolicy_OnEveryCheckout()
        {
            var liveRoot = Track(new GameObject("LiveRoot"));
            var headless = new ProjectileService(liveRoot.transform, presentationEnabled: false);
            var presenting = new ProjectileService(liveRoot.transform);

            var go = Track(new GameObject("Projectile"));
            go.AddComponent<MeshRenderer>();
            var part = go.AddComponent<RecordingPart>();
            var projectile = go.AddComponent<TestProjectile>();

            headless.Register(projectile, projectile.Return);
            Assert.AreEqual(false, part.LastApplied);
            Assert.IsFalse(go.GetComponent<MeshRenderer>().enabled);

            // The process-wide pool hands the darkened instance to a presenting session next.
            projectile.Return();
            go.SetActive(true);
            presenting.Register(projectile, projectile.Return);
            Assert.AreEqual(true, part.LastApplied);
            Assert.IsTrue(go.GetComponent<MeshRenderer>().enabled);
        }

        private static readonly string[] TransientPrefabPaths =
        {
            "Assets/Prefabs/Weapons/Laser.prefab",
            "Assets/Prefabs/Weapons/TurboLaser.prefab",
            "Assets/Prefabs/Weapons/RipperSlug.prefab",
            "Assets/Prefabs/Weapons/ChargeBolt.prefab",
            "Assets/Prefabs/Weapons/MissileProjectile.prefab",
            "Assets/Prefabs/Weapons/GrenadeCharge.prefab",
            "Assets/Prefabs/Weapons/ConcussionWave.prefab",
            "Assets/Prefabs/Asteroid/Asteroid3D.prefab",
        };

        [Test]
        public void EveryTransientPrefab_GoesFullyDark_UnderApplyOff([ValueSource(nameof(TransientPrefabPaths))] string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.IsNotNull(prefab, $"Missing prefab at {path}");

            var instance = Track(UnityEngine.Object.Instantiate(prefab));
            PresentationApplier.Apply(instance, false);

            foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
                Assert.IsFalse(renderer.enabled, $"{path}: renderer '{renderer.name}' still enabled");
            foreach (var source in instance.GetComponentsInChildren<AudioSource>(true))
                Assert.IsFalse(source.enabled, $"{path}: audio source '{source.name}' still enabled");
            foreach (var ps in instance.GetComponentsInChildren<ParticleSystem>(true))
                Assert.IsFalse(ps.isPlaying, $"{path}: particle system '{ps.name}' still playing");
            foreach (var part in instance.GetComponentsInChildren<IPresentationPart>(true))
                Assert.IsFalse(((Behaviour)part).enabled, $"{path}: part '{part.GetType().Name}' still enabled");
        }
    }
}
