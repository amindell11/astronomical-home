namespace Game.Dev
{
    internal static class LaunchArgs
    {
        internal static string FirstFaction(string[] args)
        {
            for (var i = 0; i < args.Length - 1; i++)
                if (args[i] == "-firstFaction") return args[i + 1];
            return null;
        }
    }
}
