using UnityEditor;

namespace Game.Runs
{
    public static class UnlockAllShipsMenu
    {
        private const string MenuPath = "Astronomical/Unlock All Ships";

        [MenuItem(MenuPath)]
        private static void Toggle() =>
            EditorPrefs.SetBool(ShipUnlocks.UnlockAllPref, !EditorPrefs.GetBool(ShipUnlocks.UnlockAllPref, false));

        [MenuItem(MenuPath, true)]
        private static bool ToggleValidate()
        {
            Menu.SetChecked(MenuPath, EditorPrefs.GetBool(ShipUnlocks.UnlockAllPref, false));
            return true;
        }
    }
}
