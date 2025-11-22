namespace Asc.Utils.Needle;

/// <summary>
/// Provides an asynchronous manual-reset event that allows tasks to wait for a signal to be set or reset. This class
/// enables coordination between asynchronous operations by allowing one or more tasks to wait until the event is
/// signaled.
/// </summary>
/// <remarks>
/// Unlike AutoResetEvent, a manual-reset event remains signaled until it is explicitly reset, allowing
/// multiple waiting tasks to proceed. This class is designed for use in asynchronous programming scenarios where
/// traditional synchronization primitives are not suitable. All members are thread-safe.
/// </remarks>
public interface IAsyncManualResetEvent
{
    /// <summary>
    /// Resets the object to its initial state.
    /// </summary>
    void Reset();

    /// <summary>
    /// Applies the current configuration or state changes.
    /// </summary>
    void Set();

    /// <summary>
    /// Asynchronously waits until the current operation can proceed or the wait is canceled.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the wait operation before it completes.</param>
    /// <returns>
    /// A task that represents the asynchronous wait operation. The task completes when the wait is satisfied or the
    /// operation is canceled.
    /// </returns>
    Task WaitAsync(CancellationToken cancellationToken = default);
}