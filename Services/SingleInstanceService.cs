namespace WinBridge.Services;

public sealed class SingleInstanceService : IDisposable
{
    private const string DefaultInstanceName = "v1";
    private readonly Mutex _mutex;
    private readonly EventWaitHandle _activationEvent;
    private readonly bool _ownsMutex;
    private RegisteredWaitHandle? _registeredWait;

    public bool IsFirstInstance => _ownsMutex;

    public SingleInstanceService(string? instanceName = null)
    {
        var name = string.IsNullOrWhiteSpace(instanceName) ? DefaultInstanceName : instanceName;
        _activationEvent = new EventWaitHandle(
            false, EventResetMode.AutoReset, $@"Local\WinBridge.Activate.{name}", out _);
        _mutex = new Mutex(true, $@"Local\WinBridge.SingleInstance.{name}", out _ownsMutex);
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
