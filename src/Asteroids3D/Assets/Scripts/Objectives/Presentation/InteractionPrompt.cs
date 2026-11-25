namespace Objectives.Presentation
{
    public readonly struct InteractionPrompt
    {
        public string Key { get; }
        public string Action { get; }

        public InteractionPrompt(string key, string action)
        {
            Key = key;
            Action = action;
        }
    }
}
