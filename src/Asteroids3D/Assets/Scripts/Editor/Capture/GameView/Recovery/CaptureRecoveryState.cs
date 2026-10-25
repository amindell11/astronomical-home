using System;

namespace Capture.GameView.Recovery
{
    [Serializable]
    internal sealed class CaptureRecoveryState
    {
        public string unityVersion;
        public bool runInBackground;
        public bool renderGraphCompatibilityMode;
        public bool renderPipelineSettingsDirty;
        public GameViewState gameView;
        public GizmoState[] gizmos;

        [Serializable]
        public sealed class GameViewState
        {
            public bool hadMainView;
            public int viewType;
            public bool focused;
            public int width;
            public int height;
            public bool wasGameView;
            public bool drawGizmos;
            public int selectedSizeIndex;
        }

        [Serializable]
        public sealed class GizmoState
        {
            public int index;
            public string name;
            public string scriptPath;
            public bool gizmoEnabled;
            public bool iconEnabled;
        }
    }

}
