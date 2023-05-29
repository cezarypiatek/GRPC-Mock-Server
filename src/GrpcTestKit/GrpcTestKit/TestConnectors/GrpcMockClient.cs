using WireMock.Admin.Mappings;
using WireMock.Client;
using WireMock.Net.Extensions.WireMockInspector;

namespace GrpcTestKit.TestConnectors;

public class GrpcMockClient : IGrpcMockClient
{
    private readonly IWireMockAdminApi _wireMockAdminApi;
    private readonly string _adminUrl;

    public GrpcMockClient(IWireMockAdminApi wireMockAdminApi, string adminUrl)
    {
        _wireMockAdminApi = wireMockAdminApi;
        _adminUrl = adminUrl;
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


    public void Inspect() => WireMockServerExtensions.Inspect(_adminUrl);

    public ValueTask DisposeAsync() => default;
}