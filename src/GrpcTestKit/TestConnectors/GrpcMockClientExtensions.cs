using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using WireMock.Admin.Mappings;

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
    /// <param name="activityScopeLimit">Filter requests by current trace-id</param>
    public static async Task<IAsyncDisposable> MockRequestReply(this IGrpcMockClient @this, string serviceName, string methodName, object? request, object response, bool activityScopeLimit = true)
    {
        var responseBody = JsonSerializer.Serialize(response, options: _jsonSerializerOptions);
        return await @this.MockEndpoint(builder =>
        {
            builder.WithRequest(x =>
            {

                var rmb = x.UsingPost()
                    .WithPath($"/{serviceName}/{methodName}");

                if (activityScopeLimit && System.Diagnostics.Activity.Current is { } currentActivity)
                {
                    rmb.WithHeaders(x => x.Add(new HeaderModel
                    {
                        Name = "traceparent",
                        Matchers = new List<MatcherModel>
                        {
                            new ()
                            {
                                Name = "WildcardMatcher",
                                Pattern = $"*{currentActivity.TraceId}*",
                                IgnoreCase = true
                            }
                        }
                    }));
                }
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
    /// <param name="activityScopeLimit">Filter requests by current trace-id</param>
    public static async Task<IAsyncDisposable> MockClientStreaming(this IGrpcMockClient @this, string serviceName, string methodName, IReadOnlyList<object> requests, object response, bool activityScopeLimit = true) 
        => await @this.MockRequestReply(serviceName, methodName, requests, response, activityScopeLimit);


    /// <summary>
    ///     Define mock for GRPC service server-streaming method
    /// </summary>
    /// <param name="this"></param>
    /// <param name="serviceName">Fully qualified service name (prefixed with package name)</param>
    /// <param name="methodName">Method name</param>
    /// <param name="request">Expected incoming message</param>
    /// <param name="activityScopeLimit">Filter requests by current trace-id</param>
    public static async Task<IAsyncDisposable> MockServerStreaming(this IGrpcMockClient @this, string serviceName, string methodName, object? request, IReadOnlyList<object> response, bool activityScopeLimit = true) 
        => await @this.MockRequestReply(serviceName, methodName, request, response, activityScopeLimit);
}