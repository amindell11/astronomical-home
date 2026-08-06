using UnityEditor;

namespace Game.Diagnostics
{
    /// <summary>Checkable menu over <see cref="DiagnosticGate"/>: atoms toggle individually; a preset click replaces the active set (a preset is a modal lens).</summary>
    internal static class DiagnosticsMenu
    {
        private const string EverythingItem = "Diagnostics/Presets/Everything";
        private const string ScoutScanItem = "Diagnostics/Painters/scout-scan";
        private const string LockOnItem = "Diagnostics/Painters/lock-on";
        private const string PolicyItem = "Diagnostics/Painters/policy";
        private const string DrawUnselectedItem = "Diagnostics/Draw Unselected Ships";

        [MenuItem(EverythingItem)]
        private static void SelectEverything()
        {
            DiagnosticPainters.TryExpandPreset(DiagnosticPainters.Everything, out var atoms);
            DiagnosticGate.Replace(atoms);
            SyncChecks();
        }

        [MenuItem(ScoutScanItem)]
        private static void ToggleScoutScan() => Toggle(DiagnosticPainters.ScoutScan);

        [MenuItem(LockOnItem)]
        private static void ToggleLockOn() => Toggle(DiagnosticPainters.LockOn);

        [MenuItem(PolicyItem)]
        private static void TogglePolicy() => Toggle(DiagnosticPainters.Policy);

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
            Menu.SetChecked(EverythingItem, IsEverythingActive());
            Menu.SetChecked(ScoutScanItem, DiagnosticGate.IsActive(DiagnosticPainters.ScoutScan));
            Menu.SetChecked(LockOnItem, DiagnosticGate.IsActive(DiagnosticPainters.LockOn));
            Menu.SetChecked(PolicyItem, DiagnosticGate.IsActive(DiagnosticPainters.Policy));
            Menu.SetChecked(DrawUnselectedItem, DiagnosticGate.DrawUnselected);
        }

        private static bool IsEverythingActive()
        {
            DiagnosticPainters.TryExpandPreset(DiagnosticPainters.Everything, out var atoms);
            if (DiagnosticGate.ActiveCount != atoms.Length) return false;
            foreach (var atom in atoms)
                if (!DiagnosticGate.IsActive(atom))
                    return false;
            return true;
        }
    }
}
