namespace GrpcTestKit;

public interface ITestComponentConnector<out TClient>: IAsyncDisposable
{
    Task Install();
    TClient CreateClient();
}