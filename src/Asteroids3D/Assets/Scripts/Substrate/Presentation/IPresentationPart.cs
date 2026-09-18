namespace Substrate.Presentation
{
    /// <summary>
    /// A behaviour on a spawned/pooled world object that runs presentation logic (visual fades,
    /// audio state machines, effect spawns). The owning spawn seam applies session presentation
    /// policy through <see cref="PresentationApplier"/>: raw renderers/particles/audio sources are
    /// switched generically first, then parts toggle their own behavior and settle the hardware they
    /// own.
    /// </summary>
    public interface IPresentationPart
    {
        void ApplyPresentation(bool visible);
    }
}
