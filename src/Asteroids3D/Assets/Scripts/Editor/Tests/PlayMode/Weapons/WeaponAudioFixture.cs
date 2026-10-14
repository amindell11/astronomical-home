#if UNITY_EDITOR && ASTRO_FMOD
using System.Collections;
using System.Collections.Generic;
using Combat;
using Combat.Weapons;
using FMOD.Studio;
using FMODUnity;
using NUnit.Framework;
using Ships.Registry;
using Substrate;
using Tests.PlayMode.Common;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tests.PlayMode.Weapons
{
    /// <summary>
    /// Local FMOD evidence for weapon audio, compiled only against the real package: weapon prefabs
    /// mounted under a bare shooter, triggers driven directly, and instance state read back from
    /// Studio after a flushed frame.
    /// </summary>
    public abstract class WeaponAudioFixture : PlayModeWorldFixture
    {
        private sealed class TestShooter : MonoBehaviour, IShooter
        {
            public Vector3 Velocity => Vector3.zero;
            public Rigidbody Body => null;
            public ShipId Id => ShipId.Invalid;
        }

        private readonly List<GameObject> spawned = new();

        [TearDown]
        public override void TearDown()
        {
            foreach (var go in spawned)
                if (go) Object.DestroyImmediate(go);
            spawned.Clear();

            base.TearDown();
        }

        /// <summary>The prefab's weapon mounted under a shooter root at the origin, nose along the plane's forward axis.</summary>
        protected T Mount<T>(string prefabPath) where T : WeaponComponent
        {
            var prefab = AssetDatabase.LoadAssetAtPath<T>(prefabPath);
            Assert.IsNotNull(prefab, $"Failed to load weapon prefab at {prefabPath}");
            var ship = new GameObject("Shooter") { layer = LayerIds.Ship };
            spawned.Add(ship);
            ship.transform.rotation = GamePlane.Rotation;
            ship.AddComponent<TestShooter>();
            var weapon = Object.Instantiate(prefab, ship.transform);
            spawned.Add(weapon.gameObject);
            return weapon;
        }

        protected GameObject Target(Vector3 position)
        {
            var go = new GameObject("Target") { layer = LayerIds.Ship };
            spawned.Add(go);
            go.transform.position = position;
            go.AddComponent<BoxCollider>().size = Vector3.one;
            return go;
        }

        protected static Vector3 Ahead(WeaponComponent weapon, float distance) =>
            weapon.firePoint.position + weapon.firePoint.up * distance;

        protected void Hold(WeaponComponent weapon, int steps)
        {
            for (var i = 0; i < steps; i++)
                weapon.HandleTrigger(new WeaponCommand { pressed = i == 0, held = true }, Projectiles);
        }

        protected void Release(WeaponComponent weapon) =>
            weapon.HandleTrigger(new WeaponCommand { pressed = false, held = false }, Projectiles);

        /// <summary>The event's description with every instance earlier tests left released.</summary>
        protected static EventDescription Fresh(string eventPath)
        {
            var description = RuntimeManager.GetEventDescription(eventPath);
            description.releaseAllInstances();
            RuntimeManager.StudioSystem.flushCommands();
            return description;
        }

        /// <summary>Flushes Studio's command queue and yields a frame, so instance state reflects everything sent.</summary>
        protected static IEnumerator Flush()
        {
            RuntimeManager.StudioSystem.flushCommands();
            yield return null;
        }

        protected static EventInstance[] Instances(EventDescription description)
        {
            description.getInstanceList(out var instances);
            return instances;
        }

        protected static PLAYBACK_STATE State(EventInstance instance)
        {
            instance.getPlaybackState(out var state);
            return state;
        }

        protected static float Parameter(EventInstance instance, string name)
        {
            instance.getParameterByName(name, out var value);
            return value;
        }

        protected static Vector3 Position(EventInstance instance)
        {
            instance.get3DAttributes(out var attributes);
            return new Vector3(attributes.position.x, attributes.position.y, attributes.position.z);
        }

        protected static void AssertPlaying(EventInstance instance, string why) =>
            Assert.That(State(instance), Is.EqualTo(PLAYBACK_STATE.PLAYING).Or.EqualTo(PLAYBACK_STATE.STARTING), why);
    }
}
#endif
