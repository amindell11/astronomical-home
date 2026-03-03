namespace Game.Session
{
    internal interface ISessionLegacyBridge
    {
        void Bind(SessionContext context);
        void Clear(SessionContext context);
    }

    public sealed class NullSessionLegacyBridge : ISessionLegacyBridge
    {
        public NullSessionLegacyBridge() { }

        void ISessionLegacyBridge.Bind(SessionContext context) { }
        void ISessionLegacyBridge.Clear(SessionContext context) { }
    }
}
