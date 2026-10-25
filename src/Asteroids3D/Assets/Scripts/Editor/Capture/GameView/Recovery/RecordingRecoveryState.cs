using System;

namespace Capture.GameView.Recovery
{
    [Serializable]
    internal sealed class RecordingRecoveryState
    {
        public string unityVersion;
        public bool runInBackground;
        public CaptureRecoveryState.GameViewState gameView;
    }
}
