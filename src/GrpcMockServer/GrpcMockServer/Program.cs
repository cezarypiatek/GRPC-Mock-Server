

await using var mockServer = new StandaloneGrpcMockServer();
await mockServer.StartAsync(grpcPort: 5033, wireMockPort: 9096);

[GrpcMockServerForAutoDiscoveredSourceServices]
public partial class StandaloneGrpcMockServer
{

}
