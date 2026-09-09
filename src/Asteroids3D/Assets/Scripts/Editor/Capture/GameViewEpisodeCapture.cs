using System;
using System.Collections.Generic;
using System.IO;
using Game.Services;
using Ships;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Input;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Capture.GameView
{
    [InitializeOnLoad]
    public sealed class GameViewEpisodeCapture : ScriptableObject, IEpisodeCapture
    {
        private static GameViewEpisodeCapture active;

        private readonly List<Object> selectedSubjects = new();

        private CaptureConfig config;
        private Vector2[] framedSubjects;
        private IReadOnlyList<Ship> subjects;
        private IProjectileService projectiles;
        private GameObject rig;
        private Camera captureCamera;
        private CaptureArtifacts artifacts;
        private CaptureCost cost;
        private GameViewCaptureTransaction transaction;
        private RecorderController controller;
        private RecorderControllerSettings controllerSettings;
        private ImageRecorderSettings recorderSettings;
        private Object[] appliedSelection = Array.Empty<Object>();
        private string frameDir;
        private int activationFrame;
        private bool gameViewPrimed;
        private bool recordingStarted;

        static GameViewEpisodeCapture()
        {
            AssemblyReloadEvents.beforeAssemblyReload += RestoreActive;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.quitting += RestoreActive;
            EditorApplication.delayCall += RecoverJournal;
        }

        public void Begin(CaptureConfig captureConfig, GizmoCaptureProfile profile, IReadOnlyList<Ship> captureSubjects,
            IProjectileService projectileService)
        {
            if (active) throw new InvalidOperationException("A native Game View capture transaction is already active.");
            config = captureConfig ?? throw new ArgumentNullException(nameof(captureConfig));
            subjects = captureSubjects ?? throw new ArgumentNullException(nameof(captureSubjects));
            if (subjects.Count == 0)
                throw new ArgumentException("Native Game View capture needs at least one subject to film.");
            for (var i = 0; i < subjects.Count; i++)
                if (!subjects[i])
                    throw new ArgumentException($"Native Game View capture subject {i} is missing.");
            framedSubjects = new Vector2[subjects.Count];
            projectiles = projectileService ?? throw new ArgumentNullException(nameof(projectileService));

            active = this;
            try
            {
                artifacts = new CaptureArtifacts(config);
                cost = new CaptureCost();
                frameDir = artifacts.FrameDir;
                BuildSelection();
                transaction = new GameViewCaptureTransaction(GizmoCaptureProfiles.Resolve(profile), appliedSelection,
                    subjects[0].gameObject, config.width, config.height, MapScope(config.gizmoScope), config.gizmoScopeTeam);
                CreateRig();
                FrameCamera();
                activationFrame = Time.renderedFrameCount;
            }
            catch
            {
                End();
                throw;
            }
        }

        public string FrameDir => frameDir;

        public void Step()
        {
            if (transaction == null) throw new InvalidOperationException("Native Game View capture has not begun.");
            for (var i = 0; i < subjects.Count; i++)
                if (!subjects[i])
                    throw new InvalidOperationException("Native Game View capture lost a filmed subject.");

            cost.Sample();
            FrameCamera();
            BuildSelection();
            transaction.SetSelection(appliedSelection, subjects[0].gameObject);
            if (!gameViewPrimed)
            {
                if (Time.renderedFrameCount <= activationFrame) return;
                gameViewPrimed = true;
            }
            if (recordingStarted) return;
            CreateRecorder();
            recordingStarted = controller.StartRecording();
            if (!recordingStarted) throw new InvalidOperationException("Unity Recorder refused to start Game View capture.");
        }

        public void End()
        {
            if (active != this && transaction == null && controller == null && !rig) return;

            var failures = new List<Exception>();
            CaptureRecoveryJournal.Attempt(() => controller?.StopRecording(), failures);
            CaptureRecoveryJournal.Attempt(() => artifacts?.Complete(cost), failures);
            CaptureRecoveryJournal.Attempt(() => transaction?.Restore(), failures);
            CaptureRecoveryJournal.Attempt(DestroyOwnedObjects, failures);
            controller = null;
            transaction = null;
            artifacts = null;
            cost = null;
            gameViewPrimed = false;
            recordingStarted = false;
            subjects = null;
            framedSubjects = null;
            projectiles = null;
            if (active == this) active = null;
            if (failures.Count > 0) throw new AggregateException("Native Game View capture cleanup failed.", failures);
        }

        private void OnDestroy()
        {
            if (active != this) return;
            try { End(); }
            catch (Exception exception) { Debug.LogException(exception); }
        }

        private static Game.Diagnostics.GizmoScope MapScope(CaptureGizmoScope scope) => scope switch
        {
            CaptureGizmoScope.All => Game.Diagnostics.GizmoScope.All,
            CaptureGizmoScope.Selected => Game.Diagnostics.GizmoScope.Selected,
            CaptureGizmoScope.Team => Game.Diagnostics.GizmoScope.Team,
            _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, "Unmapped capture gizmo scope."),
        };

        private void CreateRig()
        {
            rig = new GameObject("[Capture] Game View Rig");
            captureCamera = rig.AddComponent<Camera>();
            captureCamera.orthographic = true;
            captureCamera.clearFlags = CameraClearFlags.SolidColor;
            captureCamera.backgroundColor = new Color(0.02f, 0.02f, 0.05f);
            captureCamera.depth = 100f;

            var lightRig = new GameObject("light");
            lightRig.transform.SetParent(rig.transform);
            var directional = lightRig.AddComponent<Light>();
            directional.type = LightType.Directional;
            directional.transform.rotation = Quaternion.LookRotation(
                GamePlane.Rotation * new Vector3(0.4f, -0.3f, 1f));
        }

        private void FrameCamera()
        {
            for (var i = 0; i < subjects.Count; i++) framedSubjects[i] = subjects[i].Kinematics.pos;
            CaptureFraming.Apply(captureCamera, config, framedSubjects);
        }

        private void BuildSelection()
        {
            selectedSubjects.Clear();
            for (var i = 0; i < subjects.Count; i++) AddHierarchy(subjects[i].transform);
            projectiles.ForEachLive(projectile => AddHierarchy(projectile.transform));
            selectedSubjects.Sort((left, right) => left.GetInstanceID().CompareTo(right.GetInstanceID()));
            if (SameSelection()) return;
            appliedSelection = selectedSubjects.ToArray();
        }

        private void AddHierarchy(Transform root)
        {
            selectedSubjects.Add(root.gameObject);
            for (var i = 0; i < root.childCount; i++) AddHierarchy(root.GetChild(i));
        }

        private bool SameSelection()
        {
            if (appliedSelection.Length != selectedSubjects.Count) return false;
            for (var i = 0; i < appliedSelection.Length; i++)
                if (appliedSelection[i] != selectedSubjects[i])
                    return false;
            return true;
        }

        private void CreateRecorder()
        {
            controllerSettings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
            controllerSettings.SetRecordModeToManual();
            controllerSettings.FrameRatePlayback = FrameRatePlayback.Variable;
            // Recorder cadence follows simulation; captureEveryNthFrame controls PNG cadence.
            controllerSettings.FrameRate = 1f / Time.fixedDeltaTime;
            controllerSettings.CapFrameRate = false;
            controllerSettings.ExitPlayMode = false;

            recorderSettings = ScriptableObject.CreateInstance<ImageRecorderSettings>();
            recorderSettings.name = "Native Gizmo PNG Recorder";
            recorderSettings.Enabled = true;
            recorderSettings.OutputFormat = ImageRecorderSettings.ImageRecorderOutputFormat.PNG;
            recorderSettings.CaptureAlpha = false;
            recorderSettings.imageInputSettings = new GameViewInputSettings
            {
                OutputWidth = config.width,
                OutputHeight = config.height,
            };
            var serialized = new SerializedObject(recorderSettings);
            var cadence = serialized.FindProperty("captureEveryNthFrame") ??
                          throw new MissingFieldException("Unity Recorder 5.1.2 captureEveryNthFrame");
            cadence.intValue = config.everyFixedSteps;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            recorderSettings.OutputFile = Path.Combine(frameDir, "f_0") + DefaultWildcard.Frame;

            controllerSettings.AddRecorderSettings(recorderSettings);
            controller = new RecorderController(controllerSettings);
            controller.PrepareRecording();
        }

        private void DestroyOwnedObjects()
        {
            if (recorderSettings) DestroyImmediate(recorderSettings);
            if (controllerSettings) DestroyImmediate(controllerSettings);
            if (rig) DestroyImmediate(rig);
            recorderSettings = null;
            controllerSettings = null;
            rig = null;
            captureCamera = null;
        }

        private static void RestoreActive()
        {
            if (!active) return;
            try { active.End(); }
            catch (Exception exception) { Debug.LogException(exception); }
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode) RestoreActive();
        }

        private static void RecoverJournal()
        {
            if (!CaptureRecoveryJournal.Exists || active) return;
            try { CaptureRecoveryJournal.Recover(); }
            catch (Exception exception) { Debug.LogException(exception); }
        }
    }
}
