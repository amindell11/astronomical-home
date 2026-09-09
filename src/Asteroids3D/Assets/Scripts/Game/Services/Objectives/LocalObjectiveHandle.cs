using System;
using Objectives;
using UnityEngine;

namespace Game.Services.Objectives
{
    /// <summary>An encounter-owned concurrent objective; the service ticks its tracker until Close().</summary>
    public sealed class LocalObjectiveHandle
    {
        private readonly Action<LocalObjectiveHandle> close;

        public ObjectiveTracker Tracker { get; }
        public Transform Target { get; set; }

        internal LocalObjectiveHandle(
            ObjectiveTracker tracker, Transform target, Action<LocalObjectiveHandle> close)
        {
            Tracker = tracker;
            Target = target;
            this.close = close;
        }

        public void Close() => close(this);
    }
}
