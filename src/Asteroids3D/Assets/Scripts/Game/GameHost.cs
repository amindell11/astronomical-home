using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using Balance;
using Cameras;
using Combat.Weapons;
using Damage;
using Game.Audio;
using Game.Companion;
using Game.Display;
using Game.Moments;
using Game.Player;
using Game.Runs;
using Game.Travel;
using Loot;
using Substrate;
using Substrate.Presentation;
using Substrate.Sectors;
using Substrate.Sectors.Elements.Phases;
using Substrate.Sectors.Elements.Stations;
using Substrate.Sessions;
using Ships.Loadout;
using UI;
using UI.Hangar.Hardware;
using UI.Recap;
using UI.Screens;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using Ships.Registry;
using Substrate.Services.Units;
using Substrate.Services.Objectives;
using Substrate.Services.Locales;
using Utils.Coroutines;

namespace Game
{
    /// <summary>
    /// Runs the interactive game over one session: an optional title, then hangar, sectors (pausable in flight), extraction recaps, then final results.
    /// Owns the player rig, viewport, UI, music, companion, run records, each run's moment stream and clock;
    /// the session composes, loads and unloads.
    /// Hull carries between sectors. Death frames the explosion while surviving ships keep moving.
    /// Presentation is read once alongside the session's snapshot.
    /// Shutdown finishes the current setup operation, releases input waits, then tears down the owned world.
    /// A fault in the run's coroutine stack is logged, and Shutdown rethrows it instead of tearing down.
    /// An optional jump profile enables protected travel with the extraction recap held in hyperspace.
    /// The hangar, recap and restart stand in for Home Base and player progress.
    /// https://github.com/amindell11/astronomical-home/issues/295#issuecomment-5787867584
    /// </summary>
    [RequireComponent(typeof(UnitService))]
    [RequireComponent(typeof(ObjectiveService))]
    public class GameHost : MonoBehaviour
    {
        // Values are pinned: scenes serialize the numbers, so renumbering rewrites authored data.
        /// <summary>Session policy for what happens when the persistent player ship dies.</summary>
        public enum PlayerDeathBehavior { None = 0, RestartSector = 2 }

        [Header("Session")]
        [SerializeField] internal SessionProfile sessionProfile = new SessionProfile();

        [Tooltip("Prefab of the player and its HUD. The host builds its own copy once after session " +
                 "compose; it persists across sector restarts. Null → no player (spectator).")]
        [SerializeField] internal PlayerRig playerRig;

        [Header("View")]
        [Tooltip("Observer camera spawned once at session start and framed on the fleet; the player " +
                 "rig and the sector's modules are handed this instance.")]
        [SerializeField] internal ObserverCam observerCamPrefab;

        [Header("Splash")]
        [Tooltip("Full-screen splash shown over the non-interactive steps (boot, session compose, " +
                 "sector load/unload). Optional; skipped when presentation is off (headless/RL).")]
        [SerializeField] private LoadingSplash splashPrefab;

        [Header("Title")]
        [Tooltip("Opens the session on the title screen, over the hangar sky, before the first hangar. On in the " +
                 "shipped prefab; the preview host turns it off, and hosts built in code leave it off. Skipped when " +
                 "presentation is off.")]
        [SerializeField] internal bool openOnTitle;

        [Tooltip("The title screen's name face.")]
        [SerializeField] internal Font titleFont;

        [Tooltip("The face of the title, pause and settings screens' buttons and labels.")]
        [SerializeField] internal Font titleLabelFont;

        [Header("Music")]
        [Tooltip("The run's music, built once for the session and told each run step's state. Optional; " +
                 "skipped when presentation is off (headless/RL).")]
        [SerializeField] internal GameMusic musicPrefab;

        [Header("Hangar")]
        [Tooltip("Between-run hangar: the console, its displays and the ship hologram. " +
                 "Null → no interactive hangar (the standing loadout is applied silently).")]
        [SerializeField] internal HangarConsole hangarConsolePrefab;

        [Tooltip("Camera that shows the sky behind the hangar in place of the observer camera, drifting " +
                 "so its layers parallax. Null → the hangar shows the observer camera's view. Skipped " +
                 "when presentation is off.")]
        [SerializeField] internal DriftCam hangarCameraPrefab;

        [Tooltip("Modules the hangar offers per slot; its weapons are also the armory's weapon pick pool. " +
                 "Null → no hangar choices (the player flies its prefab-authored modules) and no weapon pick.")]
        [SerializeField] internal ItemSubset hangarOffer;

        [Header("Recap")]
        [Tooltip("Timings of the recap sequence: HUD fade, banner, gap, breakdown. The preview host tunes " +
                 "this same asset. Required whenever presentation is on.")]
        [SerializeField] internal RecapFeelProfile recapFeel;

        [Header("Sector travel")]
        [SerializeField] internal SectorJumpProfile jumpFeel;
        [SerializeField] internal SectorJumpFeature jumpFeature;

        [Header("Death Policy")]
        [Tooltip("What happens when the player ship dies. RestartSector ends the run (run record, death " +
                 "recap) and starts a new one from the hangar; None does nothing.")]
        [SerializeField] private PlayerDeathBehavior deathBehavior = PlayerDeathBehavior.RestartSector;

        [Header("Kill Refill")]
        [Tooltip("Fraction of max hull restored to the player on each kill the run tally counts.")]
        [SerializeField, Range(0f, 1f)] private float killHullRestore = 0.25f;

        [Header("Lull pick")]
        [Tooltip("Lull mods offered when the player claims a mod station, three at a time with time stopped. " +
                 "Null → no lull picks.")]
        [SerializeField] internal LullModPool lullMods;

        [Header("Companion")]
        [Tooltip("The companion's lines, one line slot per moment. Null → the companion stays silent. The " +
                 "preview host tunes this same asset.")]
        [SerializeField] internal CompanionLineSheet companionLines;

        [Tooltip("The nag budget and line timings. Required when lines are assigned and presentation is on.")]
        [SerializeField] internal CompanionFeelProfile companionFeel;

        internal string runRecordPath;
        internal BuildIdentitySource buildIdentity;
        internal string runExportFolder;
        internal Action<string> revealExportedRun;
        internal ShipUnlocks unlocks;

        private RunRecordStore runRecords;
        private DamageInfo lastKillingBlow;
        private Vector2 deathPosition;
        private bool playerDied;
        private bool sectorEnded;
        private float sectorEndTime;
        private readonly ClockStops clock = new();
        private IDisposable recapStop;
        private InputAction pauseKey;
        private bool pauseRequested;
        private bool quitRun;
        private bool shutdownRequested;
        private bool shutdownComplete;
        private int activeSectorIndex;
        private SectorJumpFlight jump;
        private ExceptionDispatchInfo fault;

        private UnitService unitService;
        private ObjectiveService objectiveService;

        private Session session;
        private ObserverCam observer;
        private DriftCam hangarCamera;
        private PlayerRig rigInstance;
        private LoadingSplash splash;
        private GameMusic music;
        private CompanionVoice voice;
        private RunMomentStream moments;

        private Transform viewport;
        private Transform ui;

        public Sector ActiveSector => session?.ActiveSector;
        public SectorJumpFlight Jump => jump;
        internal IUnitService Units => unitService;

        private void Awake()
        {
            unitService = GetComponent<UnitService>();
            objectiveService = GetComponent<ObjectiveService>();

            viewport = NewRoot("Viewport");
            ui = NewRoot("UI");

            buildIdentity ??= BuildIdentity.Source();
            runRecords = new RunRecordStore(runRecordPath);
            unlocks = new ShipUnlocks(runRecords.Path);

            DontDestroyOnLoad(gameObject);
            StartCoroutine(CatchingRoutine.Run(Run(), exception =>
            {
                fault = ExceptionDispatchInfo.Capture(exception);
                Debug.LogException(exception, this);
            }));
        }

        private IEnumerator Run()
        {
            // No yield separates this read from the session's own snapshot, so the two cannot disagree.
            var presentation = sessionProfile.presentation;
            if (presentation && !recapFeel)
                throw new InvalidOperationException("GameHost presents but has no recap feel profile assigned.");
            if (presentation && jumpFeel && !jumpFeature)
                throw new InvalidOperationException("GameHost enables jump travel but has no jump feature assigned.");
            if (presentation && companionLines && !companionFeel)
                throw new InvalidOperationException("GameHost has companion lines but no companion feel profile assigned.");
            session = new Session(sessionProfile, gameObject, unitService, objectiveService);
            if (presentation)
                BuildEventSystem(ui);
            if (splashPrefab && presentation)
                splash = Instantiate(splashPrefab, ui);
            if (musicPrefab && presentation)
            {
                music = Instantiate(musicPrefab, transform);
                music.Initialize(objectiveService);
            }
            if (presentation)
            {
                VolumeSettings.Apply();
                DisplaySettings.Apply();
            }

            SetSplashVisible(true);
            yield return null;

            yield return session.Compose();
            observer = BuildObserver(session.Units, presentation, viewport);
            if (hangarCameraPrefab && presentation)
            {
                hangarCamera = Instantiate(hangarCameraPrefab, viewport);
                SetHangarViewVisible(false);
            }
            if (playerRig)
            {
                // An asset gets no lifecycle, and state parked on it would outlive the session.
                rigInstance = Instantiate(playerRig, transform);
                yield return rigInstance.Build(session.Units, session.Objectives, presentation,
                    observer, ui, session.Frame, BuildDeathCallback());
                rigInstance.Tally.Killed += RefillPlayerHull;
                rigInstance.Spawns.EliteKilled += unlocks.Claim;
            }
            if (presentation && rigInstance)
            {
                pauseKey = new InputAction("Pause", InputActionType.Button, "<Keyboard>/escape");
                pauseKey.AddBinding("<Gamepad>/start");
                pauseKey.performed += _ => pauseRequested = true;
                pauseKey.Enable();
            }
            if (presentation && companionLines && rigInstance && rigInstance.Overlay)
                voice = CompanionVoice.Create(transform, companionLines, companionFeel, rigInstance.Overlay.CommLine,
                    () => rigInstance.Overlay.IsVisible, Debug.isDebugBuild);

            if (openOnTitle && presentation)
                yield return RunTitle();

            yield return PlayRuns(presentation);

            if (jump)
            {
                jump.Release(false);
                Destroy(jump.gameObject);
                jump = null;
                yield return null;
            }

            // The voice reads the rig's overlay each frame, so it goes first.
            if (voice) Destroy(voice.gameObject);
            if (rigInstance)
            {
                rigInstance.Tally.Killed -= RefillPlayerHull;
                rigInstance.Teardown();
            }
            clock.ReleaseAll();
            yield return session.Teardown();
            if (rigInstance) Destroy(rigInstance.gameObject);
            if (music) Destroy(music.gameObject);
            Destroy(ui.gameObject);
            Destroy(viewport.gameObject);
            yield return null;
            shutdownComplete = true;
        }

        public IEnumerator Shutdown()
        {
            shutdownRequested = true;
            while (!shutdownComplete)
            {
                fault?.Throw();
                yield return null;
            }
        }

        private IEnumerator PlayRuns(bool presentation)
        {
            while (!shutdownRequested)
            {
                SetSplashVisible(false);
                if (rigInstance)
                {
                    SetMusic(GameMusic.State.Hangar);
                    SetHangarViewVisible(true);
                    yield return RunHangar(rigInstance, presentation, ui);
                    SetHangarViewVisible(false);
                }
                if (shutdownRequested) yield break;

                playerDied = false;
                moments = new RunMomentStream();
                if (voice) voice.Initialize(moments);
                string playerLoadoutStatHash = null;
                var hull = 1f;
                var index = 0;
                var transferred = false;
                var locales = presentation ? sessionProfile.run.DrawLocales(new System.Random()) : null;
                while (true)
                {
                    if (!transferred)
                    {
                        SetSplashVisible(true);
                        sectorEnded = false;
                        activeSectorIndex = index;
                        if (rigInstance) rigInstance.Spawns.BeginSector();
                        yield return session.LoadSector(index, rigInstance ? rigInstance.Player : null, OnSectorComplete,
                            locales?[index]);
                    }
                    transferred = false;
                    if (shutdownRequested) yield break;
                    if (index == 0)
                        playerLoadoutStatHash = rigInstance ? BeginRun() : null;
                    else if (rigInstance)
                        rigInstance.Player.Damage.Health.SetFraction(hull);
                    moments.Raise(new SectorEntered(index + 1));
                    SetSplashVisible(false);
                    if (music) music.EnterSector(session.ActiveSector, session.ActiveLocale);
                    if (rigInstance) rigInstance.EnterSector(session.ActiveSector.Module<PhaseSpineModule>());
                    var sectorStartTime = Time.time;
                    var modStations = presentation && lullMods && rigInstance
                        ? session.ActiveSector.Module<ModStation>() : null;
                    var picksOpened = 0;
                    var armory = presentation && hangarOffer && rigInstance
                        ? session.ActiveSector.Module<Armory>() : null;
                    var armoryPicks = 0;
                    var caches = presentation && rigInstance ? session.ActiveSector.Module<BeaconCache>() : null;
                    var cachesClaimed = 0;
                    var closeCalls = rigInstance
                        ? new CloseCallWatch(rigInstance.Player.Damage.Health, moments, index + 1) : null;
                    Action<StationKind> onStationArrived =
                        kind => moments.Raise(new StationArrived(kind, index + 1));
                    var findStations = new FindStation[]
                    {
                        session.ActiveSector.Module<RepairStation>(),
                        session.ActiveSector.Module<ModStation>(),
                        session.ActiveSector.Module<Armory>(),
                    };
                    foreach (var station in findStations)
                        if (station) station.Arrived += onStationArrived;

                    pauseRequested = false;
                    while (!playerDied && !sectorEnded && !shutdownRequested && !quitRun)
                    {
                        if (pauseRequested) yield return RunPause();
                        else
                        {
                            if (modStations && modStations.Claims > picksOpened)
                            {
                                picksOpened++;
                                yield return RunLullPick();
                            }
                            if (armory && armory.Claims > armoryPicks)
                            {
                                armoryPicks++;
                                yield return RunWeaponPick(armory, index);
                            }
                            if (caches && caches.Claims > cachesClaimed)
                            {
                                cachesClaimed++;
                                moments.Raise(new CacheClaimed(index + 1, caches.LastBonus));
                            }
                        }

                        // Drops presses that arrived while a pick, pause or other screen blocked.
                        pauseRequested = false;
                        yield return null;
                    }
                    foreach (var station in findStations)
                        if (station) station.Arrived -= onStationArrived;
                    closeCalls?.Dispose();
                    if (shutdownRequested) yield break;

                    if (playerDied || quitRun || index == sessionProfile.run.Sectors.Count - 1) break;

                    // The unload despawns this sector's ships, and the next load resets the player's hull.
                    if (rigInstance)
                    {
                        hull = rigInstance.Player.Damage.Health.Pct;
                        rigInstance.Spawns.EndSector(sectorEndTime);
                        SetMusic(GameMusic.State.Recap);
                    }
                    if (jump)
                    {
                        yield return RunJump(ExtractionContext(index, sectorStartTime), index + 1, locales[index + 1],
                            hull);
                        transferred = true;
                    }
                    else
                    {
                        if (rigInstance && presentation)
                            yield return RunRecap(ExtractionContext(index, sectorStartTime));
                        if (shutdownRequested) yield break;
                        SetSplashVisible(true);
                        if (rigInstance) rigInstance.EnterSector(null);
                        yield return session.UnloadSector();
                    }
                    if (shutdownRequested) yield break;
                    index++;
                }

                if (rigInstance)
                {
                    if (!playerDied) EndRun();
                    if (!quitRun)
                    {
                        var board = AppendRunRecord(playerLoadoutStatHash, index, playerDied ? lastKillingBlow : null,
                            locales);
                        SetMusic(playerDied ? GameMusic.State.Defeat : GameMusic.State.Victory);
                        if (presentation)
                            yield return RunRecap(OutcomeContext(board, index));
                    }
                }
                if (shutdownRequested) yield break;

                SetSplashVisible(true);
                if (rigInstance) rigInstance.EnterSector(null);
                yield return session.UnloadSector();
                if (rigInstance) rigInstance.Park();
                if (quitRun)
                {
                    quitRun = false;
                    if (openOnTitle && presentation) yield return RunTitle();
                }
            }
        }

        private void OnSectorComplete(SectorResult result)
        {
            sectorEndTime = Time.time;
            sectorEnded = true;
            if (!result.Success || !jumpFeel || !sessionProfile.presentation || !rigInstance ||
                activeSectorIndex == sessionProfile.run.Sectors.Count - 1) return;
            jump = SectorJumpFlight.Begin(transform, rigInstance, session.Units, observer, jumpFeel,
                jumpFeature, result.DepartureHeading.Value);
        }

        private IEnumerator RunJump(RecapOutcomeContext context, int destination, LocaleSky destinationLocale, float hull)
        {
            LocaleBackdropPair backdrop = null;
            var arrived = false;
            var exited = false;
            IDisposable stop = null;
            try
            {
                backdrop = session.PrepareTravelBackdrop(destinationLocale, transform);
                if (!shutdownRequested)
                {
                    jump.BindBackdrop(ui, backdrop);
                    while ((!jump.Holding || jump.State.LocaleBlend < .9f) && !shutdownRequested) yield return null;
                }
                Coroutine recap = null;
                var destinationReady = false;
                var continued = false;
                if (!shutdownRequested)
                {
                    stop = clock.Hold();
                    recap = StartCoroutine(RecapPlayback.RunScreen(new RecapOutcomeSequence(recapFeel, context), ui,
                        cancelled: () => shutdownRequested || exited,
                        ready: () => destinationReady && jump.State.LocaleBlend >= 1f,
                        onContinue: () => continued = true));
                }
                if (!shutdownRequested)
                {
                    sectorEnded = false;
                    activeSectorIndex = destination;
                    rigInstance.Spawns.BeginSector();
                    rigInstance.EnterSector(null);
                    yield return session.TransferSector(destination, rigInstance.Player, OnSectorComplete,
                        destinationLocale);
                    rigInstance.EnterSector(session.ActiveSector.Module<PhaseSpineModule>());
                    rigInstance.Player.Damage.Health.SetFraction(hull);
                    destinationReady = true;
                }
                if (recap != null) yield return recap;
                if (continued && !shutdownRequested)
                {
                    jump.ResumeArrival();
                    stop?.Dispose();
                }
                if (!shutdownRequested)
                {
                    while (!jump.Complete && !shutdownRequested) yield return null;
                }
                arrived = !shutdownRequested;
            }
            finally
            {
                exited = true;
                jump.Release(arrived);
                Destroy(jump.gameObject);
                jump = null;
                if (backdrop) Destroy(backdrop.gameObject);
                stop?.Dispose();
            }
            yield return null;
        }

        private IEnumerator RunTitle()
        {
            SetSplashVisible(false);
            SetMusic(GameMusic.State.Hangar);
            SetHangarViewVisible(true);
            string exportResult = null;
            while (true)
            {
                var screen = TitleScreen.Create(ui, titleFont, titleLabelFont, exportResult);
                var exporting = false;
                try
                {
                    while (!screen.Choice.HasValue && !shutdownRequested) yield return null;
                    if (screen.Choice == TitleScreen.Option.Quit)
                    {
                        QuitGame();
                        // Quitting lands after this frame; the hangar must not open meanwhile.
                        while (!shutdownRequested) yield return null;
                    }

                    exporting = screen.Choice == TitleScreen.Option.Export;
                    if (exporting)
                        exportResult = ExportRuns();
                    else if (screen.Choice != TitleScreen.Option.Settings)
                        yield break;
                }
                finally
                {
                    Destroy(screen.gameObject);
                }

                if (exporting)
                    continue;

                exportResult = null;
                yield return RunSettings();
            }
        }

        private string ExportRuns()
        {
            if (!buildIdentity(out var identity, out var failure))
            {
                Debug.LogError($"Runs not exported, build identity unreadable: {failure}", this);
                return "Could not save the runs file";
            }

            var folder = string.IsNullOrEmpty(runExportFolder)
                ? Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)
                : runExportFolder;
            var export = runRecords.Export(folder, identity);
            if (export.Outcome == RunRecordExport.Kind.Saved)
            {
                (revealExportedRun ?? RevealExportedFile)(export.WrittenPath);
                return "Saved to Desktop: " + System.IO.Path.GetFileName(export.WrittenPath);
            }

            if (export.Outcome == RunRecordExport.Kind.NothingYet)
                return "No runs recorded yet";
            return "Could not save the runs file";
        }

        private static void RevealExportedFile(string path)
        {
            if (Application.isEditor || Application.platform != RuntimePlatform.WindowsPlayer)
                return;
            var start = new System.Diagnostics.ProcessStartInfo("explorer.exe", "/select,\"" + path + "\"")
            {
                UseShellExecute = true,
            };
            using var explorer = System.Diagnostics.Process.Start(start);
        }

        private IEnumerator RunPause()
        {
            var stop = clock.Hold();
            MixBuses.SetWorldPaused(true);
            rigInstance.SetInputEnabled(false);
            try
            {
                // The screen under settings goes, so its Escape can't fire too; it is rebuilt after.
                while (true)
                {
                    var screen = PauseScreen.Create(ui, titleLabelFont);
                    try
                    {
                        while (!screen.Choice.HasValue && !shutdownRequested) yield return null;
                        quitRun = screen.Choice == PauseScreen.Option.QuitRun;
                        if (screen.Choice != PauseScreen.Option.Settings) yield break;
                    }
                    finally { Destroy(screen.gameObject); }
                    yield return RunSettings();
                }
            }
            finally
            {
                rigInstance.SetInputEnabled(true);
                MixBuses.SetWorldPaused(false);
                stop.Dispose();
            }
        }

        private IEnumerator RunSettings()
        {
            var resolutions = DisplaySettings.Resolutions();
            var rows = new List<SettingsRow>();
            foreach (var channel in VolumeSettings.Channels)
                rows.Add(SettingsRow.Slider(VolumeSettings.Label(channel), VolumeSettings.Get(channel),
                    v => VolumeSettings.Set(channel, v)));
            rows.Add(SettingsRow.Toggle("FLIGHT ASSIST", AssistSetting.On, on =>
            {
                AssistSetting.Set(on);
                if (rigInstance) rigInstance.SetAssist(on);
            }));
            rows.Add(SettingsRow.Toggle("FULLSCREEN", DisplaySettings.Fullscreen,
                on => DisplaySettings.Set(DisplaySettings.Resolution, on)));
            rows.Add(SettingsRow.Choice("RESOLUTION", resolutions.Select(DisplaySettings.Label).ToList(),
                DisplaySettings.IndexOf(resolutions, DisplaySettings.Resolution),
                i => DisplaySettings.Set(resolutions[i], DisplaySettings.Fullscreen)));

            var screen = SettingsScreen.Create(ui, titleLabelFont, rows);
            try
            {
                while (!screen.Closed && !shutdownRequested) yield return null;
            }
            finally
            {
                Destroy(screen.gameObject);
                PlayerPrefs.Save();
            }
        }

        private static void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // Like the lull pick, the clock stops for it; only a taken weapon spends the armory.
        private IEnumerator RunWeaponPick(Armory armory, int index)
        {
            var weapons = rigInstance.Player.Weapons;
            if (!weapons) yield break;
            var primary = weapons.PrimaryMountPrefab;
            var secondary = weapons.SecondaryMountPrefab;
            var offers = LootOffers.Draw(hangarOffer.weapons, weapon => weapon != primary && weapon != secondary,
                WeaponPickScreen.MaxOffers, n => UnityEngine.Random.Range(0, n));
            if (offers.Count == 0) yield break;

            var stop = clock.Hold();
            rigInstance.SetInputEnabled(false);
            var spine = session.ActiveSector.Module<PhaseSpineModule>();
            var screen = WeaponPickScreen.Create(ui, index + 1,
                spine ? spine.Threat(sessionProfile.run.Sectors[index]) : null, offers,
                primary, secondary, rigInstance.Mods);
            try
            {
                while (!screen.Choice.HasValue && !shutdownRequested) yield return null;
                if (!shutdownRequested && screen.Choice is int pick && pick != WeaponPickScreen.Skip)
                {
                    rigInstance.SwapWeapon(offers[pick]);
                    armory.Take();
                    moments.Raise(new ArmoryPicked(index + 1, offers[pick]));
                }
            }
            finally
            {
                Destroy(screen.gameObject);
                rigInstance.SetInputEnabled(true);
                stop.Dispose();
            }
        }

        // The clock stops for the pick and runs on after it; a claim can land mid-phase.
        private IEnumerator RunLullPick()
        {
            var player = rigInstance.Player;
            var offers = LootOffers.Draw(lullMods.mods, mod => mod.Fits(player), LullPickScreen.MaxOffers,
                n => UnityEngine.Random.Range(0, n));
            if (offers.Count == 0) yield break;

            var stop = clock.Hold();
            rigInstance.SetInputEnabled(false);
            var screen = LullPickScreen.Create(ui, offers, mod => mod.Landing(player));
            try
            {
                while (!screen.Choice.HasValue && !shutdownRequested) yield return null;
                if (!shutdownRequested && screen.Choice is int pick && pick != LullPickScreen.Skip)
                {
                    rigInstance.PickMod(offers[pick]);
                    moments.Raise(new ModPicked(offers[pick]));
                }
            }
            finally
            {
                Destroy(screen.gameObject);
                rigInstance.SetInputEnabled(true);
                stop.Dispose();
            }
        }

        private Transform NewRoot(string name)
        {
            var root = new GameObject(name).transform;
            root.SetParent(transform, false);
            return root;
        }

        // The hangar and recap screens click through uGUI, which needs exactly one EventSystem.
        private static void BuildEventSystem(Transform parent)
        {
            var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            eventSystem.transform.SetParent(parent, false);
        }

        private void SetSplashVisible(bool visible)
        {
            if (splash) splash.SetVisible(visible);
        }

        private void SetMusic(GameMusic.State state)
        {
            if (music) music.Set(state);
        }

        private void SetHangarViewVisible(bool visible)
        {
            if (!hangarCamera) return;
            hangarCamera.gameObject.SetActive(visible);
            observer.SetVisible(!visible);
        }

        /// <summary>Stays callable without a session so the presentation gate can be driven directly.</summary>
        internal ObserverCam BuildObserver(IUnitService units, bool presentationEnabled, Transform parent)
        {
            var built = Instantiate(observerCamPrefab, parent);

            // The authored prefab sees the locale's Sky layer; presentation-off does not draw it.
            if (!presentationEnabled)
                built.Cam.cullingMask &= ~(1 << LayerIds.Sky);

            // The camera carries authored presentation of its own (the reverb zone).
            PresentationApplier.Apply(built.gameObject, presentationEnabled);

            var registry = units.ActiveRegistry;
            if (registry == null)
                return built;

            registry.ActiveShips.OnAdd += s => built.AddSecondarySubject(s.transform);
            registry.ActiveShips.OnRemove += s => built.RemoveSecondarySubject(s.transform);
            return built;
        }

        private Action<ShipId, DamageInfo> BuildDeathCallback()
        {
            switch (deathBehavior)
            {
                case PlayerDeathBehavior.RestartSector:
                    return (_, killingBlow) =>
                    {
                        EndRun();
                        lastKillingBlow = killingBlow;
                        deathPosition = GamePlane.WorldPointToPlane(rigInstance.Player.transform.position);
                        playerDied = true;
                        moments.Raise(new PlayerDied(activeSectorIndex + 1, killingBlow.Kind));
                    };
                case PlayerDeathBehavior.None:
                default:
                    return null;
            }
        }

        /// <summary>Starts the rig's run clocks and returns the loadout stat hash the player flies this run.</summary>
        private string BeginRun()
        {
            rigInstance.Tally.Begin(Time.time);
            rigInstance.Spawns.Begin(Time.time);
            rigInstance.SetAssist(AssistSetting.On);
            moments.Raise(new RunStarted(PastRuns.Read(runRecords.Path)));
            return StatHash.OfLoadout(rigInstance.Player);
        }

        private void EndRun()
        {
            rigInstance.Tally.End(Time.time);
            rigInstance.Spawns.End(Time.time);
        }

        // An unreadable build identity leaves the run unrecorded, like a failed write.
        private Scoreboard AppendRunRecord(string playerLoadoutStatHash, int endedIn, DamageInfo? killingBlow,
            LocaleSky[] locales)
        {
            if (!buildIdentity(out var identity, out var failure))
            {
                Debug.LogError($"Run record skipped, build identity unreadable: {failure}", this);
                return Scoreboard.Read(runRecords.Path, null);
            }

            var sectorsCleared = killingBlow.HasValue ? endedIn : endedIn + 1;
            var record = RunRecord.Compose(DateTime.UtcNow, sectorsCleared, endedIn, locales, identity,
                StatHash.OfSetting(hangarOffer, sessionProfile.run.Sectors, killHullRestore), rigInstance.Loadout,
                playerLoadoutStatHash, rigInstance.Tally, rigInstance.Ledger, rigInstance.Spawns, killingBlow);
            var board = Scoreboard.Read(runRecords.Path, record);
            return runRecords.Append(record) ? board : Scoreboard.Read(runRecords.Path, null);
        }

        private void RefillPlayerHull() => rigInstance.Player.Damage.Health.RestoreFraction(killHullRestore);

        /// <summary>Never blocks on a click when not presenting; callable without a session for tests.</summary>
        internal IEnumerator RunHangar(PlayerRig rig, bool presentationEnabled, Transform uiRoot)
        {
            unlocks.Read();
            if (!rig || !rig.Player || rig.Loadout == null || !hangarConsolePrefab || !presentationEnabled)
            {
                if (rig) rig.ApplyLoadout();
                yield break;
            }

            var console = Instantiate(hangarConsolePrefab, uiRoot);
            // Its own world, away from the sector and the preview stage.
            console.transform.position = new Vector3(0f, -2000f, 0f);
            var launched = false;
            console.Show(hangarOffer, rig.Loadout, unlocks.Earned, () => launched = true);

            yield return new WaitUntil(() => launched || shutdownRequested);

            if (!shutdownRequested) rig.ApplyLoadout();
            Destroy(console.gameObject);
        }

        private RecapOutcomeContext ExtractionContext(int index, float sectorStartTime) =>
            RecapOutcomeContext.Extraction(rigInstance.Spawns.KilledShips(currentSector: true), index + 1,
                Mathf.Max(0f, sectorEndTime - sectorStartTime));

        private RecapOutcomeContext OutcomeContext(Scoreboard board, int index)
        {
            var kills = rigInstance.Spawns.KilledShips();
            var count = sessionProfile.run.Sectors.Count;
            return playerDied
                ? RecapOutcomeContext.Defeat(kills, index, count, board, deathPosition)
                : RecapOutcomeContext.Victory(kills, index + 1, count, board);
        }

        private IEnumerator RunRecap(RecapOutcomeContext context)
        {
            var sequence = new RecapOutcomeSequence(recapFeel, context);
            var presenter = new RecapPresenter(context, rigInstance.Overlay, session.Units.ActiveRegistry.ActiveShips,
                observer);
            try
            {
                yield return RecapPlayback.RunScreen(sequence, ui, state =>
                    {
                        presenter.Apply(state);
                        SetRecapClock(state.SimulationFrozen);
                    }, () => shutdownRequested);
            }
            finally
            {
                recapStop?.Dispose();
                recapStop = null;
                presenter.Restore();
            }
        }

        private void SetRecapClock(bool frozen)
        {
            if (frozen) recapStop ??= clock.Hold();
            else
            {
                recapStop?.Dispose();
                recapStop = null;
            }
        }

        private void OnDestroy()
        {
            pauseKey?.Dispose();
            clock.ReleaseAll();
        }
    }
}
