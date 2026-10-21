using System;
using UnityEngine;

namespace UI.Screens
{
    [RequireComponent(typeof(Canvas))]
    public class RunWonScreen : MonoBehaviour
    {
        public static RunWonScreen Create(Transform parent) =>
            ScreenWidgets.CreateOverlay<RunWonScreen>(parent, "RunWonScreen");

        public void Show(int kills, int sectorsCleared, Action onContinue) =>
            ScreenWidgets.Show(transform, "EXTRACTED", kills, sectorsCleared, onContinue);
    }
}
