using System;
using System.Collections;
using System.IO;
using System.Runtime.ExceptionServices;
using Cameras;
using Game.Runs;
using Game.Travel;
using Ships;
using Substrate.Results;
using Substrate.Services.Objectives;
using Substrate.Services.Units;
using Substrate.Sessions;
using UI.Recap;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using Utils.Coroutines;

namespace Game.Preview
{
    public enum PreviewSubjectKind { RunRecap, SectorJump }
    public enum PreviewMode { Inspection, Playtest }
    public enum PreviewPlaytestSetup { Combat, CrowdedJumpGate }

    /// <summary>
    /// Inspection owns a session and presentation subject driven by a transport; Playtest owns a
    /// real GameHost and its isolated run records. Replacement awaits the previous mode's teardown
    /// before composing the next. Only inspection subjects may freeze this host's clock.
    /// A throw while composing, replacing or tearing down faults the host: every later request refuses,
    /// Shutdown rethrows instead of tearing down, and only destroying the host recovers.
    /// https://github.com/amindell11/astronomical-home/issues/1151
    /// </summary>
    [RequireComponent(typeof(UnitService))]
    [RequireComponent(typeof(ObjectiveService))]
    public sealed class PreviewHost : MonoBehaviour
    {
        [SerializeField] internal SessionProfile sessionProfile;
        [SerializeField] internal SessionProfile jumpPlaytestProfile;
        [SerializeField] internal PlayerRig playerRigPrefab;
        [SerializeField] internal ObserverCam observerCamPrefab;
        [SerializeField] internal RecapFeelProfile recapFeel;
        [SerializeField] internal SectorJumpProfile jumpFeel;
        [SerializeField] internal Shader jumpShader;
        [SerializeField] internal PreviewSubjectKind subjectKind;
        [SerializeField] internal PreviewMode mode;
        [SerializeField] internal PreviewPlaytestSetup playtestSetup;

        private UnitService unitService;
        private ObjectiveService objectiveService;
        private Session session;
        private ObserverCam observer;
        private PlayerRig rig;
        private Transform viewport;
        private Transform ui;
        private Transform arena;
        private float? frozenTimeScale;
        private GameHost playtest;
        private bool composing;
        private bool replacing;
        private bool closing;
        private bool closed;
        private ExceptionDispatchInfo fault;

        public PreviewTransport Transport { get; } = new();

        public IPreviewSubject Subject { get; private set; }

        public ObserverCam Observer => observer;
        public PreviewSubjectKind SubjectKind => subjectKind;
        public PreviewMode Mode => mode;
        public PreviewPlaytestSetup PlaytestSetup => playtestSetup;
        public GameHost Playtest => playtest;
        public RecapFeelProfile RecapFeel => recapFeel;
        public SectorJumpProfile JumpFeel => jumpFeel;
        public bool IsReplacing => replacing || closing;
        public Exception Fault => fault?.SourceException;
        public bool IsReady => fault == null && !composing && !IsReplacing &&
            (mode == PreviewMode.Inspection ? Subject != null : playtest && playtest.ActiveSector);

        private void Awake()
        {
            unitService = GetComponent<UnitService>();
            objectiveService = GetComponent<ObjectiveService>();
            RequireMode(mode);
            RequirePlaytestSetup(playtestSetup);
            if (!sessionProfile.presentation)
                throw new InvalidOperationException("The preview host requires presentation to be enabled.");
            DontDestroyOnLoad(gameObject);
            composing = true;
            StartCoroutine(CatchingRoutine.Run(ComposeMode(mode), Faulted));
        }

        private IEnumerator ComposeMode(PreviewMode selected)
        {
            if (selected == PreviewMode.Inspection)
                yield return ComposeInspection();
            else
                BuildPlaytest();
            composing = false;
        }

        private IEnumerator ComposeInspection()
        {
            viewport = NewRoot("Viewport");
            ui = NewRoot("UI");
            arena = NewRoot("Arena");
            session = new Session(sessionProfile, arena, unitService, objectiveService);
            var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            eventSystem.transform.SetParent(ui, false);
            yield return session.Compose();

            observer = Instantiate(observerCamPrefab, viewport);
            var registry = session.Units.ActiveRegistry;
            registry.ActiveShips.OnAdd += AddCameraSubject;
            registry.ActiveShips.OnRemove += RemoveCameraSubject;

            rig = Instantiate(playerRigPrefab, transform);
            yield return rig.Build(session.Units, session.Objectives, true, observer, ui, session.Frame, null);
            rig.ApplyLoadout();
            yield return session.LoadSector(0, rig.Player);

            yield return ComposeSubject();
            Transport.Restart();
        }

        private void BuildPlaytest()
        {
            var root = new GameObject("Preview Playtest");
            root.SetActive(false);
            playtest = root.AddComponent<GameHost>();
            playtest.sessionProfile = playtestSetup == PreviewPlaytestSetup.CrowdedJumpGate
                ? jumpPlaytestProfile : sessionProfile;
            playtest.playerRig = playerRigPrefab;
            playtest.observerCamPrefab = observerCamPrefab;
            playtest.recapFeel = recapFeel;
            if (playtestSetup == PreviewPlaytestSetup.CrowdedJumpGate)
            {
                playtest.jumpFeel = jumpFeel;
                playtest.jumpShader = jumpShader;
            }
            playtest.runRecordPath = Path.Combine(ResultsRoot.Folder("preview"),
                Guid.NewGuid().ToString("N"), RunRecordStore.FileName);
            root.SetActive(true);
        }

        public void SelectMode(PreviewMode selected)
        {
            RequireMode(selected);
            RequireAvailable();
            if (selected == mode) throw new InvalidOperationException("That preview mode is already selected.");
            mode = selected;
            BeginReplacement();
        }

        public void RestartPlaytest() => RestartPlaytest(recapFeel);

        public void SelectPlaytestSetup(PreviewPlaytestSetup selected)
        {
            RequireAvailable();
            if (mode != PreviewMode.Playtest)
                throw new InvalidOperationException("Setup selection requires Playtest mode.");
            RequirePlaytestSetup(selected);
            if (selected == playtestSetup)
                throw new InvalidOperationException("That Playtest setup is already selected.");
            playtestSetup = selected;
            BeginReplacement();
        }

        public void RestartPlaytest(RecapFeelProfile feel)
        {
            RequireAvailable();
            if (mode != PreviewMode.Playtest)
                throw new InvalidOperationException("Restart Playtest requires Playtest mode.");
            if (!feel) throw new ArgumentNullException(nameof(feel));
            recapFeel = feel;
            BeginReplacement();
        }

        public void RestartPlaytest(SectorJumpProfile feel)
        {
            RequireAvailable();
            if (mode != PreviewMode.Playtest || playtestSetup != PreviewPlaytestSetup.CrowdedJumpGate)
                throw new InvalidOperationException("Jump profile selection requires the crowded jump Playtest setup.");
            if (!feel) throw new ArgumentNullException(nameof(feel));
            jumpFeel = feel;
            BeginReplacement();
        }

        private void BeginReplacement()
        {
            replacing = true;
            Transport.Playing = false;
            StartCoroutine(CatchingRoutine.Run(ReplaceMode(), Faulted));
        }

        private IEnumerator ReplaceMode()
        {
            while (composing && fault == null) yield return null;
            if (fault != null) yield break;
            yield return TeardownMode();
            composing = true;
            yield return ComposeMode(mode);
            replacing = false;
        }

        public void SelectSubject(PreviewSubjectKind kind)
        {
            RequireAvailable();
            if (mode != PreviewMode.Inspection)
                throw new InvalidOperationException("Subject selection requires Inspection mode.");
            if (!Enum.IsDefined(typeof(PreviewSubjectKind), kind))
                throw new ArgumentOutOfRangeException(nameof(kind));
            if (composing) throw new InvalidOperationException("The preview is still composing.");
            DisposeSubject();
            subjectKind = kind;
            Transport.Playing = false;
            Transport.Restart();
            composing = true;
            StartCoroutine(CatchingRoutine.Run(ReplaceSubject(), Faulted));
        }

        private IEnumerator ReplaceSubject()
        {
            yield return ComposeSubject();
            composing = false;
        }

        private IEnumerator ComposeSubject()
        {
            var subject = NewSubject();
            yield return subject.Compose(new PreviewWorld(session, rig, observer, ui, transform, SetSimulationFrozen));
            Subject = subject;
        }

        private IPreviewSubject NewSubject() => subjectKind switch
        {
            PreviewSubjectKind.RunRecap => new RecapPreviewSubject(recapFeel, feel => recapFeel = feel),
            PreviewSubjectKind.SectorJump => new SectorJumpPreviewSubject(jumpFeel, jumpShader, feel => jumpFeel = feel),
            _ => throw new ArgumentOutOfRangeException(nameof(subjectKind), subjectKind, "Unknown preview subject.")
        };

        private void LateUpdate()
        {
            if (mode != PreviewMode.Inspection || !IsReady) return;
            Transport.Tick(Time.unscaledDeltaTime, Subject);
        }

        public IEnumerator Shutdown()
        {
            if (closing)
            {
                while (!closed)
                {
                    fault?.Throw();
                    yield return null;
                }
                yield break;
            }
            closing = true;
            Transport.Playing = false;
            while (composing || replacing)
            {
                fault?.Throw();
                yield return null;
            }
            yield return CatchingRoutine.Run(TeardownMode(), Faulted);
            fault?.Throw();
            closed = true;
        }

        private IEnumerator TeardownMode()
        {
            if (playtest)
            {
                yield return playtest.Shutdown();
                Destroy(playtest.gameObject);
                playtest = null;
            }
            if (session != null)
            {
                DisposeSubject();
                SetSimulationFrozen(false);
                rig.Teardown();
                yield return session.Teardown();
                session = null;
                unitService.ActiveRegistry.ActiveShips.OnAdd -= AddCameraSubject;
                unitService.ActiveRegistry.ActiveShips.OnRemove -= RemoveCameraSubject;
                Destroy(rig.gameObject);
                Destroy(viewport.gameObject);
                Destroy(ui.gameObject);
                Destroy(arena.gameObject);
                rig = null;
                observer = null;
                viewport = ui = arena = null;
            }
            yield return null;
        }

        private void AddCameraSubject(Ship ship) => observer.AddSecondarySubject(ship.transform);
        private void RemoveCameraSubject(Ship ship) => observer.RemoveSecondarySubject(ship.transform);

        private void DisposeSubject()
        {
            Subject.Dispose();
            Subject = null;
        }

        private void Faulted(Exception exception)
        {
            fault ??= ExceptionDispatchInfo.Capture(exception);
            Debug.LogException(exception, this);
        }

        private static void RequireMode(PreviewMode selected)
        {
            if (!Enum.IsDefined(typeof(PreviewMode), selected))
                throw new ArgumentOutOfRangeException(nameof(selected));
        }

        private void RequirePlaytestSetup(PreviewPlaytestSetup selected)
        {
            if (!Enum.IsDefined(typeof(PreviewPlaytestSetup), selected))
                throw new ArgumentOutOfRangeException(nameof(selected));
            if (selected == PreviewPlaytestSetup.CrowdedJumpGate &&
                (jumpPlaytestProfile == null || !jumpPlaytestProfile.presentation || !jumpFeel || !jumpShader))
                throw new InvalidOperationException("The crowded jump setup requires its visible session and jump assets.");
        }

        private void RequireAvailable()
        {
            if (fault != null)
                throw new InvalidOperationException("The preview host faulted; exit play mode to recover.",
                    fault.SourceException);
            if (IsReplacing) throw new InvalidOperationException("The preview is already replacing or shutting down.");
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
            if (playtest) Destroy(playtest.gameObject);
        }
    }
}
