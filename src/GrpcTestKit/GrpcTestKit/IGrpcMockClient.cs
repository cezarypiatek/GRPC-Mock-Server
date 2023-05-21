using WireMock.Admin.Mappings;

namespace GrpcTestKit;

public interface IGrpcMockClient:IAsyncDisposable
{
    Task<IAsyncDisposable> MockEndpoint(Action<MappingModelBuilder> configureBuilder);
}