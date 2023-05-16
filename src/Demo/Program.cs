using System.Net;
using System.Reflection.PortableExecutable;
using System.Text.Json;
using Demo;
using Microsoft.AspNetCore.Server.Kestrel.Core;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddGrpc();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpClient("Greeter", config =>
{
    var url = "https://example.com/api";
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

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}


var jsonSerializerOptions = new JsonSerializerOptions();
jsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
jsonSerializerOptions.AddProtobufSupport();

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();
app.MapGrpcToRestProxies();
app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");

app.Run();
System.Net.Http.HttpClient _httpClient;

