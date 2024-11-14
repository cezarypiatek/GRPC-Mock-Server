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
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using GrpcTestKit.TestConnectors;
using System;
using System.Net;
using System.Threading;
using System.Linq;

/*MockServerNamespace*/

public partial class /*MockServerName*/: IGrpcMockServerConnector
{
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? ServerTask { get; set; }
    private WireMockServer? _wireMock;
    private int _grpcPort;
    private int _wireMockPort;
    private string _stubbingUrl = "";

    public /*MockServerName*/ (int grpcPort, int wireMockPort)
    {
        _grpcPort = grpcPort;
        _wireMockPort = wireMockPort;
    }

    public Task<GrpcMockServerConnectionInfo> Install()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddGrpc();
       
        builder.Services.AddHttpClient("WireMock", config =>
        {
            var url = $"http://localhost:{_wireMockPort}";
            config.BaseAddress = new Uri(url);
        });

        builder.WebHost
            .UseUrls()
            .UseKestrel(options =>
            {
                options.ListenAnyIP(_grpcPort, listenOptions =>
                {
                    listenOptions.Protocols = HttpProtocols.Http2;
                });
            });
     
        var app = builder.Build();
        app.UseHttpsRedirection();
        //REPLACE:RegisterProxy
        app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");

        _wireMock = WireMockServer.StartWithAdminInterface(port: _wireMockPort);

        this._cancellationTokenSource = new CancellationTokenSource();
        ServerTask = app.RunAsync(this._cancellationTokenSource.Token);
        Console.WriteLine("GRPC-Mock-Server is ready");

        this._stubbingUrl = $"http://localhost:{_wireMockPort}";
        var connectionInfo = new GrpcMockServerConnectionInfo($"http://localhost:{_grpcPort}", _stubbingUrl);
        return Task.FromResult(connectionInfo);
    }

    public Task Wait()
    {
        if (ServerTask == null)
        {
            throw new InvalidOperationException("Connector not installed. Call Install() method first.");
        }

        return ServerTask;
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

    public class InfoBinder : ServiceBinderBase
    {
        public HashSet<string> Services { get; } = new HashSet<string>();

        public override void AddMethod<TRequest, TResponse>(Method<TRequest, TResponse> method, UnaryServerMethod<TRequest, TResponse> handler)
        {
            Services.Add(method.ServiceName);
        }

        public override void AddMethod<TRequest, TResponse>(Method<TRequest, TResponse> method, ClientStreamingServerMethod<TRequest, TResponse> handler)
        {
            Services.Add(method.ServiceName);
        }

        public override void AddMethod<TRequest, TResponse>(Method<TRequest, TResponse> method, ServerStreamingServerMethod<TRequest, TResponse> handler)
        {
            Services.Add(method.ServiceName);
        }

        public override void AddMethod<TRequest, TResponse>(Method<TRequest, TResponse> method, DuplexStreamingServerMethod<TRequest, TResponse> handler)
        {
            Services.Add(method.ServiceName);
        }
    }

    //REPLACE:ProxyDefinition
}