namespace Tempo.Blazor.Demo.Services;

/// <summary>
/// Minimal IObservable/IObserver bridge — no Rx dependency.
/// Thread-safe via lock; observer errors are swallowed individually so one bad
/// observer cannot break others.
/// </summary>
internal sealed class SimpleSubject<T> : IObservable<T>
{
    private readonly List<IObserver<T>> _observers = [];
    private readonly object _sync = new();

    public IDisposable Subscribe(IObserver<T> observer)
    {
        lock (_sync) _observers.Add(observer);
        return new Unsubscriber(this, observer);
    }

    public void OnNext(T value)
    {
        IObserver<T>[] snapshot;
        lock (_sync) snapshot = [.. _observers];
        foreach (var o in snapshot)
        {
            try { o.OnNext(value); }
            catch { /* individual observer failure must not affect others */ }
        }
    }

    public void OnCompleted()
    {
        IObserver<T>[] snapshot;
        lock (_sync) snapshot = [.. _observers];
        foreach (var o in snapshot)
        {
            try { o.OnCompleted(); } catch { }
        }
    }

    private void Remove(IObserver<T> observer)
    {
        lock (_sync) _observers.Remove(observer);
    }

    private sealed class Unsubscriber(SimpleSubject<T> subject, IObserver<T> observer) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            subject.Remove(observer);
        }
    }
}
