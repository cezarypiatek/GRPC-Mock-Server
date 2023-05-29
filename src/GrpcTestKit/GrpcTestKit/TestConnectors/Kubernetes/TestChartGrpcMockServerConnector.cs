using RestEase;
using WireMock.Client;

namespace GrpcTestKit.TestConnectors.Kubernetes;

public class TestChartGrpcMockServerConnector : IGrpcMockServerConnector
{
    private readonly string _protoDirectory;
    private readonly int _grpcPort;
    private readonly string _dockerImage;
    private readonly string _releaseName;
    private readonly ChartInstaller _chartInstaller;
    private readonly ChartFromLocalPath _chart;
    private Release? _release;
    private int _wireMockPort;

    public TestChartGrpcMockServerConnector(string protoDirectory, int grpcPort = 5033, string dockerImage = "cezarypiatek/grpc-mock-server", string? releaseName = null)
    {
        _protoDirectory = Path.GetFullPath(protoDirectory);
        _grpcPort = grpcPort;
        _dockerImage = dockerImage;
        _releaseName = releaseName ?? "grpcmockserverconnector";
        _chartInstaller = new ChartInstaller(new ProcessLauncher(new ConsoleProcessOutputWriter()));
        _chart = new ChartFromLocalPath("./charts/grpcmockserver");
    }

    public async Task Install()
    {
        var allProtoFile = Directory.EnumerateFiles(_protoDirectory, "*.proto", SearchOption.AllDirectories);

        var overrides = new
        {
            dockerImage = _dockerImage,
            protoFiles = allProtoFile.Select(x => new
            {
                key = Guid.NewGuid().ToString("N"),
                path = x.Remove(0, _protoDirectory.Length).Replace("\\", "/").Trim('/'),
                content = File.ReadAllText(x)
            }).ToArray()
        };

        _release = await _chartInstaller.Install(_chart, _releaseName, overrides);
        await _release.StartPortForward(serviceName: $"{_releaseName}-grpcmockserver-service", servicePort: 5033, localPort: _grpcPort);
        _wireMockPort = await _release.StartPortForward(serviceName: $"{_releaseName}-grpcmockserver-service", servicePort: 9095);
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