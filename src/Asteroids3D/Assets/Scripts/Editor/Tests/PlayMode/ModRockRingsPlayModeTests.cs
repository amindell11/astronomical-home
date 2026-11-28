#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Loot;
using NUnit.Framework;
using Ships;
using Ships.Mods;
using Substrate;
using Tests.PlayMode.Common;
using UI.Markers;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Tests.PlayMode
{
    /// <summary>
    /// The HUD ship's rock rings: with Lie in wait and Orbit held, a heavy rock gets a Lie in wait ring,
    /// a big one an Orbit ring, a heavy big one both (the Orbit ring wider), and a light small one
    /// none; with neither held no rock gets a ring.
    /// </summary>
    [Category("Ships")]
    public class ModRockRingsPlayModeTests : PlayModeWorldFixture
    {
        private const string ValisPath = "Assets/Prefabs/Ships/Valis.prefab";
        private const string LieInWaitPath = "Assets/Settings/Ships/Passives/LieInWait.asset";
        private const string OrbitPath = "Assets/Settings/Ships/Keystones/Mods/Orbit.asset";
        private const string OverlayPrefabPath = "Assets/Prefabs/UI/UIOverlay.prefab";
        private const float BigScale = 8f;
        private const float SmallScale = 1f;

        private readonly List<Object> made = new();

        public override void TearDown()
        {
            foreach (var item in made) if (item) Object.DestroyImmediate(item);
            made.Clear();
            base.TearDown();
        }

        private GameObject Rock(Vector2 at, float scale, float mass)
        {
            var rock = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            made.Add(rock);
            rock.layer = LayerIds.Asteroid;
            rock.transform.position = GamePlane.PlanePointToWorld(at);
            rock.transform.localScale = Vector3.one * scale;
            var body = rock.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.mass = mass;
            return rock;
        }

        private (ModRockRings rings, Color lies, Color orbits) Rings(Ship ship)
        {
            var host = new GameObject("ModRockRingsTest");
            made.Add(host);
            host.SetActive(false);
            var rings = host.AddComponent<ModRockRings>();
            var tunables = new SerializedObject(rings);
            var material = new Material(Shader.Find("Hidden/InternalErrorShader"));
            made.Add(material);
            tunables.FindProperty("ringMaterial").objectReferenceValue = material;
            tunables.ApplyModifiedPropertiesWithoutUndo();
            rings.Initialize(ship, ship.transform);
            host.SetActive(true);
            return (rings, tunables.FindProperty("lieInWaitColor").colorValue, tunables.FindProperty("orbitColor").colorValue);
        }

        private static List<(Vector3 centre, float radius, Color color)> Drawn(ModRockRings rings) =>
            rings.GetComponentsInChildren<LineRenderer>()
                .Where(line => line.enabled)
                .Select(line =>
                {
                    var centre = Vector3.zero;
                    for (var i = 0; i < line.positionCount; i++) centre += line.GetPosition(i) / line.positionCount;
                    return (centre, Vector3.Distance(centre, line.GetPosition(0)), line.startColor);
                })
                .ToList();

        private static List<(Vector3 centre, float radius, Color color)> Around(
            List<(Vector3 centre, float radius, Color color)> drawn, GameObject rock) =>
            drawn.Where(ring => Vector3.Distance(ring.centre, rock.transform.position) < 0.01f)
                .OrderBy(ring => ring.radius)
                .ToList();

        [UnityTest]
        public IEnumerator HeldRockMods_RingEachRockThatCountsForThem_InThatModsColour([Values] bool held)
        {
            var valis = Object.Instantiate(TestAssets.Load<Ship>(ValisPath), Vector3.zero, GamePlane.Rotation);
            made.Add(valis.gameObject);
            valis.Initialize(0, 0, Projectiles);
            var stack = new LullModStack();
            valis.BindMods(stack);
            if (held)
            {
                stack.Add(TestAssets.Load<PassiveMod>(LieInWaitPath));
                stack.Add(TestAssets.Load<KeystoneMod>(OrbitPath));
            }
            var heavy = valis.Rigidbody.mass * 100f;
            var light = valis.Rigidbody.mass * 0.01f;
            var both = Rock(new Vector2(15f, 0f), BigScale, heavy);
            var heavySmall = Rock(new Vector2(-15f, 0f), SmallScale, heavy);
            var bigLight = Rock(new Vector2(0f, 15f), BigScale, light);
            var neither = Rock(new Vector2(0f, -15f), SmallScale, light);
            Physics.SyncTransforms();
            var (rings, lies, orbits) = Rings(valis);

            yield return null;
            yield return null;

            var drawn = Drawn(rings);
            if (!held)
            {
                Assert.That(drawn, Is.Empty, "without the mods no rock gets a ring");
                yield break;
            }

            string Mod(Color color) => color == lies ? "Lie in wait" : color == orbits ? "Orbit" : "other";
            var onBoth = Around(drawn, both);
            Assert.That(onBoth.Select(ring => Mod(ring.color)), Is.EqualTo(new[] { "Lie in wait", "Orbit" }),
                "a heavy big rock gets both rings, the Orbit one wider");
            Assert.That(onBoth[0].radius, Is.GreaterThan(BigScale * 0.5f), "the ring sits outside the rock");
            Assert.That(Around(drawn, heavySmall).Select(ring => Mod(ring.color)), Is.EqualTo(new[] { "Lie in wait" }),
                "a heavy small rock gets the Lie in wait ring");
            Assert.That(Around(drawn, bigLight).Select(ring => Mod(ring.color)), Is.EqualTo(new[] { "Orbit" }),
                "a big light rock gets the Orbit ring");
            Assert.That(Around(drawn, neither), Is.Empty, "a light small rock gets none");
        }

        [Test]
        public void OverlayPrefab_CarriesTheRockRings_WithTheirMaterial()
        {
            var overlay = AssetDatabase.LoadAssetAtPath<GameObject>(OverlayPrefabPath);
            Assert.IsNotNull(overlay, $"Failed to load {OverlayPrefabPath}");
            var rings = overlay.GetComponentInChildren<ModRockRings>(true);
            Assert.IsNotNull(rings, "the player's HUD carries the rock rings");
            Assert.IsNotNull(new SerializedObject(rings).FindProperty("ringMaterial").objectReferenceValue,
                "the rock rings have their ring material");
        }
    }
}
#endif
