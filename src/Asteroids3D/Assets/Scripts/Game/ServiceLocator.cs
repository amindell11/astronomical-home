namespace Game
{
    public static class ServiceLocator
    {
        private static readonly System.Collections.Generic.Dictionary<System.Type, object> Services = new();

        public static void Register<T>(T service)
        {
            var type = typeof(T);
            if (Services.ContainsKey(type))
            {
                Services[type] = service;
            }
            else
            {
                Services.Add(type, service);
            }
        }

        public static T Get<T>()
        {
            var type = typeof(T);
            if (!Services.TryGetValue(type, out var service))
            {
                throw new System.Exception($"Service of type {type} not found.");
            }
            return (T)service;
        }

        public static void Unregister<T>()
        {
            var type = typeof(T);
            Services.Remove(type);
        }
    }
}
