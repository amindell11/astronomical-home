using Objectives;
using UnityEngine;

namespace Game.Services
{
    /// <summary>The spine owner's mutation surface (mirrors LocalObjectiveHandle); every mutation through a superseded handle is a no-op.</summary>
    public sealed class SpineObjectiveHandle
    {
        private readonly ObjectiveService service;

        public ObjectiveTracker Tracker { get; }

        internal SpineObjectiveHandle(ObjectiveService service, ObjectiveTracker tracker)
        {
            this.service = service;
            Tracker = tracker;
        }

        public Transform Target
        {
            get => service.IsCurrent(this) ? service.SpineTarget : null;
            set => service.SetSpineTarget(this, value);
        }

        public void Fail()
        {
            if (service.IsCurrent(this)) Tracker.Fail();
        }

        public void Restart()
        {
            if (service.IsCurrent(this)) Tracker.Restart();
        }

        public void Close() => service.CloseSpine(this);
    }
}
