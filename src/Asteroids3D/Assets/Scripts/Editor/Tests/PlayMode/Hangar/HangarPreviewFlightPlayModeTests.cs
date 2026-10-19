using System.Collections;
using System.Linq;
using NUnit.Framework;
using Ships;
using Ships.Loadout;
using UI.Hangar;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.PlayMode.Hangar
{
    [Category("UI"), Category("RequiresGraphics")]
    public sealed class HangarPreviewFlightPlayModeTests
    {
        private HangarPreviewStage stage;
        private Ship valis;
        private Ship nightshade;

        [SetUp]
        public void SetUp()
        {
            valis = AssetDatabase.LoadAssetAtPath<Ship>("Assets/Prefabs/Ships/Valis.prefab");
            nightshade = AssetDatabase.LoadAssetAtPath<Ship>("Assets/Prefabs/Ships/Nightshade.prefab");
            stage = HangarPreviewStage.Create(null, 256, new[] { valis, nightshade });
        }

        [TearDown]
        public void TearDown() => Object.Destroy(stage.gameObject);

        [UnityTest]
        public IEnumerator Arrival_FacesViewer_AndUsesValisWingSweep()
        {
            Show(valis);
            var flight = stage.GetComponentInChildren<HangarPreviewFlight>();
            var wing = flight.GetComponentsInChildren<Transform>(true).Single(t => t.name == "Upper wing right");
            var start = flight.transform.localPosition;
            yield return new WaitForSeconds(.65f);
            Assert.That(Quaternion.Angle(Quaternion.identity, wing.localRotation), Is.LessThan(.1f));
            yield return new WaitForSeconds(1.15f);
            Assert.That(Quaternion.Angle(Quaternion.identity, wing.localRotation), Is.EqualTo(20f).Within(.1f));
            Assert.That(flight.transform.localPosition.magnitude, Is.LessThan(start.magnitude * .05f));
            Assert.That(flight.transform.localScale, Is.EqualTo(valis.VisualRig.transform.lossyScale));
            var camera = stage.GetComponentInChildren<Camera>();
            Assert.That(Vector3.Dot(flight.transform.up, -camera.transform.forward), Is.GreaterThan(.75f));
            var settled = flight.transform.localRotation;
            yield return new WaitForSeconds(.7f);
            Assert.That(Quaternion.Angle(settled, flight.transform.localRotation), Is.InRange(.5f, 12f));
        }

        [UnityTest]
        public IEnumerator Switch_PeelsLeft_AndJoinsFromRight_WithoutMovingCamera()
        {
            Show(valis);
            yield return new WaitForSeconds(1.8f);
            var outgoing = stage.GetComponentInChildren<HangarPreviewFlight>().transform;
            var start = outgoing.localPosition;
            var camera = stage.GetComponentInChildren<Camera>();
            var cameraPosition = camera.transform.position;
            Show(nightshade);
            var incoming = stage.GetComponentsInChildren<HangarPreviewFlight>().Single(f => f.transform != outgoing);
            Assert.That(outgoing.localPosition, Is.EqualTo(start), "handoff starts at the displayed pose");
            Assert.That(incoming.transform.localPosition.x, Is.GreaterThan(start.x));
            var right = incoming.transform.localPosition.x;
            yield return new WaitForSeconds(.35f);
            Assert.That(outgoing.localPosition.x, Is.LessThan(start.x));
            Assert.That(outgoing.localPosition.z, Is.GreaterThan(start.z));
            Assert.That(incoming.transform.localPosition.x, Is.LessThan(right));
            Assert.That(camera.transform.position, Is.EqualTo(cameraPosition));
            yield return new WaitForSeconds(1f);
            Assert.That(outgoing == null, Is.True);
            Assert.That(stage.GetComponentsInChildren<HangarPreviewFlight>(), Has.Length.EqualTo(1));
            Assert.That(incoming.transform.localScale, Is.EqualTo(nightshade.VisualRig.transform.lossyScale));
        }

        [UnityTest]
        public IEnumerator RapidSelections_KeepOnlyLatestPair_AndSettleOnLastShip()
        {
            foreach (var ship in new[] { valis, nightshade, valis, nightshade, valis })
            {
                Show(ship);
                yield return new WaitForSeconds(.1f);
                Assert.That(stage.GetComponentsInChildren<HangarPreviewFlight>().Length, Is.LessThanOrEqualTo(2));
            }
            yield return new WaitForSeconds(1.4f);
            var flights = stage.GetComponentsInChildren<HangarPreviewFlight>();
            Assert.That(flights, Has.Length.EqualTo(1));
            Assert.That(flights[0].name, Is.EqualTo(valis.name + "_Preview"));
            var camera = stage.GetComponentInChildren<Camera>();
            var center = camera.WorldToViewportPoint(flights[0].transform.position);
            Assert.That(center.x, Is.InRange(.45f, .55f));
            Assert.That(center.y, Is.InRange(.45f, .55f));
        }

        private void Show(Ship ship) => stage.Show(new ShipLoadout(ship, null, null, null, null));
    }
}
