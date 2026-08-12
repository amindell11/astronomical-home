using AI;
using Cameras;
using UnityEditor;

namespace Game.Diagnostics
{
    /// <summary>Checkable menu over <see cref="DiagnosticGate"/>: atoms toggle individually; a preset click replaces the active set (a preset is a modal lens).</summary>
    internal static class DiagnosticsMenu
    {
        private const string EverythingItem = "Diagnostics/Presets/Everything";
        private const string CombatItem = "Diagnostics/Presets/Combat";
        private const string DrawUnselectedItem = "Diagnostics/Draw Unselected Ships";

        [MenuItem(EverythingItem)]
        private static void SelectEverything() => SelectPreset(DiagnosticPainters.Everything);

        [MenuItem(CombatItem)]
        private static void SelectCombat() => SelectPreset(DiagnosticPainters.Combat);

        // Editor-only atom: the control-bar panel is billboard UI with no painter behind it.
        [MenuItem(DrawUnselectedItem)]
        private static void ToggleDrawUnselected()
        {
            DiagnosticGate.DrawUnselected = !DiagnosticGate.DrawUnselected;
            SyncChecks();
        }

        [MenuItem("Diagnostics/Clear All")]
        private static void ClearAll()
        {
            DiagnosticGate.Clear();
            SyncChecks();
        }

        private static void SelectPreset(string preset)
        {
            DiagnosticPainters.TryExpandPreset(preset, out var atoms);
            DiagnosticGate.Replace(atoms);
            SyncChecks();
        }

        private static void Toggle(string name)
        {
            DiagnosticGate.Toggle(name);
            SyncChecks();
        }

        // Menu items only exist after the first menu build; delayCall dodges the InitializeOnLoad ordering.
        [InitializeOnLoadMethod]
        private static void SyncChecksOnLoad() => EditorApplication.delayCall += SyncChecks;

        private static void SyncChecks()
        {
            Menu.SetChecked(EverythingItem, IsPresetActive(DiagnosticPainters.Everything));
            Menu.SetChecked(CombatItem, IsPresetActive(DiagnosticPainters.Combat));
            Menu.SetChecked(DrawUnselectedItem, DiagnosticGate.DrawUnselected);
        }

        private static bool IsPresetActive(string preset)
        {
            DiagnosticPainters.TryExpandPreset(preset, out var atoms);
            if (DiagnosticGate.ActiveCount != atoms.Length) return false;
            foreach (var atom in atoms)
                if (!DiagnosticGate.IsActive(atom))
                    return false;
            return true;
        }
    }
}
