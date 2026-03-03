namespace Game.Session
{
    public interface ISessionLegacyBridge
    {
        void Bind(SessionContext context);
        void Clear(SessionContext context);
    }

    public sealed class NullSessionLegacyBridge : ISessionLegacyBridge
    {
        public static readonly NullSessionLegacyBridge Instance = new();

        private NullSessionLegacyBridge() { }

        public void Bind(SessionContext context) { }
        public void Clear(SessionContext context) { }
    }
}
