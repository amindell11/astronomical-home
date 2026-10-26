using System;
using UnityEngine;

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
        public bool hasPresentation;
        public int qualityLevel;
        public int ambientMode;
        public Color ambientLight;
        public bool fog;
        public bool gameViewAsyncShaders;
        public bool overlayFound;
        public int overlayValue;
        public bool overlayHasValue;

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
