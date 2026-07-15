#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;

namespace Game.RLHarness
{
    /// <summary>-executeMethod entry point: opens the training scene and enters play mode so mlagents-learn can attach to a batch-mode editor (launch without -quit; the run ends the process externally).</summary>
    public static class TrainingBootstrap
    {
        public static void EnterTrainingPlayMode()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/RLTraining.unity");
            EditorApplication.EnterPlaymode();
        }
    }
}
#endif
