using RestEase;
using SmoothSailing;
using WireMock.Client;

namespace GrpcTestKit.TestConnectors.Kubernetes;

public class TestChartGrpcMockServerConnector : IGrpcMockServerConnector
{
    private readonly string _protoDirectory;
    private readonly int _grpcPort;
    private readonly string _dockerImage;
    private readonly bool _exposeGrpcPortOnLocalhost;
    private readonly string _releaseName;
    private readonly ChartInstaller _chartInstaller;
    private readonly ChartFromLocalPath _chart;
    private Release? _release;
    private int _wireMockPort;

    public TestChartGrpcMockServerConnector(string protoDirectory, int grpcPort = 5033, string dockerImage = "cezarypiatek/grpc-mock-server", string? releaseName = null, bool exposeGrpcPortOnLocalhost = false)
    {
        _protoDirectory = Path.GetFullPath(protoDirectory);
        _grpcPort = grpcPort;
        _dockerImage = dockerImage;
        _exposeGrpcPortOnLocalhost = exposeGrpcPortOnLocalhost;
        _releaseName = releaseName ?? "grpcmockserverconnector";
        _chartInstaller = new ChartInstaller(new ProcessLauncher(new ConsoleProcessOutputWriter()));
        _chart = new ChartFromLocalPath("./charts/grpcmockserver");
    }

    public async Task<GrpcMockServerConnectionInfo> Install()
    {
        var allProtoFile = Directory.EnumerateFiles(_protoDirectory, "*.proto", SearchOption.AllDirectories);

        var overrides = new
        {
            dockerImage = _dockerImage,
            grpcPort = _grpcPort,
            protoFiles = allProtoFile.Select(x => new
            {
                key = Guid.NewGuid().ToString("N"),
                path = x.Remove(0, _protoDirectory.Length).Replace("\\", "/").Trim('/'),
                content = File.ReadAllText(x)
            }).ToArray()
        };

        _release = await _chartInstaller.Install(_chart, _releaseName, overrides);
        if (_exposeGrpcPortOnLocalhost)
        {
            _ = await _release.StartPortForwardForService(serviceName: $"{_releaseName}-grpcmockserver-service", servicePort: _grpcPort, localPort: _grpcPort);
        }
        _wireMockPort = await _release.StartPortForwardForService(serviceName: $"{_releaseName}-grpcmockserver-service", servicePort: 9095);
        return new GrpcMockServerConnectionInfo
        (
            grpcEndpoint: _exposeGrpcPortOnLocalhost ? $"http://127.0.0.1:{_grpcPort}":  $"http://{_releaseName}-grpcmockserver-service:{_grpcPort}" ,
            stubbingEndpoint: $"http://127.0.0.1:{ _wireMockPort}"
        );
    }

    public IGrpcMockClient CreateClient()
    {
        if (_release == null)
        {
            throw new InvalidOperationException("Cannot create client as the connector is not installed yet. Call Install() first.");
        }

        var adminUrl = $"http://127.0.0.1:{_wireMockPort}";
        return new GrpcMockClient(RestClient.For<IWireMockAdminApi>(adminUrl), adminUrl);
    }

    public async ValueTask DisposeAsync()
    {
        if (_release != null)
        {
            await _release.DisposeAsync();
        }
    }
}