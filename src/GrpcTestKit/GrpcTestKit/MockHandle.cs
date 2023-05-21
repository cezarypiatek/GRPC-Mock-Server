namespace GrpcTestKit;

internal class MockHandle : IAsyncDisposable
{
    private readonly Func<ValueTask> _remove;

    public MockHandle(Func<ValueTask> remove)
    {
        _remove = remove;
    }

    public ValueTask DisposeAsync() => _remove();
}