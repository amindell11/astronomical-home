using System;
using System.Collections;
using Cameras;
using Substrate.Services.Objectives;
using Substrate.Services.Units;
using Substrate.Sessions;
using UI.Recap;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace Game.Preview
{
    /// <summary>
    /// Play-mode composition root for showing a presentation inside the real game without a run: it
    /// composes a <see cref="Session"/> with presentation on, loads the profile's first sector around the
    /// real player rig, observer camera and HUD, and hosts one <see cref="IPreviewSubject"/> under a
    /// <see cref="PreviewTransport"/>. Like <see cref="GameHost"/> it owns the clock a subject may
    /// freeze. An edit-mode bootstrap or a capture scenario sets the inputs before it activates.
    /// https://github.com/amindell11/astronomical-home/issues/1151
    /// </summary>
    [RequireComponent(typeof(UnitService))]
    [RequireComponent(typeof(ObjectiveService))]
    public sealed class PreviewHost : MonoBehaviour
    {
        [SerializeField] internal SessionProfile sessionProfile;
        [SerializeField] internal PlayerRig playerRigPrefab;
        [SerializeField] internal ObserverCam observerCamPrefab;
        [SerializeField] internal RecapFeelProfile recapFeel;

        private UnitService unitService;
        private ObjectiveService objectiveService;
        private Session session;
        private ObserverCam observer;
        private PlayerRig rig;
        private Transform viewport;
        private Transform ui;
        private Transform arena;
        private float? frozenTimeScale;

        public PreviewTransport Transport { get; } = new();

        /// <summary>Null until the session, the rig and the sector are up.</summary>
        public IPreviewSubject Subject { get; private set; }

        public ObserverCam Observer => observer;

        private void Awake()
        {
            unitService = GetComponent<UnitService>();
            objectiveService = GetComponent<ObjectiveService>();
            viewport = NewRoot("Viewport");
            ui = NewRoot("UI");
            arena = NewRoot("Arena");
            DontDestroyOnLoad(gameObject);
            StartCoroutine(Compose());
        }

        private IEnumerator Compose()
        {
            if (!sessionProfile.presentation)
                throw new InvalidOperationException("The preview host shows presentation; its profile turns it off.");
            if (!recapFeel)
                throw new InvalidOperationException("The preview host has no recap feel profile assigned.");
            session = new Session(sessionProfile, arena, unitService, objectiveService);
            var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            eventSystem.transform.SetParent(ui, false);
            yield return session.Compose();

            observer = Instantiate(observerCamPrefab, viewport);
            var registry = session.Units.ActiveRegistry;
            registry.ActiveShips.OnAdd += ship => observer.AddSecondarySubject(ship.transform);
            registry.ActiveShips.OnRemove += ship => observer.RemoveSecondarySubject(ship.transform);

            rig = Instantiate(playerRigPrefab, transform);
            yield return rig.Build(session.Units, session.Objectives, true, observer, ui, session.Frame, null);
            rig.ApplyLoadout();
            yield return session.LoadSector(0, rig.Player);

            Subject = new RecapPreviewSubject(ui, rig, session.Units, observer, recapFeel, SetSimulationFrozen);
            Transport.Restart();
        }

        private void LateUpdate()
        {
            if (Subject == null) return;
            Transport.Tick(Time.unscaledDeltaTime, Subject);
        }

        private Transform NewRoot(string name)
        {
            var root = new GameObject(name).transform;
            root.SetParent(transform, false);
            return root;
        }

        private void SetSimulationFrozen(bool frozen)
        {
            if (frozen)
            {
                frozenTimeScale ??= Time.timeScale;
                Time.timeScale = 0f;
                return;
            }
            if (!frozenTimeScale.HasValue) return;
            Time.timeScale = frozenTimeScale.Value;
            frozenTimeScale = null;
        }

        private void OnDestroy() => SetSimulationFrozen(false);
    }
}
