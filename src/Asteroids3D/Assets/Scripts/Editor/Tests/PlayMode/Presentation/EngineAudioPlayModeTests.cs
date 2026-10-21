#if UNITY_EDITOR && ASTRO_FMOD
using System.Collections;
using System.Linq;
using FMOD.Studio;
using NUnit.Framework;
using Ships;
using Ships.Audio;
using Ships.Command;
using Ships.Presentation;
using Tests.PlayMode.Common;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Tests.PlayMode.Presentation
{
    [Category("Audio")]
    public class EngineAudioPlayModeTests : FmodEvidenceFixture
    {
        private const string RigPrefabPath = "Assets/Visuals/Ships/_Shared/ShipBaseRig.prefab";
        private const string EngineEvent = "event:/Ships/Engine";
        private const string BoostEvent = "event:/Ships/Boost";

        private GameObject host;
        private Ship ship;
        private PilotCommand command;
        private bool boostAvailable;

        [TearDown]
        public override void TearDown()
        {
            if (host) Object.DestroyImmediate(host);
            if (ship) Object.DestroyImmediate(ship.gameObject);
            base.TearDown();
        }

        // The base rig cannot instantiate alone, so its authored values go onto a lone component.
        private EngineAudio NewEngine()
        {
            var authored = AssetDatabase.LoadAssetAtPath<GameObject>(RigPrefabPath).GetComponentInChildren<EngineAudio>(true);
            Assert.IsNotNull(authored, "the rig prefab carries EngineAudio");
            host = new GameObject("Engine");
            var engine = host.AddComponent<EngineAudio>();
            ComponentUtility.CopyComponent(authored);
            ComponentUtility.PasteComponentValues(engine);
            return engine;
        }

        private ShipView View() =>
            new(host.transform, null, () => command, null, false, null, () => boostAvailable);

        [UnityTest]
        public IEnumerator BoundEngine_PlaysOneInstance_FedTheCommand()
        {
            var description = Fresh(EngineEvent);
            var engine = NewEngine();
            boostAvailable = true;
            command = new PilotCommand { thrust = -0.6f, strafe = -0.4f };
            engine.Bind(View());
            yield return null;
            yield return Flush();

            var instances = Instances(description);
            Debug.Log($"[FMOD] {EngineEvent} instances={instances.Length}");
            Assert.AreEqual(1, instances.Length, "engine instances on one bound engine");
            AssertPlaying(instances[0], "the engine sounds while bound");
            var thrust = Parameter(instances[0], "Thrust");
            var strafe = Parameter(instances[0], "Strafe");
            Debug.Log($"[FMOD] thrust={thrust} strafe={strafe} state={State(instances[0])}");
            Assert.AreEqual(-0.6f, thrust, 1e-4f, "Thrust is the signed command");
            Assert.AreEqual(0.4f, strafe, 1e-4f, "Strafe is the strafe magnitude");
        }

        [UnityTest]
        public IEnumerator UnboundEngine_StaysSilent()
        {
            var description = Fresh(EngineEvent);
            NewEngine();
            yield return Flush();

            Assert.AreEqual(0, Instances(description).Length, "an unbound engine starts nothing");
        }

        [UnityTest]
        public IEnumerator DisabledEngine_StopsTheInstance()
        {
            var description = Fresh(EngineEvent);
            var engine = NewEngine();
            boostAvailable = true;
            engine.Bind(View());
            yield return Flush();

            host.SetActive(false);
            yield return Flush();

            var states = Instances(description).Select(State).ToArray();
            Debug.Log($"[FMOD] after disable: {string.Join(",", states)}");
            Assert.That(states, Has.None.EqualTo(PLAYBACK_STATE.PLAYING), "disabling the engine stops it");
        }

        [UnityTest]
        public IEnumerator BoostReadyFalling_PlaysOneBoost()
        {
            var boost = Fresh(BoostEvent);
            var engine = NewEngine();
            boostAvailable = true;
            engine.Bind(View());
            yield return null;
            yield return Flush();
            Assert.AreEqual(0, Instances(boost).Length, "no boost while the boost stays ready");

            boostAvailable = false;
            yield return null;
            yield return Flush();
            var afterFall = Instances(boost).Length;
            Debug.Log($"[FMOD] {BoostEvent} instances after the fall={afterFall}");
            Assert.AreEqual(1, afterFall, "one boost when the ready state falls");

            yield return null;
            yield return Flush();
            Assert.That(Instances(boost).Length, Is.LessThanOrEqualTo(1), "staying unready plays no second boost");
        }

        [UnityTest]
        public IEnumerator SpawnedShip_RigBindsOneEngine()
        {
            var description = Fresh(EngineEvent);
            ship = ShipTestFactory.CreateDefaultShip(Projectiles);
            yield return null;
            yield return Flush();

            var instances = Instances(description);
            Debug.Log($"[FMOD] {EngineEvent} instances on a spawned ship={instances.Length}");
            Assert.AreEqual(1, instances.Length, "the ship's rig binds and starts its engine");
            AssertPlaying(instances[0], "the spawned ship's engine sounds");
        }
    }
}
#endif
