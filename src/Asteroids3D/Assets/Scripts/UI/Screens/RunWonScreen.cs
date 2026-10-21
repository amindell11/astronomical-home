using System;
using Game.Player;
using UnityEngine;
using static UI.Screens.ScreenWidgets;

namespace UI.Screens
{
    [RequireComponent(typeof(Canvas))]
    public class RunWonScreen : MonoBehaviour
    {
        public static RunWonScreen Create(Transform parent) => CreateOverlay<RunWonScreen>(parent, "RunWonScreen");

        public void Show(IRunTally tally, int sectorsCleared, Action onContinue)
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var panel = AddPanel(transform);

            AddText(panel, "Title", "EXTRACTED", font, 34, new Color(0.45f, 0.95f, 0.55f), FontStyle.Bold);
            AddText(panel, "Tally", $"{TallyBlock(tally)}\nSectors cleared: {sectorsCleared}", font, 22, Color.white);
            AddContinueButton(panel, font, onContinue);
        }
    }
}
