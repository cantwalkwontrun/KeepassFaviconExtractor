using System;
using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Threading;

namespace FaviconExtractor
{
    internal static class StaRenderOperation
    {
        // An abandoned renderer retains its slot until it actually exits.
        private static readonly SemaphoreSlim RenderSlot = new SemaphoreSlim(1, 1);

        private sealed class ResultState<T>
        {
            internal T Result;
            internal Exception Error;
            internal bool Abandoned;
        }

        internal static T Run<T>(Func<T> action, CancellationToken cancellationToken, TimeSpan timeout)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            if (timeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(timeout));
            var watch = Stopwatch.StartNew();
            if (!RenderSlot.Wait(timeout, cancellationToken))
                throw new TimeoutException("SVG rendering is still busy with an earlier conversion.");
            if (watch.Elapsed >= timeout)
            {
                RenderSlot.Release();
                throw new TimeoutException("SVG conversion timed out.");
            }
            var state = new ResultState<T>();
            Thread thread;
            try
            {
                thread = new Thread(delegate()
                {
                    try
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        T result = action();
                        bool abandoned;
                        lock (state)
                        {
                            abandoned = state.Abandoned;
                            if (!abandoned) state.Result = result;
                        }
                        if (abandoned) DisposeResult(result);
                    }
                    catch (Exception error) { lock (state) state.Error = error; }
                    finally { RenderSlot.Release(); }
                });
                thread.IsBackground = true;
                thread.SetApartmentState(ApartmentState.STA);
                thread.Start();
            }
            catch { RenderSlot.Release(); throw; }

            try
            {
                while (!thread.Join(25))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (watch.Elapsed >= timeout) throw new TimeoutException("SVG conversion timed out.");
                }
                cancellationToken.ThrowIfCancellationRequested();
                if (watch.Elapsed >= timeout) throw new TimeoutException("SVG conversion timed out.");
                lock (state)
                {
                    if (state.Error != null) ExceptionDispatchInfo.Capture(state.Error).Throw();
                    T result = state.Result;
                    state.Result = default(T);
                    return result;
                }
            }
            catch
            {
                T unclaimed;
                lock (state)
                {
                    state.Abandoned = true;
                    unclaimed = state.Result;
                    state.Result = default(T);
                }
                DisposeResult(unclaimed);
                throw;
            }
        }

        private static void DisposeResult<T>(T result)
        {
            var disposable = result as IDisposable;
            if (disposable != null) disposable.Dispose();
        }
    }
}
