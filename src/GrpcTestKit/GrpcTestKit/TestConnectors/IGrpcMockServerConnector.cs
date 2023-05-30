namespace GrpcTestKit.TestConnectors;

public interface IGrpcMockServerConnector
{
    Task Install();
    IGrpcMockClient CreateClient();
}