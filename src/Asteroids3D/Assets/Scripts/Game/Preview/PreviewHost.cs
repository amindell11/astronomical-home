using System;
using System.Collections;
using Cameras;
using Game.Travel;
using Substrate.Services.Locales;
using Substrate.Services.Objectives;
using Substrate.Services.Units;
using Substrate.Sessions;
using UI.Recap;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace Game.Preview
{
    public enum PreviewSubjectKind { RunRecap, SectorJump }

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
        [SerializeField] internal SectorJumpProfile jumpFeel;
        [SerializeField] internal Shader jumpShader;
        [SerializeField] internal PreviewSubjectKind subjectKind;

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
        public PreviewSubjectKind SubjectKind => subjectKind;

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

            yield return ComposeSubject();
            Transport.Restart();
        }

        public void SelectSubject(PreviewSubjectKind kind)
        {
            if (Subject == null) throw new InvalidOperationException("The preview is still composing.");
            Subject.Dispose();
            Subject = null;
            subjectKind = kind;
            Transport.Playing = false;
            Transport.Restart();
            StartCoroutine(ComposeSubject());
        }

        private IEnumerator ComposeSubject()
        {
            if (subjectKind == PreviewSubjectKind.RunRecap)
            {
                Subject = new RecapPreviewSubject(ui, rig, session.Units, observer, recapFeel, SetSimulationFrozen);
                yield break;
            }
            if (!jumpFeel || !jumpShader)
                throw new InvalidOperationException("The jump preview requires a feel profile and shader.");
            SetSimulationFrozen(true);
            LocaleBackdropPair backdrop = null;
            yield return session.PrepareTravelBackdrop("Locale_1", transform, pair => backdrop = pair);
            var presentation = SectorJumpPresentation.Create(ui, rig.Player, rig.Overlay,
                session.Units.ActiveRegistry.ActiveShips, observer, backdrop, jumpShader);
            Subject = new SectorJumpPreviewSubject(jumpFeel, presentation, () =>
            {
                DestroyImmediate(presentation.gameObject);
                DestroyImmediate(backdrop.gameObject);
                SetSimulationFrozen(false);
            });
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

        private void OnDestroy()
        {
            Subject?.Dispose();
            SetSimulationFrozen(false);
        }
    }
}
