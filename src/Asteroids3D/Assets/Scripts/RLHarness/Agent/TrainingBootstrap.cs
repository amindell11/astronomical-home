#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.RLHarness
{
    /// <summary>-executeMethod entry points for attaching mlagents-learn to a batch-mode editor (launch without -quit; the run ends the process externally). The signaled variant exists because an editor boot outlasts the trainer's 60 s handshake window: boot and arm first, start the trainer, then create the flag file to enter play.</summary>
    public static class TrainingBootstrap
    {
        public static readonly string StartFlagPath = Path.GetFullPath(Path.Combine(
            Application.dataPath, "..", "..", "..", "results", "rl-training", "start-play.flag"));

        public static void EnterTrainingPlayMode()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/RLTraining.unity");
            EditorApplication.EnterPlaymode();
        }

        public static void EnterTrainingPlayModeWhenSignaled()
        {
            Debug.Log($"[TrainingBootstrap] armed; create {StartFlagPath} to enter play mode");
            EditorApplication.update += EnterOnFlag;
        }

        private static void EnterOnFlag()
        {
            if (!File.Exists(StartFlagPath)) return;
            EditorApplication.update -= EnterOnFlag;
            File.Delete(StartFlagPath);
            EnterTrainingPlayMode();
        }
    }
}
#endif
