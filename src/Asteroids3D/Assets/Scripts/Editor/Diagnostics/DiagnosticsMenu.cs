using AI;
using UnityEditor;

namespace Game.Diagnostics
{
    /// <summary>Checkable menu over <see cref="DiagnosticGate"/>: atoms toggle individually; a preset click replaces the active set (a preset is a modal lens).</summary>
    internal static class DiagnosticsMenu
    {
        private const string EverythingItem = "Diagnostics/Presets/Everything";
        private const string SteeringItem = "Diagnostics/Presets/Steering";
        private const string ScoutScanItem = "Diagnostics/Painters/scout-scan";
        private const string LockOnItem = "Diagnostics/Painters/lock-on";
        private const string PolicyItem = "Diagnostics/Painters/policy";
        private const string MpcTrajectoriesItem = "Diagnostics/Painters/mpc-trajectories";
        private const string MpcObstaclesItem = "Diagnostics/Painters/mpc-obstacles";
        private const string MpcControlsItem = "Diagnostics/Painters/mpc-controls";
        private const string GunnerTargetingItem = "Diagnostics/Painters/gunner-targeting";
        private const string ObservationItem = "Diagnostics/Painters/observation";
        private const string DrawUnselectedItem = "Diagnostics/Draw Unselected Ships";

        [MenuItem(EverythingItem)]
        private static void SelectEverything() => SelectPreset(DiagnosticPainters.Everything);

        [MenuItem(SteeringItem)]
        private static void SelectSteering() => SelectPreset(DiagnosticPainters.Steering);

        [MenuItem(ScoutScanItem)]
        private static void ToggleScoutScan() => Toggle(DiagnosticPainters.ScoutScan);

        [MenuItem(LockOnItem)]
        private static void ToggleLockOn() => Toggle(DiagnosticPainters.LockOn);

        [MenuItem(PolicyItem)]
        private static void TogglePolicy() => Toggle(DiagnosticPainters.Policy);

        [MenuItem(MpcTrajectoriesItem)]
        private static void ToggleMpcTrajectories() => Toggle(DiagnosticPainters.MpcTrajectories);

        [MenuItem(MpcObstaclesItem)]
        private static void ToggleMpcObstacles() => Toggle(DiagnosticPainters.MpcObstacles);

        // Editor-only atom: the control-bar panel is billboard UI with no painter behind it.
        [MenuItem(MpcControlsItem)]
        private static void ToggleMpcControls() => Toggle(NavigatorGizmos.MpcControls);

        [MenuItem(GunnerTargetingItem)]
        private static void ToggleGunnerTargeting() => Toggle(DiagnosticPainters.GunnerTargeting);

        [MenuItem(ObservationItem)]
        private static void ToggleObservation() => Toggle(DiagnosticPainters.Observation);

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

        private static void SelectPreset(string name)
        {
            DiagnosticPainters.TryExpandPreset(name, out var atoms);
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
            Menu.SetChecked(SteeringItem, IsPresetActive(DiagnosticPainters.Steering));
            Menu.SetChecked(ScoutScanItem, DiagnosticGate.IsActive(DiagnosticPainters.ScoutScan));
            Menu.SetChecked(LockOnItem, DiagnosticGate.IsActive(DiagnosticPainters.LockOn));
            Menu.SetChecked(PolicyItem, DiagnosticGate.IsActive(DiagnosticPainters.Policy));
            Menu.SetChecked(MpcTrajectoriesItem, DiagnosticGate.IsActive(DiagnosticPainters.MpcTrajectories));
            Menu.SetChecked(MpcObstaclesItem, DiagnosticGate.IsActive(DiagnosticPainters.MpcObstacles));
            Menu.SetChecked(MpcControlsItem, DiagnosticGate.IsActive(NavigatorGizmos.MpcControls));
            Menu.SetChecked(GunnerTargetingItem, DiagnosticGate.IsActive(DiagnosticPainters.GunnerTargeting));
            Menu.SetChecked(ObservationItem, DiagnosticGate.IsActive(DiagnosticPainters.Observation));
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
