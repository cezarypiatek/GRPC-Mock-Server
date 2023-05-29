using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GrpcTestKit.TestConnectors;

public static class GrpcMockClientExtensions
{
    private static readonly JsonSerializerOptions _jsonSerializerOptions;

    static GrpcMockClientExtensions()
    {
        _jsonSerializerOptions = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        _jsonSerializerOptions.AddProtobufSupport();
    }

    /// <summary>
    ///     Define mock for GRPC service requests-reply method
    /// </summary>
    /// <param name="this"></param>
    /// <param name="serviceName">Fully qualified service name (prefixed with package name)</param>
    /// <param name="methodName">Method name</param>
    /// <param name="request">Expected request</param>
    /// <param name="response">Expected response</param>
    public static async Task MockRequestReply(this IGrpcMockClient @this, string serviceName, string methodName, object? request, object response)
    {
        var responseBody = JsonSerializer.Serialize(response, options: _jsonSerializerOptions);
        await @this.MockEndpoint(builder =>
        {
            builder.WithRequest(x =>
            {

                var rmb = x.UsingPost()
                    .WithPath($"/{serviceName}/{methodName}");

                if (request != null)
                {
                    var requestBody = JsonSerializer.Serialize(request, options: _jsonSerializerOptions);
                    rmb.WithBody(body => body.WithMatcher(m =>
                            m.WithName("JsonPartialWildcardMatcher")
                                .WithPattern(requestBody)
                                .WithIgnoreCase(true)
                        )
                    );
                }
            }).WithResponse(r => r.WithStatusCode(HttpStatusCode.OK).WithBody(responseBody));
        });
    }

    /// <summary>
    ///     Define mock for GRPC service client-streaming method
    /// </summary>
    /// <param name="this"></param>
    /// <param name="serviceName">Fully qualified service name (prefixed with package name)</param>
    /// <param name="methodName">Method name</param>
    /// <param name="requests">List of expected incoming messages</param>
    /// <param name="response">Expected response</param>
    /// <returns></returns>
    public static async Task MockClientStreaming(this IGrpcMockClient @this, string serviceName, string methodName, IReadOnlyList<object> requests, object response) => await @this.MockRequestReply(serviceName, methodName, requests, response);


    /// <summary>
    ///     Define mock for GRPC service server-streaming method
    /// </summary>
    /// <param name="this"></param>
    /// <param name="serviceName">Fully qualified service name (prefixed with package name)</param>
    /// <param name="methodName">Method name</param>
    /// <param name="request">Expected incoming message</param>
    /// <param name="response">Expected response list</param>
    /// <returns></returns>
    public static async Task MockServerStreaming(this IGrpcMockClient @this, string serviceName, string methodName, object? request, IReadOnlyList<object> response) => await @this.MockRequestReply(serviceName, methodName, request, response);
}