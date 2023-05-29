using WireMock.Admin.Mappings;

namespace GrpcTestKit.TestConnectors;

public interface IGrpcMockClient : IAsyncDisposable
{
    Task<IAsyncDisposable> MockEndpoint(Action<MappingModelBuilder> configureBuilder);
    void Inspect();
}