using RestEase;
using SmoothSailing;
using WireMock.Client;
using System.Text;

namespace GrpcTestKit.TestConnectors.Kubernetes;

public class TestChartGrpcMockServerConnector : IGrpcMockServerConnector
{
    private readonly TestChartGrpcMockServerConnectorSettings _settings;
    private readonly string? _protoDirectory;
    private readonly ChartInstaller _chartInstaller;
    private readonly ChartFromLocalPath _chart;
    private Release? _release;
    private GrpcMockServerConnectionInfo? _connectionInfo;

    public TestChartGrpcMockServerConnector(TestChartGrpcMockServerConnectorSettings settings)
    {
        _settings = settings;
        _protoDirectory = settings.ProtoDirectory;
        _chartInstaller = new ChartInstaller();
        _chart = new ChartFromLocalPath("./charts/grpcmockserver");
    }

    public async Task<GrpcMockServerConnectionInfo> Install()
    {
        var overrides = new
        {
            dockerImage = _settings.DockerImage,
            grpcPort = _settings.GrpcPort,
            stubbingPort = _settings.StubbingPort,
            configMaps = CreateConfigMapsWithProto().ToArray(),
            envVariables = _settings.EnvVariables
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

    private List<object> CreateConfigMapsWithProto()
    {
        if (_protoDirectory == null || string.IsNullOrWhiteSpace(_protoDirectory))
        {
            return new List<object>();
        }
        
        var fullProtoPath = Path.GetFullPath(_protoDirectory);
        var allProtoFile = Directory.EnumerateFiles(fullProtoPath, "*.proto", SearchOption.AllDirectories);

        const int maxConfigMapSizeBytes = 512 * 1024; // 0.5 MB
        var configMaps = new List<object>();
        var currentConfigMapFiles = new List<object>();
        var currentConfigMapSize = 0;
        var configMapIndex = 0;

        foreach (var protoFile in allProtoFile)
        {
            var content = File.ReadAllText(protoFile);
            var path = protoFile.Remove(0, fullProtoPath.Length).Replace("\\", "/").Trim('/');
            var key = Guid.NewGuid().ToString("N");
            
            // Estimate size (content + some overhead for YAML structure)
            var estimatedSize = Encoding.UTF8.GetByteCount(content) + Encoding.UTF8.GetByteCount(key) + Encoding.UTF8.GetByteCount(path) + 100;

            // If adding this file would exceed the limit and we already have files, start a new ConfigMap
            if (currentConfigMapFiles.Count > 0 && currentConfigMapSize + estimatedSize > maxConfigMapSizeBytes)
            {
                configMaps.Add(new
                {
                    index = configMapIndex,
                    files = currentConfigMapFiles.ToArray()
                });
                
                currentConfigMapFiles = new List<object>();
                currentConfigMapSize = 0;
                configMapIndex++;
            }

            currentConfigMapFiles.Add(new
            {
                key = key,
                path = path,
                content = content
            });
            currentConfigMapSize += estimatedSize;
        }

        // Add the last ConfigMap if it has any files
        if (currentConfigMapFiles.Count > 0)
        {
            configMaps.Add(new
            {
                index = configMapIndex,
                files = currentConfigMapFiles.ToArray()
            });
        }

        return configMaps;
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