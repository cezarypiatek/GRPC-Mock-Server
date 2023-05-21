using System.Diagnostics;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using RestEase;
using WireMock.Client;

namespace GrpcTestKit;

public class GrpcMockServerConnector : ITestComponentConnector<IGrpcMockClient>
{
    private readonly string _protoDirectory;
    private readonly string _dockerImage;
    private readonly int _grpcPort;
    private IContainer? container;

    public GrpcMockServerConnector(string protoDirectory, int grpcPort = 5033, string dockerImage = "cezarypiatek/grpc-mock-server")
    {
        _protoDirectory = protoDirectory;
        _dockerImage = dockerImage;
        _grpcPort = grpcPort;
    }

    public async Task Install()
    {
        this.container = new ContainerBuilder()
            .WithImage(_dockerImage)
            .WithPortBinding(9095, assignRandomHostPort:true)
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
        if (this.container == null)
        {
            throw new InvalidOperationException("Connector not installed. Call Install() method first.");
        }

        var wireMockPort = container.GetMappedPublicPort("9095");
        var wireMockApiClient = RestClient.For<IWireMockAdminApi>($"http://localhost:{wireMockPort}");
        return new GrpcMockClient(wireMockApiClient);
    }

    public async ValueTask DisposeAsync()
    {
        if (container != null)
        {
            await container.DisposeAsync();
        }
    }
}