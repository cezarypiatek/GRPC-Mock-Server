using RestEase;
using SmoothSailing;
using WireMock.Client;

namespace GrpcTestKit.TestConnectors.Kubernetes;

public class TestChartGrpcMockServerConnector : IGrpcMockServerConnector
{
    private readonly TestChartGrpcMockServerConnectorSettings _settings;
    private readonly string _protoDirectory;
    private readonly ChartInstaller _chartInstaller;
    private readonly ChartFromLocalPath _chart;
    private Release? _release;
    private GrpcMockServerConnectionInfo? _connectionInfo;

    public TestChartGrpcMockServerConnector(TestChartGrpcMockServerConnectorSettings settings)
    {
        _settings = settings;
        _protoDirectory = Path.GetFullPath(settings.ProtoDirectory);
        _chartInstaller = new ChartInstaller();
        _chart = new ChartFromLocalPath("./charts/grpcmockserver");
    }

    public async Task<GrpcMockServerConnectionInfo> Install()
    {
        var allProtoFile = Directory.EnumerateFiles(_protoDirectory, "*.proto", SearchOption.AllDirectories);

        var overrides = new
        {
            dockerImage = _settings.DockerImage,
            grpcPort = _settings.GrpcPort,
            stubbingPort = _settings.StubbingPort,
            protoFiles = allProtoFile.Select(x => new
            {
                key = Guid.NewGuid().ToString("N"),
                path = x.Remove(0, _protoDirectory.Length).Replace("\\", "/").Trim('/'),
                content = File.ReadAllText(x)
            }).ToArray()
        };

        _release = await _chartInstaller.Install(_chart, _settings.ReleaseName, overrides, context: _settings.Context);

        var grpcPort = _settings.ExposeGrpcPortOnLocalhost
            ? await _release.StartPortForwardForService(serviceName: $"{_settings.ReleaseName}-grpcmockserver-service",
                servicePort: _settings.GrpcPort, localPort: _settings.ExposeGrpcPortOnLocalhostPort)
            : _settings.GrpcPort;

        var stubbingPort = _settings.ExposeStubbingPortOnLocalhost
            ? await _release.StartPortForwardForService(serviceName: $"{_settings.ReleaseName}-grpcmockserver-service",
                servicePort: _settings.StubbingPort, localPort: _settings.ExposeStubbingPortOnLocalhostPort)
            : _settings.StubbingPort;

        var serviceName = $"{_settings.ReleaseName}-grpcmockserver-service";
        var serviceAddress = _settings.Context?.ResolveServiceAddress(serviceName) ?? serviceName;
        return this._connectionInfo = new GrpcMockServerConnectionInfo
        (
            grpcEndpoint: _settings.ExposeGrpcPortOnLocalhost ? $"http://127.0.0.1:{grpcPort}":  $"http://{serviceAddress}:{grpcPort}" ,
            stubbingEndpoint: _settings.ExposeStubbingPortOnLocalhost?  $"http://127.0.0.1:{stubbingPort}": $"http://{serviceAddress}:{stubbingPort}"
        );
    }

    public IGrpcMockClient CreateClient()
    {
        if (_connectionInfo == null)
        {
            throw new InvalidOperationException("Cannot create client as the connector is not installed yet. Call Install() first.");
        }

        return new GrpcMockClient(RestClient.For<IWireMockAdminApi>(_connectionInfo.StubbingEndpoint), _connectionInfo.StubbingEndpoint);
    }

    public async ValueTask DisposeAsync()
    {
        if (_release != null)
        {
            await _release.DisposeAsync();
        }
    }
}