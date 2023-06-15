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
    ///     Define mock for GRPC service request-reply method
    /// </summary>
    /// <param name="this"></param>
    /// <param name="serviceName">Fully qualified service name (prefixed with package name)</param>
    /// <param name="methodName">Method name</param>
    /// <param name="request">Expected request</param>
    /// <param name="response">Expected response</param>
    /// <param name="activityScopeLimit">Filter request by current trace-id</param>
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
    
    public static async Task<IAsyncDisposable> MockDuplexStreaming<TRequest,TResponse>(this IGrpcMockClient @this, string serviceName, string methodName, IReadOnlyList<MessageExchange<TRequest,TResponse>> scenario, bool activityScopeLimit = true)
    {
        var scenarioName = $"Duplex-{Guid.NewGuid():N}";
        var scenarioIndex = 0;
        var stepIndex = 0;
        var handles = new List<IAsyncDisposable>();
        foreach (var (requests, response) in scenario)
        {
            var requestIndex = 0;
            foreach (var request in requests)
            {
                var handle = await @this.MockEndpoint(builder =>
                {
                    builder.WithScenario(scenarioName);
                    if (stepIndex > 0)
                    {
                        builder.WithWhenStateIs($"step{stepIndex}");
                    }

                    if ((scenarioIndex != scenario.Count-1 || requestIndex != requests.Count-1))
                    {
                        builder.WithSetStateTo($"step{stepIndex + 1}");
                    }
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
                        var requestBody = JsonSerializer.Serialize(request, options: _jsonSerializerOptions);
                        rmb.WithBody(body => body.WithMatcher(m =>
                                m.WithName("JsonPartialWildcardMatcher")
                                    .WithPattern(requestBody)
                                    .WithIgnoreCase(true)
                            )
                        );
                    }).WithResponse(r =>
                    {

                        if (requestIndex == requests.Count - 1)
                        {
                            
                            if (scenarioIndex == scenario.Count - 1)
                            {
                                r.WithHeaders(new Dictionary<string, object>
                                {
                                    ["GRPC-Action"] = "ResponseStreamFinished"
                                });                                
                            }

                            
                            
                            if (response is {Count: > 0})
                            {
                                var responseBody = JsonSerializer.Serialize(response, options: _jsonSerializerOptions);
                                r.WithBody(responseBody);    
                            }
                        }
                        else
                        {
                            r.WithHeaders(new Dictionary<string, object>
                            {
                                ["GRPC-Action"] = "ContinueRequestStream"
                            });
                        }

                        r.WithStatusCode(HttpStatusCode.OK);
                    });
                });
                handles.Add(handle);
                requestIndex++;
                stepIndex++;
            }
            scenarioIndex++;
        }

        return new MockHandle(async () =>
        {
            foreach (var handle in handles)
            {
                try
                {
                    await handle.DisposeAsync();
                }
                catch 
                {
                    // ignored
                }
            }
        });

    }

    /// <summary>
    ///     Define mock for GRPC service client-streaming method
    /// </summary>
    /// <param name="this"></param>
    /// <param name="serviceName">Fully qualified service name (prefixed with package name)</param>
    /// <param name="methodName">Method name</param>
    /// <param name="request">List of expected incoming messages</param>
    /// <param name="response">Expected response</param>
    /// <param name="activityScopeLimit">Filter request by current trace-id</param>
    public static async Task<IAsyncDisposable> MockClientStreaming(this IGrpcMockClient @this, string serviceName, string methodName, IReadOnlyList<object> request, object response, bool activityScopeLimit = true) 
        => await @this.MockRequestReply(serviceName, methodName, request, response, activityScopeLimit);


    /// <summary>
    ///     Define mock for GRPC service server-streaming method
    /// </summary>
    /// <param name="this"></param>
    /// <param name="serviceName">Fully qualified service name (prefixed with package name)</param>
    /// <param name="methodName">Method name</param>
    /// <param name="request">Expected incoming message</param>
    /// <param name="activityScopeLimit">Filter request by current trace-id</param>
    public static async Task<IAsyncDisposable> MockServerStreaming(this IGrpcMockClient @this, string serviceName, string methodName, object? request, IReadOnlyList<object> response, bool activityScopeLimit = true) 
        => await @this.MockRequestReply(serviceName, methodName, request, response, activityScopeLimit);
}