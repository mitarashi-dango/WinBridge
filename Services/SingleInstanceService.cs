namespace WinBridge.Services;

public sealed class SingleInstanceService : IDisposable
{
    private const string MutexName = @"Local\WinBridge.SingleInstance.v1";
    private const string ActivationEventName = @"Local\WinBridge.Activate.v1";
    private readonly Mutex _mutex;
    private readonly EventWaitHandle _activationEvent;
    private readonly bool _ownsMutex;
    private RegisteredWaitHandle? _registeredWait;

    public bool IsFirstInstance => _ownsMutex;

    public SingleInstanceService()
    {
        _activationEvent = new EventWaitHandle(
            false, EventResetMode.AutoReset, ActivationEventName, out _);
        _mutex = new Mutex(true, MutexName, out _ownsMutex);
    }

    public void SignalExistingInstance()
    {
        if (!_ownsMutex)
            _activationEvent.Set();
    }

    public void ListenForActivation(Action activate)
    {
        if (!_ownsMutex || _registeredWait is not null) return;
        _registeredWait = ThreadPool.RegisterWaitForSingleObject(
            _activationEvent,
            (_, timedOut) =>
            {
                if (!timedOut) activate();
            },
            null,
            Timeout.Infinite,
            false);
    }

    public void Dispose()
    {
        _registeredWait?.Unregister(null);
        _activationEvent.Dispose();
        if (_ownsMutex)
        {
            try { _mutex.ReleaseMutex(); }
            catch (ApplicationException) { }
        }
        _mutex.Dispose();
    }
}
