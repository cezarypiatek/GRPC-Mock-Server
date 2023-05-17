using WireMock.Server;

namespace Demo
{
    public class WireMockHostedService:IHostedService
    {
        private WireMockServer? _wireMock;

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _wireMock = WireMockServer.StartWithAdminInterface(port:9095);
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {   
            _wireMock?.Stop();
            return Task.CompletedTask;
        }
    }
}
