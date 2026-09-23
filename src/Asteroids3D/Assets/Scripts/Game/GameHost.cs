using System;
using System.Collections;
using Cameras;
using Damage;
using Substrate.Presentation;
using Substrate.Sectors;
using Substrate.Sessions;
using Ships.Loadout;
using UI;
using UnityEngine;
using UI.Screens;
using Ships.Registry;
using Substrate.Services.Units;
using Substrate.Services.Objectives;

namespace Game
{
    /// <summary>
    /// The interactive game's host: the scene object that wraps one <see cref="Session"/> and runs the
    /// game over it as one straight-line coroutine — compose the session, build the viewport (the
    /// observer camera) and the optional <see cref="PlayerRig"/>, then loop runs: hangar, load the
    /// sector, play until the sector ends or the player dies, death recap, unload. It owns the clock,
    /// splash, hangar, recap and restart; the session only composes, loads and unloads. Presentation
    /// is read from the profile once, beside the session's own snapshot, and handed down to each step.
    /// The hangar, recap and restart stand in for Home Base, multi-sector runs and player progress;
    /// why the host grows in place: https://github.com/amindell11/astronomical-home/issues/295#issuecomment-5787867584
    /// </summary>
    [RequireComponent(typeof(UnitService))]
    [RequireComponent(typeof(ObjectiveService))]
    public class GameHost : MonoBehaviour
    {
        // Values are pinned: scenes serialize the numbers, so renumbering rewrites authored data.
        /// <summary>Session policy for what happens when the persistent player ship dies.</summary>
        public enum PlayerDeathBehavior { None = 0, RestartSector = 2 }

        [Header("Session")]
        [SerializeField] private SessionProfile sessionProfile = new SessionProfile();

        [Tooltip("The player and its HUD. Built once at Start; persists across sector restarts. " +
                 "Null → no player (spectator).")]
        [SerializeField] private PlayerRig playerRig;

        [Header("View")]
        [Tooltip("Observer camera spawned once at session start and framed on the fleet; the player " +
                 "rig and the sector's modules are handed this instance.")]
        [SerializeField] internal ObserverCam observerCamPrefab;

        [Header("Splash")]
        [Tooltip("Full-screen splash shown over the non-interactive states (boot, session compose, " +
                 "sector load). Optional; skipped when presentation is off (headless/RL).")]
        [SerializeField] private LoadingSplash splashPrefab;

        [Header("Hangar")]
        [Tooltip("Between-run hangar screen. Null → no interactive hangar (the standing loadout is " +
                 "applied silently).")]
        [SerializeField] internal HangarScreen hangarScreenPrefab;

        [Tooltip("Modules the hangar offers per slot. Null → no hangar choices (the player flies its " +
                 "prefab-authored modules).")]
        [SerializeField] internal LoadoutConfig loadoutCatalog;

        [Header("Death Policy")]
        [Tooltip("What happens when the player ship dies. RestartSector runs the death recap and " +
                 "reloads the active sector; None does nothing.")]
        [SerializeField] private PlayerDeathBehavior deathBehavior = PlayerDeathBehavior.RestartSector;

        [Header("Death Recap")]
        [Tooltip("Seconds the death recap holds before auto-continuing; the Continue button skips " +
                 "the wait. Shown only under RestartSector with presentation on.")]
        [SerializeField, Min(0f)] private float recapHoldSeconds = 8f;

        private DamageInfo lastKillingBlow;
        private bool playerDied;
        private bool sectorEnded;

        private UnitService unitService;
        private ObjectiveService objectiveService;

        private Session session;
        private ObserverCam observer;
        private LoadingSplash splash;

        public Sector ActiveSector => session?.ActiveSector;

        private void Awake()
        {
            unitService = GetComponent<UnitService>();
            objectiveService = GetComponent<ObjectiveService>();

            DontDestroyOnLoad(gameObject);
            StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            // No yield separates this read from the session's own snapshot, so the two cannot disagree.
            var presentation = sessionProfile.presentation;
            session = new Session(sessionProfile, transform, unitService, objectiveService);
            if (splashPrefab && presentation)
                splash = Instantiate(splashPrefab, transform);

            SetSplashVisible(true);
            yield return null;

            yield return session.Compose();
            observer = BuildObserver(session.Units, presentation);
            if (playerRig)
                yield return playerRig.Build(session.Units, session.Objectives, presentation,
                    observer, session.Frame, BuildDeathCallback());

            while (true)
            {
                SetSplashVisible(false);
                if (playerRig)
                    yield return RunHangar(playerRig, presentation);

                SetSplashVisible(true);
                playerDied = false;
                sectorEnded = false;
                yield return session.LoadSector(playerRig ? playerRig.Player : null, _ => sectorEnded = true);
                SetSplashVisible(false);

                // Run-end signals latch, so one arriving mid-load or mid-recap never cuts that step short.
                while (!playerDied && !sectorEnded)
                    yield return null;

                if (playerDied && presentation)
                    yield return RunDeathRecap();

                SetSplashVisible(true);
                yield return session.UnloadSector();
            }
        }

        private void SetSplashVisible(bool visible)
        {
            if (splash) splash.SetVisible(visible);
        }

        /// <summary>Stays callable without a session so the presentation gate can be driven directly.</summary>
        internal ObserverCam BuildObserver(IUnitService units, bool presentationEnabled)
        {
            var built = Instantiate(observerCamPrefab);

            // The authored prefab clears to the skybox; a non-presenting session must not render one.
            if (!presentationEnabled)
            {
                built.Cam.clearFlags = CameraClearFlags.SolidColor;
                built.Cam.backgroundColor = Color.black;
            }

            // The camera carries authored presentation of its own (the starfield backdrop, the reverb zone).
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
                        lastKillingBlow = killingBlow;
                        playerDied = true;
                    };
                case PlayerDeathBehavior.None:
                default:
                    return null;
            }
        }

        /// <summary>Interactive hangar flow; applies the standing loadout silently when not presenting (never blocks on a click) and stays callable without a session for tests.</summary>
        internal IEnumerator RunHangar(PlayerRig rig, bool presentationEnabled)
        {
            if (!rig || !rig.Player || rig.Loadout == null || !hangarScreenPrefab || !presentationEnabled)
            {
                if (rig) rig.ApplyLoadout();
                yield break;
            }

            var overlay = rig.Overlay;
            if (overlay) overlay.SetVisible(false);
            SetPlayerInputEnabled(rig, false);

            var screen = Instantiate(hangarScreenPrefab);
            var launched = false;
            screen.Show(loadoutCatalog, rig.Loadout, () => launched = true);

            yield return new WaitUntil(() => launched);

            rig.ApplyLoadout();
            Destroy(screen.gameObject);

            // ApplyLoadout may rebuild the player and re-bind the HUD — refs from before it are stale.
            SetPlayerInputEnabled(rig, true);
            var activeOverlay = rig.Overlay;
            if (activeOverlay) activeOverlay.SetVisible(true);
        }

        // Fire1 shares mouse 0 with UI clicks, so the commander sleeps for the hangar screen's lifetime.
        private static void SetPlayerInputEnabled(PlayerRig rig, bool inputEnabled)
        {
            if (rig.Player && rig.Player.Commander)
                rig.Player.Commander.enabled = inputEnabled;
        }

        private IEnumerator RunDeathRecap()
        {
            var overlay = playerRig.Overlay;
            if (overlay) overlay.SetVisible(false);
            SetPlayerInputEnabled(playerRig, false);

            var screen = DeathRecapScreen.Create();
            var dismissed = false;
            screen.Show(lastKillingBlow, playerRig.Ledger.Rows, () => dismissed = true);

            var deadline = Time.unscaledTime + recapHoldSeconds;
            yield return new WaitUntil(() => dismissed || Time.unscaledTime >= deadline);

            Destroy(screen.gameObject);
            SetPlayerInputEnabled(playerRig, true);
            if (overlay) overlay.SetVisible(true);
        }
    }
}
