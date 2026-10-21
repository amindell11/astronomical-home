using System;
using Game.Player;
using Game.Runs;
using UnityEngine;

namespace UI.Screens
{
    [RequireComponent(typeof(Canvas))]
    public class RunWonScreen : MonoBehaviour
    {
        public static RunWonScreen Create(Transform parent) =>
            ScreenWidgets.CreateOverlay<RunWonScreen>(parent, "RunWonScreen");

        public void Show(IRunTally tally, int sectorsCleared, DamageLedger ledger, Scoreboard board, Action onContinue) =>
            ScreenWidgets.Show(transform, "EXTRACTED", tally, sectorsCleared, ledger, board, onContinue);
    }
}
