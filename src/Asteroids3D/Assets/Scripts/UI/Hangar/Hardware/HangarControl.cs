using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace UI.Hangar.Hardware
{
    public sealed class HangarControl : MonoBehaviour, IPointerClickHandler
    {
        public event Action Pressed;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            Pressed?.Invoke();
        }
    }
}
