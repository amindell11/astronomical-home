using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using Capture.GameView.Recovery;
using Capture.Recording;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Capture.GameView.Recording
{
    internal sealed class GameViewRecording : CaptureRecording
    {
        private readonly CaptureRequest request;
        private readonly RecordingRecoveryState recovery;
        private readonly int preparationFrame;
        private readonly Action released;
        private readonly EditorWindow focusedWindow;
        private CaptureArtifacts artifacts;
        private NativeFrameWriter writer;
        private CapturePump pump;
        private ExceptionDispatchInfo failure;
        private bool prepared;
        private bool started;
        private bool disposed;

        public GameViewRecording(CaptureRequest request, Action released)
        {
            this.request = request;
            this.released = released;
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null || Application.isBatchMode)
                throw new InvalidOperationException("Native Game View recording requires a windowed graphics Editor.");
            var view = new UnityGameViewAdapter();
            focusedWindow = EditorWindow.focusedWindow;
            recovery = new RecordingRecoveryState
            {
                unityVersion = Application.unityVersion,
                runInBackground = Application.runInBackground,
                gameView = view.Snapshot(),
            };
            CaptureRecoveryJournal.WriteRecording(recovery);
            try
            {
                artifacts = new CaptureArtifacts(request);
                view.Prepare(request.Width, request.Height, recovery.gameView.drawGizmos);
                Application.runInBackground = true;
                writer = new NativeFrameWriter(request.Width, request.Height);
                pump = new GameObject("[Capture] Frame writer").AddComponent<CapturePump>();
                preparationFrame = Time.renderedFrameCount;
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public override IEnumerator Ready => WaitForView();

        private IEnumerator WaitForView()
        {
            var deadline = Time.realtimeSinceStartupAsDouble + 30;
            while (Time.renderedFrameCount <= preparationFrame || Screen.width != request.Width || Screen.height != request.Height)
            {
                RequireOpen();
                if (Time.realtimeSinceStartupAsDouble >= deadline)
                {
                    Dispose();
                    throw new TimeoutException("Game View did not render the requested capture dimensions within 30 seconds.");
                }
                yield return null;
            }
            RequireOpen();
            prepared = true;
        }

        public override void Start()
        {
            RequireOpen();
            if (!prepared || started) throw new InvalidOperationException("Capture Start requires completed Ready and one activation.");
            started = true;
            pump.Begin(Acquire);
        }

        private bool Acquire()
        {
            try
            {
                var unscaled = Time.unscaledTimeAsDouble;
                var sim = Time.timeAsDouble;
                var values = new List<double>(request.Measurements.Count);
                foreach (var reader in request.Measurements)
                {
                    var value = reader.Value();
                    if (!double.IsFinite(value)) throw new InvalidOperationException($"Capture measurement '{reader.Key}' is not finite.");
                    values.Add(value);
                }
                var id = artifacts.FrameCount + 1;
                writer.Write(artifacts.FramePath(id));
                artifacts.AppendFrame(id, unscaled, sim, values);
                return true;
            }
            catch (Exception exception)
            {
                failure = ExceptionDispatchInfo.Capture(exception);
                return false;
            }
        }

        public override CaptureResult Finish()
        {
            RequireOpen();
            try
            {
                failure?.Throw();
                if (!started || artifacts.FrameCount == 0)
                    throw new InvalidOperationException("Capture Finish requires Start and at least one acquired Game View frame.");
                pump.StopAllCoroutines();
                var result = artifacts.Complete();
                Dispose();
                return result;
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public override void Dispose()
        {
            if (disposed) return;
            disposed = true;
            var failures = new List<Exception>();
            CaptureRecoveryJournal.Attempt(() => { if (pump) Object.DestroyImmediate(pump.gameObject); }, failures);
            CaptureRecoveryJournal.Attempt(() => writer?.Dispose(), failures);
            CaptureRecoveryJournal.Attempt(() => artifacts?.Dispose(), failures);
            CaptureRecoveryJournal.Attempt(() => CaptureRecoveryJournal.RestoreRecording(recovery), failures);
            CaptureRecoveryJournal.Attempt(() => { if (focusedWindow) focusedWindow.Focus(); }, failures);
            released();
            if (failures.Count > 0) throw new AggregateException("Game View recording cleanup failed.", failures);
        }

        private void RequireOpen()
        {
            if (disposed) throw new ObjectDisposedException(nameof(CaptureRecording));
        }
    }
}
