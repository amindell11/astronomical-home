using UnityEngine;
using UnityEngine.InputSystem;

namespace AI.Playtest
{
    /// <summary>Throwaway playtest overlay: number keys 1-5 pick the enemy-AI preset, and a corner label names the active one. Skipped in batch mode so test runs never see it.</summary>
    public sealed class LethalityPresetSwitcher : MonoBehaviour
    {
        private GUIStyle style;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (Application.isBatchMode) return;
            var host = new GameObject(nameof(LethalityPresetSwitcher));
            DontDestroyOnLoad(host);
            host.AddComponent<LethalityPresetSwitcher>();
        }

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame) Set(LethalityPreset.Main);
            if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame) Set(LethalityPreset.Servo);
            if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame) Set(LethalityPreset.EarnedAccuracy);
            if (kb.digit4Key.wasPressedThisFrame || kb.numpad4Key.wasPressedThisFrame) Set(LethalityPreset.Dial025);
            if (kb.digit5Key.wasPressedThisFrame || kb.numpad5Key.wasPressedThisFrame) Set(LethalityPreset.Dial075EarnedAccuracy);
        }

        private static void Set(LethalityPreset preset)
        {
            if (LethalityPresets.Active == preset) return;
            LethalityPresets.Active = preset;
            Debug.Log($"[Playtest] Enemy AI preset {(int)preset}: {LethalityPresets.Label(preset)}");
        }

        private void OnGUI()
        {
            style ??= new GUIStyle(GUI.skin.box) { fontSize = 16, alignment = TextAnchor.MiddleLeft };
            var active = LethalityPresets.Active;
            var text = $" AI preset {(int)active}: {LethalityPresets.Label(active)}   (keys 1-5)";
            GUI.Box(new Rect(10f, Screen.height - 40f, 520f, 30f), text, style);
        }
    }
}
