using System;
using System.Collections;
using System.Collections.Generic;
using Cameras;
using Game.Travel;
using Ships;
using Substrate;
using Substrate.Services.Locales;
using UI;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Preview
{
    /// <summary>
    /// The sector jump as a preview subject: it plays the game's own <see cref="SectorJumpPassage"/>,
    /// whose hyperspace hold lasts the transport's hold, through the game's presentation. Like the
    /// game's flight it owns the ship and camera pose while composed; the simulation stays frozen, so
    /// only the sky carries the arrival coast.
    /// </summary>
    public sealed class SectorJumpPreviewSubject : IPreviewSubject
    {
        private static readonly string[] Names =
        {
            "Blue to copper, diagonal", "Blue to copper, upward", "Blue to copper, left",
            "Copper to blue, diagonal", "Blue to copper, alternate stars"
        };
        private readonly SectorJumpFeature feature;
        private readonly LocaleSky destination;
        private readonly SectorJumpProfile profile;
        private Ship ship;
        private ObserverCam observer;
        private Overlay overlay;
        private IEnumerable<Ship> ships;
        private Action<bool> freezeSimulation;
        private LocaleBackdropPair backdrop;
        private SectorJumpPresentation presentation;
        private Vector3 origin;
        private Vector3 startCamera;
        private Quaternion startRotation;
        private float startHeading;
        private bool observerEnabled;
        private bool commanderEnabled;
        private SectorJumpPassage passage;
        private SectorJumpMotion motion;
        private int revision = -1;

        public SectorJumpPreviewSubject(SectorJumpProfile profile, SectorJumpFeature feature, LocaleSky destination)
        {
            if (!profile || !feature || !destination)
                throw new InvalidOperationException(
                    "The jump preview requires a feel profile and jump feature, and a destination locale.");
            this.profile = profile;
            this.feature = feature;
            this.destination = destination;
        }

        public string Name => "Sector jump";
        public bool Scrubs => true;
        public IReadOnlyList<string> Fixtures => Names;
        public int Fixture { get; private set; }
        public int SpanCount => 2;
        public SectorJumpState State { get; private set; }

        public IEnumerator Compose(PreviewWorld world)
        {
            ship = world.Rig.Player;
            observer = world.Observer;
            overlay = world.Rig.Overlay;
            ships = world.Session.Units.ActiveRegistry.ActiveShips;
            freezeSimulation = world.FreezeSimulation;
            if (!ship.gameObject.activeInHierarchy)
                world.Session.Units.RespawnShip(ship.Id, GamePlane.WorldPointToPlane(ship.transform.position), 0f);
            freezeSimulation(true);
            backdrop = world.Session.PrepareTravelBackdrop(destination, world.Root);
            origin = ship.transform.position;
            startRotation = ship.transform.rotation;
            startHeading = Vector3.SignedAngle(GamePlane.Forward,
                Vector3.ProjectOnPlane(ship.transform.up, GamePlane.Normal), GamePlane.Normal);
            startCamera = observer.transform.position;
            observerEnabled = observer.enabled;
            commanderEnabled = ship.Commander.enabled;
            observer.enabled = false;
            ship.Commander.enabled = false;
            presentation = SectorJumpPresentation.Create(world.Ui, ship, overlay, ships, observer, backdrop, feature);
            yield break;
        }

        public PreviewSpan Span(int index)
        {
            var timeline = Passage();
            return index switch
            {
                0 => new PreviewSpan("Departure", timeline.HoldStartsAt, true),
                1 => new PreviewSpan("Arrival", timeline.DurationWithHold(0f) - timeline.HoldStartsAt, false),
                _ => throw new ArgumentOutOfRangeException(nameof(index))
            };
        }

        public void SelectFixture(int index)
        {
            if (index < 0 || index >= Names.Length) throw new ArgumentOutOfRangeException(nameof(index));
            Fixture = index;
            revision = -1;
        }

        public void Show(int span, float seconds, float holdSeconds)
        {
            var timeline = Passage();
            var exitStartsAt = timeline.ExitStartsAt(holdSeconds);
            var elapsed = span == 0 ? seconds : exitStartsAt + seconds;
            State = timeline.StateAt(elapsed, holdSeconds);
            var rotation = GamePlane.Rotation * Quaternion.AngleAxis(State.HeadingDegrees, Vector3.forward);
            ship.transform.rotation = rotation;
            ship.Body.rotation = rotation;
            observer.transform.position = Vector3.Lerp(startCamera, new Vector3(origin.x, origin.y, startCamera.z),
                SectorJumpProfile.Ease(elapsed / profile.turnDuration));
            presentation.SetWorldBlend(timeline.WorldBlendAt(elapsed, holdSeconds));
            var coast = motion.ArrivalDistanceAt(elapsed - exitStartsAt);
            presentation.Apply(profile, State, Fixture == 4 ? 417 : SectorJumpPresentation.StreakSeed, Fixture == 3,
                origin + GamePlane.PlaneDirToWorld(State.JumpDirection) * coast);
        }

        private SectorJumpPassage Passage()
        {
            if (revision == profile.Revision) return passage;
            var heading = Fixture == 1 ? 0f : Fixture == 2 ? 90f : -35f;
            passage = new SectorJumpPassage(profile, startHeading, heading);
            motion = new SectorJumpMotion(profile, ship.MaxSpeed);
            revision = profile.Revision;
            return passage;
        }

        public void Dispose()
        {
            Object.DestroyImmediate(presentation.gameObject);
            Object.DestroyImmediate(backdrop.gameObject);
            overlay.SetOpacity(1f);
            foreach (var active in ships)
                if (active && active.VisualRig) active.VisualRig.SetHudOpacity(1f);
            ship.transform.rotation = startRotation;
            ship.Body.rotation = startRotation;
            ship.Commander.enabled = commanderEnabled;
            observer.transform.position = startCamera;
            observer.enabled = observerEnabled;
            freezeSimulation(false);
        }
    }
}
