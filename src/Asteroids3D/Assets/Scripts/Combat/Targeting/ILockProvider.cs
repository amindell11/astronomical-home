namespace Combat.Targeting
{
    public interface ILockProvider
    {
        LockState State { get; }
        ITargetable ConsumeLock();
    }
}
