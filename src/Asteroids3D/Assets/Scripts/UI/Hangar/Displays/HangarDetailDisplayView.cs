using System;
using UI.Hangar.Selection;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Hangar.Displays
{
    public sealed class HangarDetailDisplayView : MonoBehaviour
    {
        [SerializeField] private Text title;
        [SerializeField] private Text body;

        private HangarSelection selection;

        internal string Title => title.text;
        internal string Body => body.text;

        private void Awake()
        {
            if (!title || !body)
                throw new InvalidOperationException("HangarDetailDisplayView needs a title and a body.");
        }

        public void Bind(HangarSelection selection)
        {
            this.selection = selection;
            selection.Changed += Refresh;
            Refresh();
        }

        private void OnDestroy()
        {
            if (selection != null) selection.Changed -= Refresh;
        }

        private void Refresh()
        {
            title.text = selection.Label(HangarSlot.Ship);
            body.text = selection.Ship ? HangarDescriptions.Describe(selection.Ship) : "";
        }
    }
}
