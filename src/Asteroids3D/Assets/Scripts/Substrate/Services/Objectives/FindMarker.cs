using System;
using UnityEngine;

namespace Substrate.Services.Objectives
{
    /// <summary>An unused find on the minimap, held by the module that owns it until Close().</summary>
    public sealed class FindMarker
    {
        private readonly Action<FindMarker> close;

        public Transform Target { get; }
        public Sprite Icon { get; }

        internal FindMarker(Transform target, Sprite icon, Action<FindMarker> close)
        {
            Target = target;
            Icon = icon;
            this.close = close;
        }

        public void Close() => close(this);
    }
}
