using UnityEditor;

namespace Game.Diagnostics
{
    /// <summary>Checkable menu over <see cref="DiagnosticGate"/>: the unselected-ship atom toggles, and Clear All empties the gate.</summary>
    internal static class DiagnosticsMenu
    {
        private const string DrawUnselectedItem = "Diagnostics/Draw Unselected Ships";

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

        // Menu items only exist after the first menu build; delayCall dodges the InitializeOnLoad ordering.
        [InitializeOnLoadMethod]
        private static void SyncChecksOnLoad() => EditorApplication.delayCall += SyncChecks;

        private static void SyncChecks() => Menu.SetChecked(DrawUnselectedItem, DiagnosticGate.DrawUnselected);
    }
}
