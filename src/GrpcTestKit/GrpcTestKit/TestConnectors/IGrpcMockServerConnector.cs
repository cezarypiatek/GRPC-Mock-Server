namespace GrpcTestKit.TestConnectors;

public interface IGrpcMockServerConnector: System.IAsyncDisposable
{
    Task Install();
    IGrpcMockClient CreateClient();
}