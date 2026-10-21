using System;
using Game.Player;
using Game.Runs;
using UnityEngine;

namespace UI.Screens
{
    [RequireComponent(typeof(Canvas))]
    public class DeathRecapScreen : MonoBehaviour
    {
        public static DeathRecapScreen Create(Transform parent) =>
            ScreenWidgets.CreateOverlay<DeathRecapScreen>(parent, "DeathRecapScreen");

        public void Show(DamageLedger ledger, IRunTally tally, int sectorsCleared, Scoreboard board, Action onContinue) =>
            ScreenWidgets.Show(transform, "SHIP DESTROYED", tally, sectorsCleared, ledger, board, onContinue);
    }
}
