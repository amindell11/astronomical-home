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
using Game.CloseUps;
using Game.Companion;
using Game.Display;
using Game.Moments;
using Game.Moments.Encounters;
using Game.Moments.Finds;
using Game.Player;
using Game.Player.Recorders;
using Game.Runs;
using Game.Travel;
using Game.Tutorial;
using Loot;
using Substrate;
using Substrate.Presentation;
using Substrate.Sectors;
using Substrate.Sectors.Elements.Phases;
using Substrate.Sectors.Elements.Stations;
using Substrate.Sectors.Factions;
using Substrate.Sessions;
using Ships;
using Ships.Command;
using Ships.Loadout;
using Ships.Weapons;
using UI;
using UI.Hangar.Hardware;
using UI.Prompts;
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
    /// Each sector's first wave slows time for a close-up on its rival.
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
        [Tooltip("Hull points restored to the player on each counted kill that is neither a rival nor the elite: minions, hunt patrols and elite escorts.")]
        [SerializeField, Min(0f)] private float minionKillHeal = 10f;
        [Tooltip("Hull points restored to the player on each counted kill of a wave's rival or the sector's elite.")]
        [SerializeField, Min(0f)] private float rivalKillHeal = 40f;

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

        [Header("Rival close-up")]
        [Tooltip("How each sector's first wave shows its rival: time slows while the camera pans and zooms onto " +
                 "it. Presentation only.")]
        [SerializeField] internal CloseUpFeel closeUpFeel = new CloseUpFeel();

        [Header("Guided first sector")]
        [Tooltip("Teach the controls and the hunt in sector 1, holding the hunt until the player has done every " +
                 "step, while the player's Tutorial prompts setting is on. Presentation only.")]
        [SerializeField] internal bool teachFirstSector;
        [SerializeField] internal TutorialFeel tutorialFeel = new TutorialFeel();

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
        private bool runOpen;
        private int clearedSectors;
        private RunCourse course;
        private string runLoadoutHash;
        private LocaleSky[] runLocales;
        private Faction[] runFactions;
        private bool shutdownComplete;
        private int activeSectorIndex;
        private string activeSectorFaction = string.Empty;
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
        private Ship closeUpRival;

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
            var unlockedByElite = new Dictionary<string, string>();
            foreach (var row in sessionProfile.run.Sectors)
            {
                var faction = row.faction;
                if (faction) unlockedByElite.TryAdd(faction.EliteShip.name, faction.Unlocks.name);
            }
            unlocks = new ShipUnlocks(runRecords.Path, unlockedByElite);

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
                unlocks.Unlocked += OnShipUnlocked;
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
            RecordAbandonedRun();

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
                unlocks.Unlocked -= OnShipUnlocked;
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
                moments.Raised += OnMomentRaised;
                var hull = 1f;
                var index = 0;
                var transferred = false;
                var draw = new System.Random();
                runLocales = presentation ? sessionProfile.run.DrawLocales(draw) : null;
                runFactions = presentation ? sessionProfile.run.DealFactions(draw) : null;
                if (voice) voice.Initialize(moments, runFactions);
                while (true)
                {
                    if (!transferred)
                    {
                        SetSplashVisible(true);
                        sectorEnded = false;
                        activeSectorIndex = index;
                        if (rigInstance) rigInstance.BeginSector(index + 1);
                        yield return session.LoadSector(index, rigInstance ? rigInstance.Player : null, OnSectorComplete,
                            runLocales?[index], runFactions?[index]);
                    }
                    transferred = false;
                    if (shutdownRequested) yield break;
                    if (index == 0)
                        runLoadoutHash = rigInstance ? BeginRun() : null;
                    else if (rigInstance)
                        rigInstance.Player.Damage.Health.SetFraction(hull);
                    var factionModule = session.ActiveSector.Module<SectorFaction>();
                    var faction = factionModule ? factionModule.Faction.id : string.Empty;
                    activeSectorFaction = faction;
                    moments.Raise(new SectorEntered(index + 1, faction));
                    SetSplashVisible(false);
                    if (music) music.EnterSector(session.ActiveSector, session.ActiveLocale);
                    var spine = session.ActiveSector.Module<PhaseSpineModule>();
                    if (rigInstance) rigInstance.EnterSector(spine);
                    var tutorial = Teaches(index, presentation, spine)
                        ? new FirstSectorTutorial(rigInstance.Overlay, spine.Hunt, tutorialFeel.wavesCardSeconds)
                        : null;
                    if (tutorial != null) spine.Hunt.Hold();
                    var sectorStartTime = Time.time;
                    OpenSector(index);
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
                    var sectorMoments = new SectorMoments(moments, faction, spine ? spine.Hunt : null,
                        spine ? spine.Elite : null, spine ? spine.Waves : null, Introduces);
                    closeUpRival = null;

                    pauseRequested = false;
                    while (!playerDied && !sectorEnded && !shutdownRequested && !quitRun)
                    {
                        if (pauseRequested) yield return RunPause();
                        else
                        {
                            if (modStations && modStations.Claims > picksOpened)
                            {
                                picksOpened++;
                                yield return RunLullPick(index + 1);
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
                            if (tutorial != null)
                            {
                                tutorial.Tick();
                                if (tutorial.IntroDue)
                                {
                                    yield return RunTutorialStations(findStations);
                                    if (!pauseRequested) yield return RunTutorialPatrol(tutorial.Patrol);
                                    tutorial.IntroDone();
                                    spine.Hunt.Resume();
                                    if (pauseRequested) continue;
                                }
                            }
                            if (closeUpRival)
                            {
                                var rival = closeUpRival;
                                closeUpRival = null;
                                yield return RunCloseUp(rival);
                                // A pause cuts the close-up short and must survive the drop below.
                                if (pauseRequested) continue;
                            }
                        }

                        // Drops presses that arrived while a pick, pause or other screen blocked.
                        pauseRequested = false;
                        yield return null;
                    }
                    foreach (var station in findStations)
                        if (station) station.Arrived -= onStationArrived;
                    sectorMoments.Dispose();
                    closeCalls?.Dispose();
                    tutorial?.End();
                    if (shutdownRequested) yield break;
                    CloseOpenSector();

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
                        yield return RunJump(ExtractionContext(index, sectorStartTime), index + 1, runLocales[index + 1],
                            runFactions[index + 1], hull);
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
                    runOpen = false;
                    var board = AppendRunRecord(runLoadoutHash, index,
                        quitRun || !playerDied ? (DamageInfo?)null : lastKillingBlow, runLocales, quitRun,
                        out var record);
                    if (!quitRun)
                    {
                        // Victory has no cue of its own in Studio, so the sector's cue plays on under it.
                        if (playerDied) SetMusic(GameMusic.State.Defeat);
                        if (presentation)
                        {
                            if (!playerDied) StandDown(true);
                            yield return RunRecap(OutcomeContext(board, index));
                            if (!playerDied) StandDown(false);
                            if (playerDied && record is { buildIdentity: { playtest: true } } run)
                                yield return AskFeedback(run);
                        }
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
            if (result.Success && activeSectorIndex == 0 && teachFirstSector && sessionProfile.presentation && rigInstance)
                TutorialSetting.NoteFirstSectorCleared();
            if (!result.Success || !jumpFeel || !sessionProfile.presentation || !rigInstance ||
                activeSectorIndex == sessionProfile.run.Sectors.Count - 1) return;
            jump = SectorJumpFlight.Begin(transform, rigInstance, session.Units, observer, jumpFeel,
                jumpFeature, result.DepartureHeading.Value);
        }

        private IEnumerator RunJump(RecapOutcomeContext context, int destination, LocaleSky destinationLocale,
            Faction destinationFaction, float hull)
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
                        rigInstance.Screens,
                        cancelled: () => shutdownRequested || exited,
                        ready: () => destinationReady && jump.State.LocaleBlend >= 1f,
                        onContinue: () => continued = true));
                }
                if (!shutdownRequested)
                {
                    sectorEnded = false;
                    activeSectorIndex = destination;
                    rigInstance.BeginSector(destination + 1);
                    rigInstance.EnterSector(null);
                    yield return session.TransferSector(destination, rigInstance.Player, OnSectorComplete,
                        destinationLocale, destinationFaction);
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
                try
                {
                    while (!screen.Choice.HasValue && !shutdownRequested) yield return null;
                    if (screen.Choice == TitleScreen.Option.Quit)
                    {
                        QuitGame();
                        // Quitting lands after this frame; the hangar must not open meanwhile.
                        while (!shutdownRequested) yield return null;
                    }

                    if (screen.Choice == TitleScreen.Option.Export)
                    {
                        exportResult = ExportRuns();
                        continue;
                    }

                    if (screen.Choice != TitleScreen.Option.Settings) yield break;
                }
                finally
                {
                    Destroy(screen.gameObject);
                }

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
            if (Application.platform != RuntimePlatform.WindowsPlayer)
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
            rows.Add(SettingsRow.Toggle("TUTORIAL PROMPTS", TutorialSetting.On, TutorialSetting.Set));
            rows.Add(SettingsRow.Slider("SCREEN SHAKE", CameraSettings.ShakeScale, v => CameraSettings.ShakeScale = v));
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
            var screen = WeaponPickScreen.Create(ui, offers, primary, secondary, rigInstance.Mods,
                rigInstance.Screens);
            try
            {
                while (!screen.Choice.HasValue && !shutdownRequested) yield return null;
                if (!screen.Choice.HasValue) yield break;
                var pick = screen.Choice.Value;
                var taken = string.Empty;
                var replaced = string.Empty;
                if (pick != WeaponPickScreen.Skip)
                {
                    var chosen = offers[pick];
                    var mount = WeaponsController.SlotFor(chosen) == WeaponSlot.Primary ? primary : secondary;
                    replaced = mount ? mount.name : string.Empty;
                    taken = chosen.name;
                    rigInstance.SwapWeapon(chosen);
                    armory.Take();
                    moments.Raise(new ArmoryPicked(index + 1, chosen));
                }
                course.NotePick(RunRecord.PickWeapon, index + 1, rigInstance.Tally.SecondsSurvived,
                    AssetNames(offers), taken, replaced);
            }
            finally
            {
                Destroy(screen.gameObject);
                rigInstance.SetInputEnabled(true);
                stop.Dispose();
            }
        }

        // The clock stops for the pick and runs on after it; a claim can land mid-phase.
        private IEnumerator RunLullPick(int sector)
        {
            var player = rigInstance.Player;
            var offers = LootOffers.Draw(lullMods.mods, mod => mod.Fits(player), LullPickScreen.MaxOffers,
                n => UnityEngine.Random.Range(0, n));
            if (offers.Count == 0) yield break;

            var stop = clock.Hold();
            rigInstance.SetInputEnabled(false);
            var screen = LullPickScreen.Create(ui, offers, mod => mod.Landing(player), rigInstance.Screens);
            try
            {
                while (!screen.Choice.HasValue && !shutdownRequested) yield return null;
                if (!screen.Choice.HasValue) yield break;
                var pick = screen.Choice.Value;
                var taken = string.Empty;
                if (pick != LullPickScreen.Skip)
                {
                    taken = offers[pick].name;
                    rigInstance.PickMod(offers[pick]);
                    moments.Raise(new ModPicked(offers[pick]));
                }
                course.NotePick(RunRecord.PickMod, sector, rigInstance.Tally.SecondsSurvived,
                    AssetNames(offers), taken, string.Empty);
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
                        var byEnemy = killingBlow.AttackerId != ShipId.Invalid && killingBlow.AttackerId != rigInstance.Player.Id;
                        var faction = byEnemy ? activeSectorFaction : string.Empty;
                        moments.Raise(new PlayerDied(activeSectorIndex + 1, killingBlow.Kind, faction));
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
            rigInstance.Dealt.Begin();
            course = new RunCourse();
            clearedSectors = 0;
            runOpen = true;
            rigInstance.SetAssist(AssistSetting.On);
            moments.Raise(new RunStarted(PastRuns.Read(runRecords.Path)));
            return StatHash.OfLoadout(rigInstance.Player);
        }

        private void EndRun()
        {
            rigInstance.Tally.End(Time.time);
            rigInstance.Spawns.End(Time.time);
            rigInstance.Dealt.End();
        }

        // A window close never reaches Shutdown, so the run loop cannot write this itself.
        private void OnApplicationQuit() => RecordAbandonedRun();

        private void RecordAbandonedRun()
        {
            if (!runOpen || !rigInstance) return;
            runOpen = false;
            EndRun();
            CloseOpenSector();
            var endedIn = course.Sectors.Count > 0
                ? course.Sectors[course.Sectors.Count - 1].sector - 1
                : activeSectorIndex;
            AppendRunRecord(runLoadoutHash, endedIn, null, runLocales, true, out _);
        }

        private void OpenSector(int index)
        {
            if (course == null) return;
            var damage = rigInstance.Player.Damage;
            course.Enter(index + 1, Time.time, damage.Health.Pct, damage.Shield.Pct);
            rigInstance.Recovery.Open();
        }

        private void CloseOpenSector()
        {
            if (course == null || !course.IsOpen) return;
            if (sectorEnded && !playerDied && !quitRun) clearedSectors = activeSectorIndex + 1;
            var damage = rigInstance.Player.Damage;
            course.End(sectorEnded ? sectorEndTime : Time.time, rigInstance.Spawns.KillsSinceSectorStart(),
                damage.Health.Pct, damage.Shield.Pct, rigInstance.Spawns.EliteMet(activeSectorIndex),
                rigInstance.Spawns.EliteBeaten(activeSectorIndex), rigInstance.Recovery.Close());
        }

        // An unreadable build identity leaves the run unrecorded, like a failed write.
        private Scoreboard AppendRunRecord(string playerLoadoutStatHash, int endedIn, DamageInfo? killingBlow,
            LocaleSky[] locales, bool abandoned, out RunRecord? appended)
        {
            appended = null;
            if (!buildIdentity(out var identity, out var failure))
            {
                Debug.LogError($"Run record skipped, build identity unreadable: {failure}", this);
                return Scoreboard.Read(runRecords.Path, null);
            }

            var sectorsCleared = abandoned ? clearedSectors : killingBlow.HasValue ? endedIn : endedIn + 1;
            var record = RunRecord.Compose(DateTime.UtcNow, sectorsCleared, endedIn, locales, identity,
                StatHash.OfSetting(hangarOffer, lullMods, sessionProfile.run.Sectors, minionKillHeal, rivalKillHeal), rigInstance.Loadout,
                playerLoadoutStatHash, rigInstance.Tally, rigInstance.Ledger, rigInstance.Spawns, killingBlow,
                course, MountedLoadout(rigInstance.Player), rigInstance.Mods.PickedNames(), ReadEndPhase(),
                Application.isEditor ? RunRecord.OriginEditor : RunRecord.OriginBuild, DealtRows(rigInstance.Dealt),
                abandoned);
            if (abandoned)
            {
                runRecords.Append(record);
                return Scoreboard.Read(runRecords.Path, null);
            }

            var board = Scoreboard.Read(runRecords.Path, record);
            if (!runRecords.Append(record)) return Scoreboard.Read(runRecords.Path, null);
            appended = record;
            return board;
        }

        private EndPhaseRow ReadEndPhase()
        {
            var sector = session != null ? session.ActiveSector : null;
            var spine = sector ? sector.Module<PhaseSpineModule>() : null;
            if (!spine) return default;
            return EndPhaseRow.Of(spine.Phase,
                rigInstance.Spawns.EliteMet(activeSectorIndex) && !rigInstance.Spawns.EliteBeaten(activeSectorIndex));
        }

        private static LoadoutRow MountedLoadout(Ship ship)
        {
            var weapons = ship.Weapons;
            return new LoadoutRow
            {
                chassis = SpawnLog.NameOf(ship),
                engine = SpawnLog.NameOf(ship.Engine),
                shield = SpawnLog.NameOf(ship.Shield),
                primary = SpawnLog.NameOf(weapons ? weapons.PrimaryMountPrefab : null),
                secondary = SpawnLog.NameOf(weapons ? weapons.SecondaryMountPrefab : null),
                statHash = StatHash.OfLoadout(ship),
            };
        }

        private static List<DealtRow> DealtRows(DamageDealt dealt)
        {
            var rows = new List<DealtRow>(dealt.Rows.Count);
            foreach (var row in dealt.Rows)
                rows.Add(new DealtRow
                {
                    sector = row.Sector,
                    kind = row.Kind.ToString(),
                    total = row.Total,
                    shield = row.Shield,
                    hull = row.Hull,
                    hits = row.Hits,
                });
            return rows;
        }

        private static List<string> AssetNames<T>(IReadOnlyList<T> assets) where T : UnityEngine.Object
        {
            var names = new List<string>(assets.Count);
            foreach (var asset in assets)
                names.Add(asset ? asset.name : string.Empty);
            return names;
        }

        private void RefillPlayerHull(ShipId victim, Vector3 diedAt)
        {
            var hull = rigInstance.Player.Damage.Health;
            var before = hull.CurrentValue;
            hull.Restore(PaysRivalHeal(victim) ? rivalKillHeal : minionKillHeal);
            var gained = hull.CurrentValue - before;
            if (gained > 0f && rigInstance.Overlay) rigInstance.Overlay.ShowHullGain(gained, diedAt);
        }

        private bool PaysRivalHeal(ShipId victim)
        {
            var sector = session != null ? session.ActiveSector : null;
            var spine = sector ? sector.Module<PhaseSpineModule>() : null;
            return (spine && spine.IsRival(victim)) || rigInstance.Spawns.IsElite(victim);
        }

        private void OnShipUnlocked(ShipUnlocked unlocked)
        {
            if (rigInstance.Overlay) rigInstance.Overlay.ShowShipUnlocked(unlocked.Chassis);
            moments.Raise(new ShipEarned(unlocked));
        }

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
                yield return RecapPlayback.RunScreen(sequence, ui, rigInstance.Screens, state =>
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

        private bool Introduces(Ship rival) => sessionProfile.presentation && rigInstance && rival;

        private void OnMomentRaised(IRunMoment moment)
        {
            if (moment is RivalIntroduced introduced) closeUpRival = introduced.Rival;
        }

        private bool Teaches(int sectorIndex, bool presentation, PhaseSpineModule spine) =>
            sectorIndex == 0 && teachFirstSector && presentation && TutorialSetting.On && rigInstance &&
            rigInstance.Overlay && spine && spine.Hunt;

        private IEnumerator RunTutorialStations(FindStation[] finds)
        {
            var labels = new List<(string text, Color tint, Vector3 at)>();
            foreach (var find in finds)
            {
                if (!find) continue;
                foreach (var station in find.Stations)
                    if (!station.Spent)
                        labels.Add((find.Kind.ToString().ToUpperInvariant(), FindColours.Of(find.Kind), station.Position));
            }
            if (labels.Count == 0) yield break;

            var min = GamePlane.WorldPointToPlane(rigInstance.Player.transform.position);
            var max = min;
            foreach (var label in labels)
            {
                var at = GamePlane.WorldPointToPlane(label.at);
                min = Vector2.Min(min, at);
                max = Vector2.Max(max, at);
            }
            var center = (min + max) / 2f;
            var half = (max - min) / 2f + Vector2.one * tutorialFeel.stationsMargin;
            var size = Mathf.Max(half.y, half.x / observer.Cam.aspect);
            yield return RunTutorialShot(FirstSectorTutorial.StationsText, tutorialFeel.stationsZoom, () => center, size,
                labels);
        }

        private IEnumerator RunTutorialPatrol(Ship patrol)
        {
            var feel = tutorialFeel.introPan;
            Func<Vector2?> target = () => patrol && patrol.gameObject.activeInHierarchy
                ? GamePlane.WorldPointToPlane(patrol.transform.position)
                : (Vector2?)null;
            yield return RunTutorialShot(FirstSectorTutorial.IntroText, feel, patrol ? target : null, feel.closeSize,
                Array.Empty<(string, Color, Vector3)>());
        }

        // Fire skips only after the delay and a release, so held fire never skips.
        // Its own action, because the frozen ship's flight input is disabled.
        private IEnumerator RunTutorialShot(string text, CloseUpFeel feel, Func<Vector2?> target, float size,
            IReadOnlyList<(string text, Color tint, Vector3 at)> labels)
        {
            var fire = new InputAction("TutorialSkip", InputActionType.Button, "<Mouse>/leftButton");
            fire.Enable();
            var shownAt = Time.unscaledTime;
            var released = false;
            var skipped = false;
            var showing = true;
            bool Skippable() => Time.unscaledTime - shownAt >= tutorialFeel.introSkipDelaySeconds;
            bool Cancelled()
            {
                if (Skippable() && !skipped)
                {
                    if (!fire.IsPressed()) released = true;
                    else if (released) skipped = true;
                }
                return skipped || playerDied || sectorEnded || shutdownRequested || quitRun || pauseRequested;
            }

            rigInstance.Overlay.ShowTutorialCard(text, TutorialCard.Spot.High, () => !showing, Skippable);
            foreach (var label in labels)
                rigInstance.Overlay.ShowWorldLabel(label.text, label.tint, label.at, () => !showing);
            rigInstance.SetInputEnabled(false);
            try
            {
                if (target != null)
                    yield return CloseUp.Run(feel, observer, rigInstance.Player.transform, target, size,
                        clock.Slow(1f), Cancelled);
                else
                    for (var t = 0f; t < feel.holdSeconds && !Cancelled(); t += Time.unscaledDeltaTime)
                        yield return null;
            }
            finally
            {
                showing = false;
                fire.Dispose();
                rigInstance.SetInputEnabled(true);
            }
        }

        private IEnumerator RunCloseUp(Ship rival)
        {
            rigInstance.SetInputEnabled(false);
            try
            {
                yield return CloseUp.Run(closeUpFeel, observer, rigInstance.Player.transform, rival,
                    clock.Slow(1f), () => playerDied || sectorEnded || shutdownRequested || quitRun || pauseRequested);
            }
            finally
            {
                rigInstance.SetInputEnabled(true);
            }
        }

        private IEnumerator AskFeedback(RunRecord run)
        {
            var screen = RunFeedbackScreen.Create(ui, rigInstance.Screens);
            try
            {
                while (screen.Note == null && !shutdownRequested) yield return null;
                if (!string.IsNullOrEmpty(screen.Note))
                    runRecords.AppendFeedback(RunFeedback.Of(run, DateTime.UtcNow, screen.Note));
            }
            finally { Destroy(screen.gameObject); }
        }

        // The world plays on behind the victory recap: the winner can't fly out of the held view or be shot down.
        private void StandDown(bool down)
        {
            rigInstance.SetInputEnabled(!down);
            rigInstance.Player.Damage.SetInvulnerability(down ? float.PositiveInfinity : 0f);
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
