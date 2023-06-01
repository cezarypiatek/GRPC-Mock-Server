using System.Diagnostics;
using System.Reflection;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using RestEase;
using WireMock.Client;

namespace GrpcTestKit.TestConnectors.Docker;

public class TestContainerGrpcMockServerConnector : IGrpcMockServerConnector
{
    private readonly string _protoDirectory;
    private readonly string _dockerImage;
    private readonly int _grpcPort;
    private IContainer? container;
    private string? _stubbingUrl;

    public TestContainerGrpcMockServerConnector(string protoDirectory, int grpcPort = 5033, string dockerImage = "cezarypiatek/grpc-mock-server")
    {
        if (Path.IsPathRooted(protoDirectory))
        {
            _protoDirectory = protoDirectory;
        }
        else
        {
            _protoDirectory = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, protoDirectory);
        }

        _dockerImage = dockerImage;
        _grpcPort = grpcPort;
    }

    public async Task<GrpcMockServerConnectionInfo> Install()
    {
        container = new ContainerBuilder()
            .WithImage(_dockerImage)
            .WithPortBinding(9095, assignRandomHostPort: true)
            .WithPortBinding(5033, _grpcPort)
            .WithOutputConsumer(Consume.RedirectStdoutAndStderrToConsole())
            .WithWaitStrategy(
                Wait.ForUnixContainer()
                    .UntilMessageIsLogged("GRPC-Mock-Server is ready")
            )
            .WithBindMount(_protoDirectory, "/protos")
            .WithAutoRemove(true)
            .WithCleanUp(false)
            .Build();
        var st = Stopwatch.StartNew();
        await container.StartAsync();
        st.Stop();
        Console.WriteLine($"Container startup time: {st.Elapsed}");
        
        var wireMockPort = container.GetMappedPublicPort("9095");
        this._stubbingUrl = $"http://localhost:{wireMockPort}";
        return new GrpcMockServerConnectionInfo($"http://localhost:{_grpcPort}", _stubbingUrl);
    }

    public IGrpcMockClient CreateClient()
    {
        if (_stubbingUrl == null)
        {
            throw new InvalidOperationException("Connector not installed. Call Install() method first.");
        }

        return GrpcMockClient.FromWireMockUrl(_stubbingUrl);
    }

    public async ValueTask DisposeAsync()
    {
        if (container != null)
        {
            await container.DisposeAsync();
        }
    }
}