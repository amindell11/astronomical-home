using System;

namespace Combat.Targeting
{
    public interface ILockStateSource
    {
        LockState State { get; }
        event Action<LockState, LockState> OnStateChanged;
    }
}
