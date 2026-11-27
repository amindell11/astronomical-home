using UnityEditor;

namespace Game.Runs
{
    public static class MeetEnemiesAgainMenu
    {
        private const string MenuPath = "Astronomical/Meet Every Enemy Again";

        [MenuItem(MenuPath)]
        private static void Toggle() =>
            EditorPrefs.SetBool(EnemiesMet.MeetAgainPref, !EditorPrefs.GetBool(EnemiesMet.MeetAgainPref, false));

        [MenuItem(MenuPath, true)]
        private static bool ToggleValidate()
        {
            Menu.SetChecked(MenuPath, EditorPrefs.GetBool(EnemiesMet.MeetAgainPref, false));
            return true;
        }
    }
}
