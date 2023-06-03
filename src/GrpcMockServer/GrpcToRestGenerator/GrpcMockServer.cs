#nullable enable
#pragma warning disable CS8981 
#pragma warning disable CS1998 
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WireMock.Server;
using grpc = global::Grpc.Core;
using Grpc.Core;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace /*MockServerNamespace*/;

public partial class /*MockServerName*/: IAsyncDisposable
{
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? ServerTask { get; set; }
    private WireMockServer? _wireMock;

    public void Start(int grpcPort, int wireMockPort) => StartAsync(grpcPort, wireMockPort);

    public Task StartAsync(int grpcPort, int wireMockPort)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddGrpc();
       
        builder.Services.AddHttpClient("WireMock", config =>
        {
            var url = $"http://localhost:{wireMockPort}";
            config.BaseAddress = new Uri(url);
        });

        builder.WebHost
            .UseUrls()
            .UseKestrel(options =>
            {
                options.ListenAnyIP(grpcPort, listenOptions =>
                {
                    listenOptions.Protocols = HttpProtocols.Http2;
                });
            });
     
        var app = builder.Build();
        app.UseHttpsRedirection();
        //REPLACE:RegisterProxy
        app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");

        _wireMock = WireMockServer.StartWithAdminInterface(port: wireMockPort);

        this._cancellationTokenSource = new CancellationTokenSource();
        ServerTask = app.RunAsync(this._cancellationTokenSource.Token);
        Console.WriteLine("GRPC-Mock-Server is ready");
        return ServerTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (_cancellationTokenSource != null)
        {
            _wireMock?.Stop();
            _cancellationTokenSource.Cancel();

            if (ServerTask != null)
            {
                try
                {
                    await ServerTask;
                }
                catch (Exception e)
                {
                    if (e is not TaskCanceledException)
                    {
                        throw;
                    }
                }
            }
        }
    }

    //REPLACE:ProxyDefinition
}