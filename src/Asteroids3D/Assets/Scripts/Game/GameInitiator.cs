using System;
using System.Collections;
using AI;
using Game.Session;
using Player;
using Ships;
using UnityEngine;
using Utils;

namespace Game
{
    public class GameInitiator : MonoBehaviour, ISectorSessionOrchestrator
    {
        [SerializeField] private GameConfig gameConfig;
        [SerializeField] private Transform referencePlane;
        [SerializeField] private ShipRespawnRunner respawnRunner;

        private SessionContext sessionContext;
        private SessionEnvironmentLoader environmentLoader;
        private SessionRuntimeBuilder runtimeBuilder;
        private SessionPresentationBinder presentationBinder;
        private Coroutine sessionRoutine;
        private bool isInitialized;
        private bool worldSceneLoadedBySession;

        public event Action<Ship, Camera> PresentationReady;

        public ShipRegistry ShipRegistry => sessionContext?.ShipRegistry;
        public bool IsSessionActive => isInitialized;

        private Transform WorldFollowerTransform => sessionContext?.WorldFollowerTransform;

        private void Awake()
        {
            ValidateSerializedDependencies();
            EnsurePhaseModules();

            if (!gameConfig)
                throw new ArgumentNullException(nameof(gameConfig));

            StartSession(SectorSessionConfig.FromGameConfig(gameConfig));
        }

        public Coroutine StartSession(SectorSessionConfig config)
        {
            if (sessionRoutine != null || isInitialized)
                return sessionRoutine;

            sessionRoutine = StartCoroutine(StartSessionRoutine(config));
            return sessionRoutine;
        }

        public Coroutine RestartSession(SectorSessionConfig config = null)
        {
            var nextConfig = config ?? sessionContext?.Config ?? (gameConfig ? SectorSessionConfig.FromGameConfig(gameConfig) : null);
            if (nextConfig == null)
                throw new ArgumentNullException(nameof(config));

            StopSession();
            return StartSession(nextConfig);
        }

        public IEnumerator Initialize(GameConfig config)
        {
            if (sessionRoutine != null || isInitialized)
                yield break;

            sessionRoutine = StartCoroutine(StartSessionRoutine(SectorSessionConfig.FromGameConfig(config)));
            yield return sessionRoutine;
        }

        public void StopSession()
        {
            if (sessionRoutine != null)
            {
                StopCoroutine(sessionRoutine);
                sessionRoutine = null;
            }

            UnbindShipRegistry();

            if (respawnRunner)
                respawnRunner.ResetRunner();

            DestroySessionObjects();

            sessionContext?.ShipRegistry?.Dispose();
            GamePlane.Reset();

            sessionContext = null;
            isInitialized = false;
            worldSceneLoadedBySession = false;
        }

        public void Shutdown()
        {
            StopSession();
        }

        private IEnumerator StartSessionRoutine(SectorSessionConfig config)
        {
            if (isInitialized)
                yield break;

            if (config == null)
                throw new ArgumentNullException(nameof(config));
            if (!config.GameConfig)
                throw new ArgumentNullException(nameof(config.GameConfig));

            ValidateSerializedDependencies();
            EnsurePhaseModules();

            var startupSucceeded = false;
            try
            {
                gameConfig = config.GameConfig;
                sessionContext = new SessionContext(config, respawnRunner);
                isInitialized = true;

                yield return LoadEnvironment(config);
                BuildRuntimeServices(config);
                SpawnActors(config);
                BindPresentation(config.GameConfig);
                StartSessionFlow();

                startupSucceeded = true;
            }
            finally
            {
                sessionRoutine = null;
                if (!startupSucceeded)
                    TeardownFailedStartup();
            }
        }

        private void EnsurePhaseModules()
        {
            environmentLoader ??= new SessionEnvironmentLoader(referencePlane);
            runtimeBuilder ??= new SessionRuntimeBuilder();
            presentationBinder ??= new SessionPresentationBinder(() => WorldFollowerTransform);
        }

        private void TeardownFailedStartup()
        {
            isInitialized = false;

            UnbindShipRegistry();

            if (respawnRunner)
                respawnRunner.ResetRunner();

            DestroySessionObjects();

            sessionContext?.ShipRegistry?.Dispose();
            GamePlane.Reset();

            sessionContext = null;
            worldSceneLoadedBySession = false;
        }

        private IEnumerator LoadEnvironment(SectorSessionConfig config)
        {
            GamePlane.SetReferencePlane(referencePlane);
            yield return StartCoroutine(environmentLoader.LoadEnvironment(config, MarkWorldSceneLoadedBySession));
        }

        private void MarkWorldSceneLoadedBySession()
        {
            worldSceneLoadedBySession = true;
        }

        private void BuildRuntimeServices(SectorSessionConfig _config)
        {
            runtimeBuilder.BuildRuntimeServices(sessionContext);
        }

        private void SpawnActors(SectorSessionConfig _config)
        {
            runtimeBuilder.SpawnActors(sessionContext, WireShipDependencies, () => WorldFollowerTransform);
        }

        private void BindPresentation(GameConfig _config)
        {
            presentationBinder.Bind(sessionContext);
        }

        private void StartSessionFlow()
        {
            ValidateRuntimeWiring();
            PublishPresentationReady();
        }

        private void WireShipDependencies(Ship ship)
        {
            if (!ship)
                return;

            ship.Targeting?.SetRegistry(ShipRegistry);
            if (ship.Commander is AICommander aiCommander)
                aiCommander.SetRegistry(ShipRegistry);
        }

        private void ValidateRuntimeWiring()
        {
            ValidateShipWiring(sessionContext.Player);
            if (sessionContext.Enemy)
                ValidateShipWiring(sessionContext.Enemy);

            if (sessionContext.Player?.Commander is PlayerCommander { HasScreenProjectorConfigured: false })
                throw new InvalidOperationException("PlayerCommander requires a configured screen-to-plane projector.");

            if (!respawnRunner || !respawnRunner.IsInitialized)
                throw new InvalidOperationException("ShipRespawnRunner must be initialized before gameplay starts.");

            if (GamePlane.Plane != referencePlane)
                throw new InvalidOperationException("GamePlane must be configured from the serialized reference plane.");
        }

        private void ValidateSerializedDependencies()
        {
            if (!referencePlane)
                throw new InvalidOperationException("GameInitiator requires a serialized reference plane Transform.");

            if (!respawnRunner)
                throw new InvalidOperationException("GameInitiator requires a scene-owned ShipRespawnRunner reference.");
        }

        private static void ValidateShipWiring(Ship ship)
        {
            if (!ship)
                throw new InvalidOperationException("Ship must be created before validation.");

            if (ship.Targeting && !ship.Targeting.HasRegistry)
                throw new InvalidOperationException($"TargetingComputer on ship '{ship.name}' is missing IShipRegistry wiring.");

            if (ship.Commander is AICommander { HasRegistryConfigured: false })
                throw new InvalidOperationException($"AICommander on ship '{ship.name}' is missing IShipRegistry wiring.");
        }

        private void DestroySessionObjects()
        {
            if (sessionContext == null)
                return;

            if (sessionContext.CameraRig)
                Destroy(sessionContext.CameraRig.gameObject);
            if (sessionContext.AsteroidField)
                Destroy(sessionContext.AsteroidField.gameObject);
            if (sessionContext.World)
                Destroy(sessionContext.World.gameObject);
            if (sessionContext.Player)
                Destroy(sessionContext.Player.gameObject);
            if (sessionContext.Enemy)
                Destroy(sessionContext.Enemy.gameObject);

            UnloadWorldScene(sessionContext.Config);
        }

        private void UnloadWorldScene(SectorSessionConfig config)
        {
            environmentLoader.UnloadOwnedWorldScene(config, worldSceneLoadedBySession);
            worldSceneLoadedBySession = false;
        }

        private void UnbindShipRegistry()
        {
            presentationBinder?.Unbind();
        }

        private void PublishPresentationReady()
        {
            if (sessionContext?.Player == null || sessionContext.CameraRig == null)
                return;
            PresentationReady?.Invoke(sessionContext.Player, sessionContext.CameraRig.UICamera);
        }

        private void OnDestroy()
        {
            StopSession();
        }
    }
}
