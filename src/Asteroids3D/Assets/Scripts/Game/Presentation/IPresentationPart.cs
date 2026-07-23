namespace Game.Presentation
{
    /// <summary>
    /// A behaviour on a spawned/pooled world object that runs presentation logic (visual fades,
    /// audio state machines, effect spawns). The owning spawn seam applies session presentation
    /// policy through <see cref="PresentationApplier"/>; parts toggle their own behavior — raw
    /// renderers/particles/audio sources are disabled generically by the applier.
    /// </summary>
    public interface IPresentationPart
    {
        void ApplyPresentation(bool visible);
    }
}
