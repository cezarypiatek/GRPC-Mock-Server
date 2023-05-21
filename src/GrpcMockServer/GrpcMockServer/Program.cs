using GrpcMockServer;
using Microsoft.AspNetCore.Server.Kestrel.Core;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddGrpc();
builder.Services.AddHostedService<WireMockHostedService>();
builder.Services.AddHttpClient("WireMock", config =>
{
    var url = "http://localhost:9095";
    config.BaseAddress = new Uri(url);
});
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(5033, listenOptions =>
    {
        listenOptions.Protocols = HttpProtocols.Http2;
    });
});

var app = builder.Build();


app.UseHttpsRedirection();
app.MapGrpcToRestProxies();
app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");

app.Run();