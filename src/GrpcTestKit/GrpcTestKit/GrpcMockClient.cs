using WireMock.Admin.Mappings;
using WireMock.Client;

namespace GrpcTestKit;

public class GrpcMockClient : IGrpcMockClient
{
    private readonly IWireMockAdminApi _wireMockAdminApi;

    public GrpcMockClient(IWireMockAdminApi wireMockAdminApi)
    {
        _wireMockAdminApi = wireMockAdminApi;
    }

    public async Task<IAsyncDisposable> MockEndpoint(Action<MappingModelBuilder> configureBuilder)
    {
        var builder = new MappingModelBuilder();
        configureBuilder(builder);
        var mapping = builder.Build();
        var mappingId = mapping.Guid ??= Guid.NewGuid();
        await _wireMockAdminApi.PostMappingAsync(mapping);
        return new MockHandle(async () => await _wireMockAdminApi.DeleteMappingAsync(mappingId));
    }

    public ValueTask DisposeAsync() => default;
}