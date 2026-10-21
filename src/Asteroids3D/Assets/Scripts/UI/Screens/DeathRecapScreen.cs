using System;
using UnityEngine;

namespace UI.Screens
{
    [RequireComponent(typeof(Canvas))]
    public class DeathRecapScreen : MonoBehaviour
    {
        public static DeathRecapScreen Create(Transform parent) =>
            ScreenWidgets.CreateOverlay<DeathRecapScreen>(parent, "DeathRecapScreen");

        public void Show(int kills, int sectorsCleared, Action onContinue) =>
            ScreenWidgets.Show(transform, "SHIP DESTROYED", kills, sectorsCleared, onContinue);
    }
}
