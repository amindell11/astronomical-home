using System;
using UI.Hangar.Selection;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Hangar.Displays
{
    public sealed class HangarMainDisplayView : MonoBehaviour
    {
        private static readonly Color Idle = new(0.16f, 0.17f, 0.19f, 1f);
        private static readonly Color Chosen = new(0.95f, 0.78f, 0.42f, 1f);

        [SerializeField] private Button[] buttons;
        [SerializeField] private Text[] labels;

        private Image[] faces;
        private HangarSelection selection;

        internal Button this[HangarSlot slot] => buttons[(int)slot];

        private void Awake()
        {
            if (buttons == null || labels == null || buttons.Length != 3 || labels.Length != 3)
                throw new InvalidOperationException("HangarMainDisplayView needs a button and a label for each slot.");
            faces = new Image[3];
            for (var i = 0; i < 3; i++)
            {
                if (!buttons[i] || !labels[i])
                    throw new InvalidOperationException("HangarMainDisplayView needs a button and a label for each slot.");
                faces[i] = buttons[i].GetComponent<Image>();
                if (!faces[i])
                    throw new InvalidOperationException("HangarMainDisplayView's buttons need an image.");
            }
        }

        public void Bind(HangarSelection selection)
        {
            this.selection = selection;
            for (var i = 0; i < 3; i++)
            {
                var slot = (HangarSlot)i;
                buttons[i].onClick.AddListener(() => selection.Select(slot));
            }
            selection.Changed += Refresh;
            Refresh();
        }

        private void OnDestroy()
        {
            if (selection != null) selection.Changed -= Refresh;
        }

        private void Refresh()
        {
            for (var i = 0; i < 3; i++)
            {
                var slot = (HangarSlot)i;
                labels[i].text = $"{slot}\n{selection.Label(slot)}";
                faces[i].color = slot == selection.Selected ? Chosen : Idle;
            }
        }
    }
}
