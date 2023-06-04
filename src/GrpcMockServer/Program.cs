

await using var mockServer = new StandaloneGrpcMockServer(grpcPort: 5033, wireMockPort: 9096);
_ = mockServer.Install();
await mockServer.Wait();

[GrpcMockServerForAutoDiscoveredSourceServices]
public partial class StandaloneGrpcMockServer
{

}
