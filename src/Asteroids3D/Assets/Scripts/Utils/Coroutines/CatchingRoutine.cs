using System;
using System.Collections;
using System.Collections.Generic;

namespace Utils.Coroutines
{
    /// <summary>
    /// Steps a coroutine and every enumerator it yields as one stack, so a throw at any depth reaches
    /// its owner. Unity only logs a nested coroutine's exception and never resumes or disposes the
    /// parent; here every suspended frame is disposed, running its finally blocks, before the fault is
    /// reported. Other yields (frames, waits, async operations, coroutines) pass through to Unity.
    /// </summary>
    public static class CatchingRoutine
    {
        public static IEnumerator Run(IEnumerator routine, Action<Exception> faulted)
        {
            var frames = new Stack<IEnumerator>();
            frames.Push(routine);
            while (frames.Count > 0)
            {
                var frame = frames.Peek();
                bool advanced;
                try
                {
                    advanced = frame.MoveNext();
                }
                catch (Exception exception)
                {
                    foreach (var suspended in frames) (suspended as IDisposable)?.Dispose();
                    faulted(exception);
                    yield break;
                }
                if (!advanced) frames.Pop();
                else if (frame.Current is IEnumerator nested) frames.Push(nested);
                else yield return frame.Current;
            }
        }
    }
}
