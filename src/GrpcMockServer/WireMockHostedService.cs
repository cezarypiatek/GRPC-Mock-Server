using System.Net;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace GrpcMockServer
{
    public class WireMockHostedService:IHostedService
    {
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
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {   
            _wireMock?.Stop();
            return Task.CompletedTask;
        }
    }
}
