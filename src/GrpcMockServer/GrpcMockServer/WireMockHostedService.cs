using System.Net;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace GrpcMockServer
{
    public class WireMockHostedService:IHostedService
    {
        private readonly ILogger<WireMockHostedService> _logger;

        public WireMockHostedService(ILogger<WireMockHostedService> logger)
        {
            _logger = logger;
        }

        private WireMockServer? _wireMock;

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _wireMock = WireMockServer.StartWithAdminInterface(port:9095);
            _wireMock.Given(Request.Create()
                .WithPath("/greet.Greeter/TestRequestReply"))
                .RespondWith(Response.Create()
                    .WithStatusCode(HttpStatusCode.OK)
                    .WithBodyAsJson(new
                    {
                        message = "Hello from WireMock"
                    }));
            _logger.LogInformation("GRPC-Mock-Server is ready");
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {   
            _wireMock?.Stop();
            return Task.CompletedTask;
        }
    }
}
