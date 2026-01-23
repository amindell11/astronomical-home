namespace AI.Scanning
{
    /// <summary>
    /// Common interface for all scanner types.
    /// Provides consistent API for scanning and caching results.
    /// </summary>
    public interface IScanner<out TResult> where TResult : struct
    {
        TResult LastResult { get; }
        TResult Scan();
    }
}
