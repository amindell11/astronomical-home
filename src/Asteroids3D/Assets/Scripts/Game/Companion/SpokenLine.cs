namespace Game.Companion
{
    public readonly struct SpokenLine
    {
        public readonly string Speaker;
        public readonly string Text;

        public SpokenLine(string speaker, string text)
        {
            Speaker = speaker;
            Text = text;
        }
    }
}
