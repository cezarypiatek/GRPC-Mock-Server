namespace GrpcTestKit.TestConnectors;

public interface IGrpcMockServerConnector: IAsyncDisposable
{
    Task<GrpcMockServerConnectionInfo> Install();
    IGrpcMockClient CreateClient();
}