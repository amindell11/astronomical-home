#if UNITY_EDITOR && ASTRO_FMOD
using System.Collections;
using System.Linq;
using Combat.Targeting;
using FMOD.Studio;
using Movement;
using NUnit.Framework;
using Ships.Keystones;
using Ships.Keystones.Kits;
using Substrate.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Tests.PlayMode.Audio
{
    [Category("Audio")]
    public class KeystoneAudioPlayModeTests : FmodEvidenceFixture
    {
        private const string KitPrefabPath = "Assets/Prefabs/Keystones/Valis_Overdrive.prefab";
        private const string RamPrefabPath = "Assets/Prefabs/Keystones/Bulwark_Dash.prefab";
        private const string OverdriveEvent = "event:/Ships/Keystones/Overdrive";
        private const string RamEvent = "event:/Ships/Keystones/Ram";
        private const float HeldSeconds = 0.5f;
        private const float FadeSeconds = 1f;

        private static readonly Kinematics Still = new(Vector2.zero, Vector2.zero, 0f, 0f, 0f);

        private static readonly Vector3 ShipVelocity = new(12f, -5f, 0f);

        private GameObject kitHost;
        private GameObject ship;
        private SimulationMode simulationMode;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();
            simulationMode = Physics.simulationMode;
        }

        [TearDown]
        public override void TearDown()
        {
            if (kitHost) Object.DestroyImmediate(kitHost);
            if (ship) Object.DestroyImmediate(ship);
            Physics.simulationMode = simulationMode;
            base.TearDown();
        }

        private Keystone Kit(string prefabPath)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.IsNotNull(prefab, prefabPath);
            kitHost = Object.Instantiate(prefab);
            var kit = kitHost.GetComponent<Keystone>();
            Assert.IsNotNull(kit, "the kit prefab carries a Keystone");
            return kit;
        }

        private static void Activate(Keystone kit)
        {
            kit.Step(Still, 1f, Time.fixedDeltaTime, out var activated);
            Assert.IsTrue(activated);
        }

        [UnityTest]
        public IEnumerator Activation_StartsOneInstance()
        {
            var description = Fresh(OverdriveEvent);
            var kit = Kit(KitPrefabPath);
            yield return Flush();
            Assert.AreEqual(0, Instances(description).Length, "a ready kit starts nothing");

            Activate(kit);
            yield return null;
            yield return Flush();

            var instances = Instances(description);
            Debug.Log($"[FMOD] {OverdriveEvent} instances={instances.Length}");
            Assert.AreEqual(1, instances.Length, "one instance on activation");
            AssertPlaying(instances[0], "the activation sounds");
        }

        [UnityTest]
        public IEnumerator Ram_SoundsAtTheLaunch_NotThePress()
        {
            var description = Fresh(RamEvent);
            var kit = Kit(RamPrefabPath);
            Activate(kit);
            yield return null;
            yield return Flush();
            Assert.AreEqual(0, Instances(description).Length, "the brake is silent of the launch sound");

            for (var step = 0; ((RamDash)kit).Braking && step < 200; step++)
                kit.Step(Still, 0f, Time.fixedDeltaTime, out _);

            yield return null;
            yield return Flush();
            var instances = Instances(description);
            Assert.AreEqual(1, instances.Length, "the launch sounds");
            AssertPlaying(instances[0], "the launch sounds");
        }

        [UnityTest]
        public IEnumerator WindowEnd_FadesTheHeldSound()
        {
            var description = Fresh(OverdriveEvent);
            var kit = Kit(KitPrefabPath);
            Activate(kit);
            yield return null;
            yield return Flush();

            yield return new WaitForSecondsRealtime(HeldSeconds);
            yield return Flush();
            var held = Instances(description);
            Assert.AreEqual(1, held.Length, "one instance inside the window");
            Assert.That(State(held[0]), Is.EqualTo(PLAYBACK_STATE.SUSTAINING), "the sound holds at its sustain point inside the window");

            kit.Step(Still, 0f, ((Overdrive)kit).Window, out _);
            Assert.IsFalse(kit.Active, "the window has passed");
            yield return null;
            yield return Flush();
            yield return new WaitForSecondsRealtime(FadeSeconds);
            yield return Flush();

            var states = Instances(description).Select(State).ToArray();
            Debug.Log($"[FMOD] {OverdriveEvent} {FadeSeconds}s after the window: {string.Join(",", states)}");
            Assert.That(states, Has.All.EqualTo(PLAYBACK_STATE.STOPPED), "the window's end fades out the 12 s clip");
        }

        [UnityTest]
        public IEnumerator ReadyKit_StaysSilent()
        {
            var description = Fresh(OverdriveEvent);
            Kit(KitPrefabPath);
            yield return Flush();
            yield return Flush();
            Assert.AreEqual(0, Instances(description).Length, "a kit that never activates stays silent");
        }

        [UnityTest]
        public IEnumerator Activation_CarriesTheShipsVelocity()
        {
            Physics.simulationMode = SimulationMode.Script;
            var description = Fresh(OverdriveEvent);
            ship = new GameObject("Ship");
            var body = ship.AddComponent<Rigidbody>();
            body.useGravity = false;
            body.linearVelocity = ShipVelocity;

            var kit = Kit(KitPrefabPath);
            kit.Initialize(new KeystoneHost(body, 1f, new LockChannel()));
            Activate(kit);
            yield return null;
            yield return Flush();

            var instances = Instances(description);
            Assert.AreEqual(1, instances.Length, "one instance on activation");
            var velocity = Velocity(instances[0]);
            Debug.Log($"[FMOD] {OverdriveEvent} velocity={velocity}");
            Assert.AreEqual(ShipVelocity.x, velocity.x, 1e-3f, "activation velocity");
            Assert.AreEqual(ShipVelocity.y, velocity.y, 1e-3f, "activation velocity");
            Assert.AreEqual(ShipVelocity.z, velocity.z, 1e-3f, "activation velocity");
        }

        [UnityTest]
        public IEnumerator PresentationOff_StaysSilent()
        {
            var description = Fresh(OverdriveEvent);
            var kit = Kit(KitPrefabPath);
            PresentationApplier.Apply(kit.gameObject, false);
            Activate(kit);
            yield return null;
            yield return Flush();
            Assert.AreEqual(0, Instances(description).Length, "presentation off starts nothing");
        }
    }
}
#endif
