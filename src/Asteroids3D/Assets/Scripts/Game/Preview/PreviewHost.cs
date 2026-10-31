using System;
using System.Collections;
using System.IO;
using System.Runtime.ExceptionServices;
using Cameras;
using Game.Runs;
using Ships;
using Substrate.Results;
using Substrate.Services.Locales;
using Substrate.Services.Objectives;
using Substrate.Services.Units;
using Substrate.Sessions;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using Utils.Coroutines;

namespace Game.Preview
{
    public enum PreviewEntry { RunRecap, SectorJump, Combat, CrowdedJumpGate }

    /// <summary>
    /// A Replay entry owns a session and a presentation subject driven by a transport; a Playtest entry
    /// owns an instance of the game's GameHost prefab, overriding only its run records and, for the
    /// crowded jump gate, its sectors. Replay takes its rig, observer, jump shader and feel profiles from
    /// the same prefab, so both kinds of entry run on the feel assets the game ships. Replacement awaits
    /// the previous entry's teardown before composing the next. Only Replay subjects may freeze this
    /// host's clock.
    /// A throw while composing, replacing or tearing down faults the host: every later request refuses,
    /// Shutdown rethrows instead of tearing down, and only destroying the host recovers.
    /// https://github.com/amindell11/astronomical-home/issues/1151
    /// </summary>
    [RequireComponent(typeof(UnitService))]
    [RequireComponent(typeof(ObjectiveService))]
    public sealed class PreviewHost : MonoBehaviour
    {
        [SerializeField] internal GameHost gameHostPrefab;
        [SerializeField] internal SessionProfile sessionProfile;
        [SerializeField] internal SectorEntry[] crowdedJumpSectors;
        [SerializeField] internal LocaleSky jumpDestination;
        [SerializeField] internal PreviewEntry entry;

        private UnitService unitService;
        private ObjectiveService objectiveService;
        private Session session;
        private ObserverCam observer;
        private PlayerRig rig;
        private Transform viewport;
        private Transform ui;
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
        public PreviewEntry Entry => entry;
        public GameHost Playtest => playtest;
        public bool IsReplacing => replacing || closing;
        public Exception Fault => fault?.SourceException;
        public bool IsReady => fault == null && !composing && !IsReplacing &&
            (IsPlaytest(entry) ? playtest != null : Subject != null);

        public static bool IsPlaytest(PreviewEntry entry) => entry switch
        {
            PreviewEntry.RunRecap or PreviewEntry.SectorJump => false,
            PreviewEntry.Combat or PreviewEntry.CrowdedJumpGate => true,
            _ => throw new ArgumentOutOfRangeException(nameof(entry), entry, "Unknown preview entry.")
        };

        private void Awake()
        {
            unitService = GetComponent<UnitService>();
            objectiveService = GetComponent<ObjectiveService>();
            RequireEntry(entry);
            if (!sessionProfile.presentation)
                throw new InvalidOperationException("The preview host requires presentation to be enabled.");
            DontDestroyOnLoad(gameObject);
            composing = true;
            StartCoroutine(CatchingRoutine.Run(ComposeEntry(entry), Faulted));
        }

        // Takes its entry: a Playtest selection may land while a Replay is still composing.
        private IEnumerator ComposeEntry(PreviewEntry composed)
        {
            if (IsPlaytest(composed))
                BuildPlaytest(composed);
            else
                yield return ComposeReplay(composed);
            composing = false;
        }

        private IEnumerator ComposeReplay(PreviewEntry composed)
        {
            viewport = NewRoot("Viewport");
            ui = NewRoot("UI");
            session = new Session(sessionProfile, gameObject, unitService, objectiveService);
            var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            eventSystem.transform.SetParent(ui, false);
            yield return session.Compose();

            observer = Instantiate(gameHostPrefab.observerCamPrefab, viewport);
            var registry = session.Units.ActiveRegistry;
            registry.ActiveShips.OnAdd += AddCameraSubject;
            registry.ActiveShips.OnRemove += RemoveCameraSubject;

            rig = Instantiate(gameHostPrefab.playerRig, transform);
            yield return rig.Build(session.Units, session.Objectives, true, observer, ui, session.Frame, null);
            rig.ApplyLoadout();
            yield return session.LoadSector(0, rig.Player);

            yield return ComposeSubject(composed);
            Transport.Restart();
        }

        // GameHost reads its configuration in Awake, so the overrides land while the copy is staged inactive.
        private void BuildPlaytest(PreviewEntry composed)
        {
            var staging = new GameObject("Preview Playtest staging");
            staging.SetActive(false);
            playtest = Instantiate(gameHostPrefab, staging.transform);
            if (composed == PreviewEntry.CrowdedJumpGate)
                playtest.sessionProfile.sectors = crowdedJumpSectors;
            playtest.runRecordPath = Path.Combine(ResultsRoot.Folder("preview"),
                Guid.NewGuid().ToString("N"), RunRecordStore.FileName);
            playtest.transform.SetParent(null, false);
            Destroy(staging);
        }

        public void Select(PreviewEntry selected)
        {
            RequireEntry(selected);
            RequireAvailable();
            if (selected == entry) throw new InvalidOperationException("That preview entry is already selected.");
            if (IsPlaytest(selected) || IsPlaytest(entry))
            {
                entry = selected;
                BeginReplacement();
                return;
            }
            if (composing) throw new InvalidOperationException("The preview is still composing.");
            DisposeSubject();
            entry = selected;
            Transport.Playing = false;
            Transport.Restart();
            composing = true;
            StartCoroutine(CatchingRoutine.Run(ReplaceSubject(), Faulted));
        }

        public void RestartPlaytest()
        {
            RequireAvailable();
            if (!IsPlaytest(entry))
                throw new InvalidOperationException("Restart Playtest requires a Playtest entry.");
            BeginReplacement();
        }

        private void BeginReplacement()
        {
            replacing = true;
            Transport.Playing = false;
            StartCoroutine(CatchingRoutine.Run(ReplaceEntry(), Faulted));
        }

        private IEnumerator ReplaceEntry()
        {
            while (composing && fault == null) yield return null;
            if (fault != null) yield break;
            yield return TeardownEntry();
            composing = true;
            yield return ComposeEntry(entry);
            replacing = false;
        }

        private IEnumerator ReplaceSubject()
        {
            yield return ComposeSubject(entry);
            composing = false;
        }

        private IEnumerator ComposeSubject(PreviewEntry composed)
        {
            var subject = NewSubject(composed);
            yield return subject.Compose(new PreviewWorld(session, rig, observer, ui, transform, SetSimulationFrozen));
            Subject = subject;
        }

        private IPreviewSubject NewSubject(PreviewEntry composed) => composed switch
        {
            PreviewEntry.RunRecap => new RecapPreviewSubject(gameHostPrefab.recapFeel),
            PreviewEntry.SectorJump => new SectorJumpPreviewSubject(gameHostPrefab.jumpFeel, gameHostPrefab.jumpFeature,
                jumpDestination),
            _ => throw new ArgumentOutOfRangeException(nameof(composed), composed, "Not a Replay entry.")
        };

        private void LateUpdate()
        {
            if (IsPlaytest(entry) || !IsReady) return;
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
            yield return CatchingRoutine.Run(TeardownEntry(), Faulted);
            fault?.Throw();
            closed = true;
        }

        private IEnumerator TeardownEntry()
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
                rig = null;
                observer = null;
                viewport = ui = null;
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

        private void RequireEntry(PreviewEntry selected)
        {
            if (!Enum.IsDefined(typeof(PreviewEntry), selected))
                throw new ArgumentOutOfRangeException(nameof(selected));
            if (selected == PreviewEntry.CrowdedJumpGate &&
                (crowdedJumpSectors is not { Length: > 0 } || !gameHostPrefab.jumpFeel || !gameHostPrefab.jumpFeature))
                throw new InvalidOperationException("The crowded jump setup requires its fixture sectors and jump assets.");
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
