using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Game.Capture.GameView
{
    internal sealed class UnityGameViewAdapter
    {
        internal const string ExpectedUnityVersion = "6000.1.8f1";

        private const BindingFlags StaticFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private readonly Type gameViewType;
        private readonly MethodInfo getMainPlayModeView;
        private readonly PropertyInfo drawGizmos;
        private readonly PropertyInfo selectedSizeIndex;

        public UnityGameViewAdapter()
        {
            if (Application.unityVersion != ExpectedUnityVersion)
                throw new NotSupportedException(
                    $"Native Game View capture is pinned to Unity {ExpectedUnityVersion}; running {Application.unityVersion}.");

            var editorAssembly = typeof(EditorWindow).Assembly;
            var playModeViewType = RequireType(editorAssembly, "UnityEditor.PlayModeView");
            gameViewType = RequireType(editorAssembly, "UnityEditor.GameView");
            getMainPlayModeView = RequireMethod(playModeViewType, "GetMainPlayModeView", StaticFlags);
            drawGizmos = RequireProperty(gameViewType, "drawGizmos");
            selectedSizeIndex = RequireProperty(gameViewType, "selectedSizeIndex");
        }

        public CaptureRecoveryState.GameViewState Snapshot()
        {
            var main = MainView();
            if (!main) return new CaptureRecoveryState.GameViewState();

            PlayModeWindow.GetRenderingResolution(out var width, out var height);
            var state = new CaptureRecoveryState.GameViewState
            {
                hadMainView = true,
                viewType = (int)PlayModeWindow.GetViewType(),
                focused = PlayModeWindow.GetPlayModeFocused(),
                width = (int)width,
                height = (int)height,
                wasGameView = gameViewType.IsInstanceOfType(main),
            };
            if (!state.wasGameView) return state;

            state.drawGizmos = (bool)drawGizmos.GetValue(main);
            state.selectedSizeIndex = (int)selectedSizeIndex.GetValue(main);
            return state;
        }

        public void Prepare(int width, int height)
        {
            PlayModeWindow.SetViewType(PlayModeWindow.PlayModeViewTypes.GameView);
            var gameView = MainView();
            if (!gameView || !gameViewType.IsInstanceOfType(gameView))
                throw new InvalidOperationException("Unity did not create a Game View for native gizmo capture.");

            drawGizmos.SetValue(gameView, true);
            PlayModeWindow.SetPlayModeFocused(true);
            PlayModeWindow.SetCustomRenderingResolution((uint)width, (uint)height, "Recording Resolution");
            gameView.Focus();
            gameView.Repaint();
        }

        public void Restore(CaptureRecoveryState.GameViewState state)
        {
            if (!state.hadMainView)
            {
                var owned = MainView();
                if (owned) owned.Close();
                return;
            }

            var oldType = (PlayModeWindow.PlayModeViewTypes)state.viewType;
            PlayModeWindow.SetViewType(oldType);
            PlayModeWindow.SetPlayModeFocused(state.focused);

            if (!state.wasGameView)
            {
                PlayModeWindow.SetCustomRenderingResolution((uint)state.width, (uint)state.height,
                    "Restored Capture Resolution");
                return;
            }

            var gameView = MainView();
            if (!gameView || !gameViewType.IsInstanceOfType(gameView))
                throw new InvalidOperationException("Unity did not restore the captured Game View instance.");
            drawGizmos.SetValue(gameView, state.drawGizmos);
            selectedSizeIndex.SetValue(gameView, state.selectedSizeIndex);
            gameView.Repaint();
        }

        private EditorWindow MainView() => getMainPlayModeView.Invoke(null, null) as EditorWindow;

        private static Type RequireType(Assembly assembly, string name) =>
            assembly.GetType(name) ?? throw ContractChanged(name);

        private static MethodInfo RequireMethod(Type type, string name, BindingFlags flags) =>
            type.GetMethod(name, flags) ?? throw ContractChanged($"{type.FullName}.{name}");

        private static PropertyInfo RequireProperty(Type type, string name) =>
            type.GetProperty(name, InstanceFlags) ?? throw ContractChanged($"{type.FullName}.{name}");

        private static MissingMemberException ContractChanged(string member) => new(
            $"Unity {ExpectedUnityVersion} native Game View contract changed: missing {member}.");
    }
}
