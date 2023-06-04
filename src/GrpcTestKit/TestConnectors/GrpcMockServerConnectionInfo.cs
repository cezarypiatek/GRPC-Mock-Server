namespace GrpcTestKit.TestConnectors;

public class GrpcMockServerConnectionInfo
{
    public string GrpcEndpoint { get; }
    public string StubbingEndpoint { get; }

    public GrpcMockServerConnectionInfo(string grpcEndpoint, string stubbingEndpoint)
    {
        GrpcEndpoint = grpcEndpoint;
        StubbingEndpoint = stubbingEndpoint;
    }
}