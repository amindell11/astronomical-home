using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Objectives;
using Objectives.Presentation;
using Substrate.Services.Objectives;
using Tests.PlayMode.Common;
using UI.Objectives;
using UI.PlayerState;
using UI.Prompts;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Tests.PlayMode.PlayerState
{
    [Category("UI")]
    public class ObjectivePresentationPlayModeTests
    {
        private GameObject root;
        private ObjectiveService service;
        private Canvas canvas;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Objective presentation");
            service = root.AddComponent<ObjectiveService>();
            canvas = Child("HUD").AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(root);

        private GameObject Child(string name)
        {
            var child = new GameObject(name, typeof(RectTransform));
            child.transform.SetParent(root.transform, false);
            return child;
        }

        private SpineObjectiveHandle Open(Transform target) => service.SetSpineObjective(
            new MissionDefinition("extract", new Dictionary<string, string>()),
            new Dictionary<string, Func<ObjectiveState>> { ["extract"] = () => new ExtractionState() }, target);

        [UnityTest]
        public IEnumerator Opening_FadesToStatus_AndReplaysForANewSectorOnly()
        {
            var readout = CreateReadout();
            readout.Initialize(service);
            var label = readout.GetComponentInChildren<Text>();
            var announcement = readout.transform.Find("Extraction announcement").GetComponent<Text>();
            var target = Child("Destination").transform;
            var handle = Open(target);
            yield return null;
            yield return new WaitForSecondsRealtime(.3f);
            Assert.AreEqual("EXTRACTION OPEN", announcement.text);
            Assert.IsTrue(announcement.enabled);
            Assert.AreEqual(new Vector2(.5f, .5f), announcement.rectTransform.anchorMin);
            Assert.AreEqual(1f, announcement.color.a, .01f);
            AssertShowsJump(readout, label);
            yield return new WaitForSecondsRealtime(2.2f);
            AssertShowsJump(readout, label, "Contextual interaction survives the announcement");
            Assert.IsFalse(announcement.enabled);
            handle.Target = Child("Replacement destination").transform;
            yield return null;
            AssertShowsJump(readout, label, "Retargeting cannot replay the opening");
            Open(target);
            yield return null;
            Assert.IsTrue(announcement.enabled, "A new sector opens without a blank frame between trackers");
            service.ClearAll();
            yield return null;
            Assert.IsEmpty(label.text);
            Assert.IsFalse(announcement.enabled);
        }

        private ObjectiveStatusReadout CreateReadout() => ObjectiveStatusReadout.Create(canvas.transform,
            TestAssets.Load<Font>("Assets/Visuals/Ui/Fonts/Rajdhani-Medium.ttf"),
            TestAssets.Load<Font>("Assets/Visuals/Ui/Fonts/Michroma.ttf"),
            TestAssets.Load<Font>("Assets/Visuals/Ui/Fonts/Orbitron-Medium.ttf"),
            TestAssets.Load<Font>("Assets/Visuals/Ui/Fonts/Orbitron-Medium.ttf"),
            TestAssets.Load<Material>("Assets/Visuals/Ui/Projection/CanopyText.mat"));

        private static void AssertShowsJump(ObjectiveStatusReadout readout, Text replaced, string message = null)
        {
            var prompt = readout.GetComponentInChildren<KeyPrompt>();
            Assert.IsTrue(prompt, message);
            Assert.AreEqual("F", prompt.Key, message);
            Assert.AreEqual("JUMP", prompt.Action, message);
            Assert.IsFalse(replaced.enabled, message);
        }

        [UnityTest]
        public IEnumerator PhaseDisplay_UsesTheSchedule_AndRebindsAfterExtraction()
        {
            var phase = new SectorPhaseProgress(1, 4, 35f, false, caption: PhaseCaption.NextWave, captionSeconds: 8f);
            var readout = CreateReadout();
            readout.Initialize(service);
            var target = Child("Destination").transform;
            OpenPhases(target, () => phase);
            yield return null;
            var clock = readout.transform.Find("Phase display/Phase clock").GetComponent<Text>();
            var caption = readout.transform.Find("Phase display/Phase caption").GetComponent<Text>();
            var label = readout.transform.Find("Phase display/Label").GetComponent<Text>();
            foreach (var text in readout.GetComponentsInChildren<Text>())
                Assert.That(text.material, Is.SameAs(TestAssets.Load<Material>("Assets/Visuals/Ui/Projection/CanopyText.mat")));
            Assert.AreEqual("0:35", clock.text);
            Assert.AreEqual("Orbitron-Medium", clock.font.name);
            Assert.AreEqual("NEXT WAVE 0:08", caption.text);
            Assert.Greater(readout.transform.Find("Phase display/Sector phase 2").GetComponent<RectTransform>().sizeDelta.x,
                readout.transform.Find("Phase display/Sector phase 1").GetComponent<RectTransform>().sizeDelta.x);
            Assert.AreEqual(.32f, readout.transform.Find("Phase display/Sector phase 4").GetComponent<Image>().color.a);
            phase = new SectorPhaseProgress(3, 4, .5f, false, caption: PhaseCaption.Lull, captionSeconds: 14f);
            yield return null;
            Assert.AreEqual("0:01", clock.text);
            Assert.AreEqual("LULL 0:14", caption.text);
            phase = new SectorPhaseProgress(3, 4, 0f, true);
            yield return null;
            yield return new WaitForSecondsRealtime(.3f);
            var announcement = readout.transform.Find("Extraction announcement").GetComponent<Text>();
            Assert.IsTrue(announcement.enabled);
            Assert.AreEqual("Michroma", announcement.font.name);
            yield return new WaitForSecondsRealtime(2.2f);
            Assert.IsFalse(clock.enabled);
            Assert.AreEqual("EXTRACTION OPEN", label.text);
            Assert.AreEqual("Rajdhani-Medium", label.font.name);
            AssertShowsJump(readout, caption);
            Assert.IsFalse(announcement.enabled);
            service.ClearAll();
            yield return null;
            Assert.IsEmpty(label.text);
            Assert.IsFalse(caption.enabled);
            phase = new SectorPhaseProgress(0, 1, 60f, false, caption: PhaseCaption.Extraction);
            OpenPhases(target, () => phase);
            yield return null;
            yield return null;
            Assert.AreEqual("1:00", clock.text);
            Assert.AreEqual("EXTRACTION", caption.text);
            phase = new SectorPhaseProgress(0, 1, 60f, false, caption: PhaseCaption.Elite);
            yield return null;
            Assert.AreEqual("ELITE", caption.text);
            Assert.IsNull(readout.transform.Find("Phase display/Sector phase 2"));
            phase = new SectorPhaseProgress(-1, 1, 44.2f, false, HuntReading.Searching);
            yield return null;
            Assert.AreEqual("0:45", clock.text);
            Assert.AreEqual("SEARCHING", caption.text, "The hunt's search clock reads as the search.");
            phase = new SectorPhaseProgress(-1, 1, 2.1f, false, HuntReading.Spotted, .4f);
            yield return null;
            Assert.AreEqual("0:03", clock.text);
            Assert.AreEqual("SPOTTED", caption.text, "A filling meter reads as spotted.");
            phase = new SectorPhaseProgress(-1, 1, 4.2f, false, HuntReading.Coming);
            yield return null;
            Assert.AreEqual("0:05", clock.text);
            Assert.AreEqual("INCOMING", caption.text, "The gap after the alarm reads as incoming.");
        }

        private SpineObjectiveHandle OpenPhases(Transform target, Func<SectorPhaseProgress> progress) =>
            service.SetSpineObjective(
                new MissionDefinition("survive", new Dictionary<string, string> { ["survive"] = "extract" }),
                new Dictionary<string, Func<ObjectiveState>>
                {
                    ["survive"] = () => new SurvivalState(() => progress().ExtractionOpen),
                    ["extract"] = () => new ExtractionState()
                }, target, progress);

        [UnityTest]
        public IEnumerator HuntBanners_HoldSpottedOverItsBar_ClearWhenTheMeterEmpties_AndHoldComingThroughTheGap()
        {
            var phase = new SectorPhaseProgress(-1, 2, 40f, false, HuntReading.Searching);
            var readout = CreateReadout();
            readout.Initialize(service);
            var announcement = readout.transform.Find("Extraction announcement").GetComponent<Text>();
            var bar = readout.transform.Find("Extraction announcement/Spotted bar").GetComponent<Image>();
            OpenPhases(null, () => phase);
            yield return null;
            Assert.IsFalse(announcement.enabled, "Searching announces nothing.");
            Assert.IsFalse(bar.enabled);

            phase = new SectorPhaseProgress(-1, 2, 40f, false, HuntReading.Spotted, .25f);
            yield return null;
            yield return new WaitForSecondsRealtime(2.5f);
            Assert.IsTrue(announcement.enabled, "SPOTTED holds for as long as the meter is up.");
            Assert.AreEqual("SPOTTED", announcement.text);
            Assert.AreEqual(1f, announcement.color.a, .01f);
            Assert.Greater(announcement.color.r, announcement.color.g, "SPOTTED reads red.");
            Assert.IsTrue(bar.enabled, "The spotted banner shows its meter.");
            Assert.Greater(bar.color.r, bar.color.g);
            var quarter = bar.rectTransform.sizeDelta.x;
            Assert.Greater(quarter, 0f);

            phase = new SectorPhaseProgress(-1, 2, 40f, false, HuntReading.Spotted, 1f);
            yield return null;
            Assert.AreEqual(quarter * 4f, bar.rectTransform.sizeDelta.x, .05f, "The bar width is the fill.");

            phase = new SectorPhaseProgress(-1, 2, 30f, false, HuntReading.Searching);
            yield return null;
            Assert.IsFalse(announcement.enabled, "An empty meter clears the banner at once.");
            Assert.IsFalse(bar.enabled);

            phase = new SectorPhaseProgress(-1, 2, 4f, false, HuntReading.Coming);
            yield return null;
            yield return new WaitForSecondsRealtime(2.5f);
            Assert.IsTrue(announcement.enabled, "THEY'RE COMING holds through the gap.");
            Assert.AreEqual("THEY'RE COMING", announcement.text);
            Assert.AreEqual(Color.white, new Color(announcement.color.r, announcement.color.g, announcement.color.b));
            Assert.IsFalse(bar.enabled);

            phase = new SectorPhaseProgress(0, 2, 30f, false);
            yield return null;
            yield return new WaitForSecondsRealtime(.3f);
            Assert.IsTrue(announcement.enabled);
            Assert.AreEqual("THEY'RE COMING", announcement.text);
            yield return new WaitForSecondsRealtime(2.2f);
            Assert.IsFalse(announcement.enabled, "The alarm banner fades like the other announcements.");

            phase = new SectorPhaseProgress(-1, 2, 40f, false, HuntReading.Searching);
            yield return null;
            service.ClearAll();
            OpenPhases(null, () => new SectorPhaseProgress(0, 2, 30f, false));
            yield return null;
            Assert.IsFalse(announcement.enabled, "A new tracker never reads as the alarm.");
        }

        [UnityTest]
        public IEnumerator Readout_AnnouncesABeacon_WhenASurviveTargetAppears_AndReplaysForTheNext()
        {
            var readout = CreateReadout();
            readout.Initialize(service);
            var announcement = readout.transform.Find("Extraction announcement").GetComponent<Text>();
            var handle = OpenPhases(null, () => new SectorPhaseProgress(0, 2, 20f, false));
            yield return null;
            Assert.IsFalse(announcement.enabled, "Surviving with no beacon announces nothing.");
            var beacon = Child("Beacon").transform;
            handle.Target = beacon;
            yield return null;
            yield return new WaitForSecondsRealtime(.3f);
            Assert.IsTrue(announcement.enabled);
            Assert.AreEqual("MOD BEACON", announcement.text);
            yield return new WaitForSecondsRealtime(2.2f);
            Assert.IsFalse(announcement.enabled);
            handle.Target = null;
            yield return null;
            handle.Target = beacon;
            yield return null;
            yield return new WaitForSecondsRealtime(.3f);
            Assert.IsTrue(announcement.enabled, "The next beacon announces again.");
        }

        private (ObjectiveMarkers markers, Image icon, Camera camera, Sprite exit, Sprite lull) CreateMarkers()
        {
            var camera = Child("World camera").AddComponent<Camera>();
            camera.transform.position = new Vector3(0, 0, -50);
            camera.orthographic = true;
            camera.orthographicSize = 18;
            camera.aspect = 1f;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1;
            var minimap = (RectTransform)Child("Minimap").transform;
            minimap.SetParent(canvas.transform, false);
            minimap.anchorMin = minimap.anchorMax = Vector2.one;
            minimap.anchoredPosition = new Vector2(-120, -120);
            minimap.sizeDelta = new Vector2(200, 200);
            var icon = Child("Legacy objective").AddComponent<Image>();
            var markers = Child("Markers").AddComponent<ObjectiveMarkers>();
            var exit = Sprite.Create(new Texture2D(4, 4), new Rect(0, 0, 4, 4), Vector2.one * .5f);
            var lull = Sprite.Create(new Texture2D(4, 4), new Rect(0, 0, 4, 4), Vector2.one * .5f);
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(ObjectiveMarkers).GetField("icon", flags).SetValue(markers, icon);
            typeof(ObjectiveMarkers).GetField("exitIcon", flags).SetValue(markers, exit);
            typeof(ObjectiveMarkers).GetField("lullIcon", flags).SetValue(markers, lull);
            markers.Initialize(service, camera, camera, minimap, canvas);
            return (markers, icon, camera, exit, lull);
        }

        [UnityTest]
        public IEnumerator Markers_StaySmallAcrossZoom_AndHideWhenTheTargetIsDestroyed()
        {
            var (_, icon, camera, _, _) = CreateMarkers();
            var target = Child("Destination").transform;
            target.position = new Vector3(8, 6, 0);
            Open(target);
            yield return null;
            var screen = canvas.transform.Find("Extraction direction").GetComponent<Image>();
            var map = canvas.transform.Find("Extraction minimap").GetComponent<Image>();
            Assert.IsTrue(screen.enabled && map.enabled);
            Assert.IsFalse(icon.enabled);
            var size = screen.rectTransform.rect.size;
            camera.orthographicSize = 60;
            yield return null;
            Assert.AreEqual(size, screen.rectTransform.rect.size);
            Assert.AreEqual(Vector3.one, screen.rectTransform.localScale);
            Assert.Less(map.rectTransform.rect.width, screen.rectTransform.rect.width);
            Assert.AreEqual(Quaternion.identity, map.rectTransform.localRotation);
            Assert.AreEqual(Quaternion.identity, screen.rectTransform.localRotation);
            var mapPosition = map.rectTransform.anchoredPosition;
            camera.transform.position += new Vector3(10, 7, 0);
            target.position += new Vector3(10, 7, 0);
            yield return null;
            Assert.That(Vector2.Distance(mapPosition, map.rectTransform.anchoredPosition), Is.LessThan(.1f),
                "Moving the camera and destination together cannot shift the icon inside the minimap");
            Object.Destroy(target.gameObject);
            yield return null;
            yield return null;
            Assert.IsFalse(screen.enabled || map.enabled || icon.enabled);
        }

        [UnityTest]
        public IEnumerator Markers_InSurvive_ShowTheLullBeacon()
        {
            var (markers, icon, _, _, lull) = CreateMarkers();
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var lullColour = (Color)typeof(ObjectiveMarkers).GetField("lullColour", flags).GetValue(markers);
            var exitColour = (Color)typeof(ObjectiveMarkers).GetField("exitColour", flags).GetValue(markers);
            var target = Child("Beacon").transform;
            target.position = new Vector3(8, 6, 0);
            OpenPhases(target, () => new SectorPhaseProgress(0, 2, 5f, false));
            yield return null;
            var screen = canvas.transform.Find("Extraction direction").GetComponent<Image>();
            var map = canvas.transform.Find("Extraction minimap").GetComponent<Image>();
            Assert.IsTrue(screen.enabled && map.enabled, "A survive-state target shows the lull beacon.");
            Assert.AreSame(lull, screen.sprite);
            Assert.AreSame(lull, map.sprite);
            Assert.AreEqual(lullColour, screen.color);
            Assert.AreEqual(lullColour, map.color);
            Assert.AreNotEqual(lullColour, exitColour);
            Assert.IsFalse(icon.enabled);
        }

        [UnityTest]
        public IEnumerator BeaconBerths_RingTracksTheGraceLeft_AndBlinkWhileExpiring()
        {
            CreateMarkers();
            var target = Child("Beacon").transform;
            target.position = new Vector3(8, 6, 0);
            var phase = new SectorPhaseProgress(0, 2, 5f, false, beaconFill: .4f);
            OpenPhases(target, () => phase);
            yield return null;
            var screen = canvas.transform.Find("Extraction direction").GetComponent<Image>();
            var map = canvas.transform.Find("Extraction minimap").GetComponent<Image>();
            var screenRing = screen.GetComponentInChildren<NavRing>(true);
            var mapRing = map.GetComponentInChildren<NavRing>(true);
            var until = Time.realtimeSinceStartup + .6f;
            while (Time.realtimeSinceStartup < until)
            {
                Assert.AreEqual(.4f, screenRing.Fill);
                Assert.AreEqual(.4f, mapRing.Fill);
                Assert.AreEqual(1f, screen.canvasRenderer.GetAlpha());
                Assert.AreEqual(1f, map.canvasRenderer.GetAlpha());
                yield return null;
            }

            phase = new SectorPhaseProgress(0, 2, 5f, false, beaconFill: .25f);
            yield return null;
            Assert.AreEqual(.25f, screenRing.Fill);
            Assert.AreEqual(.25f, mapRing.Fill);

            phase = new SectorPhaseProgress(0, 2, 5f, false, beaconFill: .25f, beaconExpiring: true);
            var sawLit = false;
            var sawDark = false;
            until = Time.realtimeSinceStartup + .6f;
            while (Time.realtimeSinceStartup < until)
            {
                yield return null;
                var screenAlpha = screen.canvasRenderer.GetAlpha();
                var mapAlpha = map.canvasRenderer.GetAlpha();
                sawLit |= screenAlpha == 1f && mapAlpha == 1f;
                sawDark |= screenAlpha == 0f && mapAlpha == 0f;
            }

            Assert.IsTrue(sawLit);
            Assert.IsTrue(sawDark);
            Open(target);
            yield return null;
            Assert.IsNull(screenRing.Fill);
            Assert.IsNull(mapRing.Fill);
            Assert.AreEqual(1f, screen.canvasRenderer.GetAlpha());
            Assert.AreEqual(1f, map.canvasRenderer.GetAlpha());
        }

        [UnityTest]
        public IEnumerator Markers_BeyondMinimapRange_PinTheBeaconToTheRimWithATab()
        {
            CreateMarkers();
            var target = Child("Destination").transform;
            target.position = new Vector3(400, 300, 0);
            Open(target);
            yield return null;
            var map = canvas.transform.Find("Extraction minimap").GetComponent<Image>();
            var tab = map.GetComponentInChildren<NavTab>();
            var direction = new Vector2(400, 300).normalized;
            Assert.IsTrue(tab.enabled, "A beacon beyond minimap range shows its tab.");
            Assert.Less(Vector2.Angle(tab.rectTransform.localRotation * Vector3.up, direction), 1f,
                "The tab aims at the beacon.");
            target.position = new Vector3(2, 1, 0);
            yield return null;
            Assert.IsFalse(tab.enabled, "In minimap range the beacon has no tab.");
        }

        private sealed class ExtractionState : ObjectiveState
        {
            public override ObjectiveType StateType => ObjectiveType.ExtractionChallenge;
            public override bool IsComplete => false;
            public override InteractionPrompt? Prompt => new InteractionPrompt("F", "JUMP");
            public override void Tick(float deltaTime) { }
        }

        private sealed class SurvivalState : ObjectiveState
        {
            private readonly Func<bool> complete;
            public SurvivalState(Func<bool> complete) => this.complete = complete;
            public override ObjectiveType StateType => ObjectiveType.Survive;
            public override bool IsComplete => complete();
            public override void Tick(float deltaTime) { }
        }
    }
}
