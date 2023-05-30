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

    public async Task Install()
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
    }

    public IGrpcMockClient CreateClient()
    {
        if (container == null)
        {
            throw new InvalidOperationException("Connector not installed. Call Install() method first.");
        }

        var wireMockPort = container.GetMappedPublicPort("9095");
        var baseUrl = $"http://localhost:{wireMockPort}";
        var wireMockApiClient = RestClient.For<IWireMockAdminApi>(baseUrl);
        return new GrpcMockClient(wireMockApiClient, baseUrl);
    }



    public async ValueTask DisposeAsync()
    {
        if (container != null)
        {
            await container.DisposeAsync();
        }
    }
}