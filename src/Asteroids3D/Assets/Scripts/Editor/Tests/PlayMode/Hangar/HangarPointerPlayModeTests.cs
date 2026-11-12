#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using Ships.Loadout;
using UI.Hangar.Displays;
using UI.Hangar.Hardware;
using UI.Hangar.Selection;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Tests.PlayMode.Hangar
{
    [Category("UI")]
    [Category("RequiresGraphics")]
    public sealed class HangarPointerPlayModeTests
    {
        private const string PrefabPath = "Assets/Prefabs/UI/HangarConsole.prefab";
        private static readonly Vector2Int[] Views = { new(1920, 1080), new(1920, 1200), new(2520, 1080) };

        private HangarConsole console;
        private GameObject eventSystemObject;

        [TearDown]
        public void TearDown()
        {
            if (console) UnityEngine.Object.Destroy(console.gameObject);
            if (eventSystemObject) UnityEngine.Object.Destroy(eventSystemObject);
        }

        [UnityTest]
        public IEnumerator ViewportHitsTheSlotUnderThatGlassPoint_AndTheControlUnderItsCentre(
            [ValueSource(nameof(Views))] Vector2Int pixels)
        {
            console = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<HangarConsole>(PrefabPath));
            yield return null;

            console.Resize(pixels);
            console.MainDisplay.DisplayCamera.Render();
            Canvas.ForceUpdateCanvases();
            var raycaster = console.GetComponent<HangarConsoleRaycaster>();
            var view = console.GetComponentInChildren<HangarMainDisplayView>();
            var glass = console.MainScreenGlass;
            var surface = new MeshSurface(glass.GetComponent<MeshFilter>().sharedMesh);
            foreach (HangarSlot slot in Enum.GetValues(typeof(HangarSlot)))
            {
                var button = view[slot];
                var rect = (RectTransform)button.transform;
                var pixel = console.MainDisplay.DisplayCamera.WorldToScreenPoint(rect.TransformPoint(rect.rect.center));
                var uv = new Vector2(pixel.x / console.MainDisplay.Resolution.x, pixel.y / console.MainDisplay.Resolution.y);
                Assert.That(uv.x, Is.InRange(.05f, .95f), slot.ToString());
                Assert.That(uv.y, Is.InRange(.05f, .95f), slot.ToString());
                var world = glass.transform.TransformPoint(surface.LocalPoint(uv));
                var viewport = console.HardwareCamera.WorldToViewportPoint(world);
                Assert.That(viewport.z, Is.GreaterThan(0f), slot.ToString());
                Assert.AreEqual(button.gameObject, raycaster.HitAt(new Vector2(viewport.x, viewport.y)), slot.ToString());
            }

            foreach (var control in new[]
                     {
                         console.SelectorKnob, console.PreviousButton, console.NextButton, console.LaunchButton
                     })
            {
                var viewport = console.HardwareCamera.WorldToViewportPoint(control.GetComponent<Renderer>().bounds.center);
                Assert.AreEqual(control.gameObject, raycaster.HitAt(new Vector2(viewport.x, viewport.y)), control.name);
            }

            Assert.IsTrue(BodyMisses(console, raycaster), "a viewport point on the console body maps to nothing");
        }

        [UnityTest]
        public IEnumerator AClickOnEitherScreenArrow_StepsTheSelectedSlotsOption(
            [ValueSource(nameof(Views))] Vector2Int pixels)
        {
            var offer = AssetDatabase.LoadAssetAtPath<Game.GameHost>("Assets/Prefabs/Game/GameHost.prefab").hangarOffer;
            console = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<HangarConsole>(PrefabPath));
            console.Show(offer, new ShipLoadout(offer.starter, null, null), _ => true, null);
            var eventSystem = new GameObject("TestEventSystem").AddComponent<EventSystem>();
            eventSystemObject = eventSystem.gameObject;
            yield return null;

            console.Resize(pixels);
            console.MainDisplay.DisplayCamera.Render();
            Canvas.ForceUpdateCanvases();
            var raycaster = console.GetComponent<HangarConsoleRaycaster>();
            var view = console.GetComponentInChildren<HangarMainDisplayView>();
            foreach (HangarSlot slot in Enum.GetValues(typeof(HangarSlot)))
            {
                console.Selection.Select(slot);
                yield return null;
                console.Resize(pixels);
                console.MainDisplay.DisplayCamera.Render();
                Canvas.ForceUpdateCanvases();
                foreach (var step in new[] { 1, -1 })
                {
                    var arrow = view.Arrow(slot, step);
                    Assert.IsTrue(arrow.gameObject.activeInHierarchy, $"{slot} {step} arrow shows with its slot");
                    var before = Option(slot);
                    var hit = raycaster.HitAt(Viewport(arrow));
                    Assert.AreEqual(arrow.gameObject, hit, $"{slot} {step}");
                    var pointer = new PointerEventData(eventSystem) { button = PointerEventData.InputButton.Left };
                    ExecuteEvents.Execute(hit, pointer, ExecuteEvents.pointerClickHandler);
                    Assert.AreNotSame(before, Option(slot), $"{slot} {step} arrow steps the option");
                }
            }
        }

        private UnityEngine.Object Option(HangarSlot slot) => slot switch
        {
            HangarSlot.Ship => console.Selection.Ship,
            HangarSlot.Weapon => console.Selection.Weapon,
            _ => console.Selection.Ordnance
        };

        private Vector2 Viewport(Button button)
        {
            var rect = (RectTransform)button.transform;
            var pixel = console.MainDisplay.DisplayCamera.WorldToScreenPoint(rect.TransformPoint(rect.rect.center));
            var uv = new Vector2(pixel.x / console.MainDisplay.Resolution.x, pixel.y / console.MainDisplay.Resolution.y);
            var glass = console.MainScreenGlass;
            var surface = new MeshSurface(glass.GetComponent<MeshFilter>().sharedMesh);
            var viewport = console.HardwareCamera.WorldToViewportPoint(glass.transform.TransformPoint(surface.LocalPoint(uv)));
            return new Vector2(viewport.x, viewport.y);
        }

        private static bool BodyMisses(HangarConsole console, HangarConsoleRaycaster raycaster)
        {
            var body = console.GetComponentsInChildren<MeshRenderer>(true).SingleOrDefault(renderer => renderer.name == "ConsoleBody");
            Assert.IsTrue(body, "test premise: the console has a body");
            var mesh = body.GetComponent<MeshFilter>().sharedMesh;
            var surface = new MeshSurface(mesh);
            var vertices = mesh.vertices;
            var triangles = mesh.triangles;
            var camera = console.HardwareCamera;
            for (var i = 0; i < triangles.Length; i += 3)
            {
                var local = (vertices[triangles[i]] + vertices[triangles[i + 1]] + vertices[triangles[i + 2]]) / 3f;
                var world = body.transform.TransformPoint(local);
                var projected = camera.WorldToViewportPoint(world);
                if (projected.z <= 0f || projected.x < .05f || projected.x > .95f || projected.y < .05f || projected.y > .95f)
                    continue;
                var ray = camera.ViewportPointToRay(new Vector3(projected.x, projected.y, 0f));
                if (!surface.Raycast(ray, body.transform, out var distance, out _)) continue;
                if (Mathf.Abs(distance - Vector3.Distance(ray.origin, world)) > .05f) continue;
                if (raycaster.HitAt(new Vector2(projected.x, projected.y))) continue;
                return true;
            }
            return false;
        }
    }
}
#endif
