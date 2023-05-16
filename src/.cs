using grpc = global::Grpc.Core;
using System.Text.Json;

public class GreeterGrpcToRestProxy2 : GrpcGreeter.Greeter.GreeterBase
{
    System.Net.Http.HttpClient _httpClient;
    JsonSerializerOptions _jsonSerializerOptions;

    public GreeterGrpcToRestProxy(System.Net.Http.HttpClient httpClient)
    {
        _httpClient = httpClient;
        _jsonSerializerOptions = new JsonSerializerOptions();
        _jsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        _jsonSerializerOptions.AddProtobufSupport();
    }

    public override async global::System.Threading.Tasks.Task<global::GrpcGreeter.HelloReply> SayHello(global::GrpcGreeter.HelloRequest request, grpc::ServerCallContext context)
    {
        var payload = JsonSerializer.Serialize(request, options: _jsonSerializerOptions);
        var content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json");
        var result = await _httpClient.PostAsync("/Greeter/SayHello", content);
        var resultContent = await result.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<global::GrpcGreeter.HelloReply>(resultContent, _jsonSerializerOptions);
    }

}