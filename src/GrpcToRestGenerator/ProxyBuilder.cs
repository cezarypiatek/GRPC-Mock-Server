using System.Text;
using Microsoft.CodeAnalysis;

public class ProxyBuilder
{
    private readonly string _mockServerTypeName;
    private readonly string _mockServerNamespace;
    StringBuilder proxyBuiler = new();
    List<string> services = new();

    private static void RelayHeaders(StringBuilder sb)
    {
        sb.AppendLine(@"        foreach (var requestHeader in context.RequestHeaders)
        {
            httpRequest.Headers.Add(requestHeader.Key, requestHeader.Value);
        }");
    }

    public ProxyBuilder(string mockServerNamespace, string mockServerTypeName)
    {
        _mockServerTypeName = mockServerTypeName;
        _mockServerNamespace = mockServerNamespace;
    }

    public string Build(IReadOnlyList<INamedTypeSymbol> proxyBaseClasses)
    {
        foreach (var typeSymbol in proxyBaseClasses)
        {
            {
                var serviceName = typeSymbol.Name.Substring(0, typeSymbol.Name.Length - 4);
                var classPrefix = $"{typeSymbol.ContainingNamespace.ToString().Replace(".", "")}{serviceName}";
                var className = $"{classPrefix}GrpcToRestProxy";
                services.Add(className);

                {

                    proxyBuiler.AppendLine("");
                    proxyBuiler.AppendLine($"public class {className} : {typeSymbol.ToDisplayString()}");
                    proxyBuiler.AppendLine("{");
                    proxyBuiler.AppendLine($@"

    private static string? _serviceName;
    
    public static string GetServiceName()
    {{
        if(_serviceName == null)
        {{
            var impl = new {className}(null!);
            var infoBinder = new InfoBinder();
            {typeSymbol.ContainingType.ToDisplayString()}.BindService(infoBinder, impl);
            _serviceName = infoBinder.Services.Distinct().FirstOrDefault() ?? """";
        }}
        return _serviceName;
    }}

");



                    proxyBuiler.AppendLine("   readonly HttpClient _httpClient;");
                    proxyBuiler.AppendLine("   readonly JsonSerializerOptions _jsonSerializerOptions;");
                    proxyBuiler.AppendLine("");
                    proxyBuiler.AppendLine($"   public {className}(IHttpClientFactory factory)");
                    proxyBuiler.AppendLine("   {");
                    proxyBuiler.AppendLine($"      _httpClient = factory?.CreateClient(\"WireMock\")!;");
                    proxyBuiler.AppendLine("      _jsonSerializerOptions = new JsonSerializerOptions();");
                    proxyBuiler.AppendLine("      _jsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.Never;");
                    proxyBuiler.AppendLine("      _jsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;");
                    proxyBuiler.AppendLine("      _jsonSerializerOptions.AddProtobufSupport();");
                    proxyBuiler.AppendLine("   }");
                    proxyBuiler.AppendLine("");

                    foreach (var methodSymbol in typeSymbol.GetMembers().OfType<IMethodSymbol>().Where(x => x.IsVirtual))
                    {

                        proxyBuiler.AppendLine($"   public override async {methodSymbol.ReturnType} {methodSymbol.Name}({string.Join(", ", methodSymbol.Parameters.Select(x => $"{x.Type} {x.Name}"))})");
                        proxyBuiler.AppendLine("   {");



                        if (methodSymbol.Parameters.Length == 2 && methodSymbol.Parameters[0].Name == "requestStream")
                        {
                            //client stream
                            proxyBuiler.AppendLine($"        var input = new System.Collections.Generic.List<{((INamedTypeSymbol)methodSymbol.Parameters[0].Type).TypeArguments[0]}>();");
                            proxyBuiler.AppendLine($"        await foreach (var item in  requestStream.ReadAllAsync(context.CancellationToken))");
                            proxyBuiler.AppendLine("        {");
                            proxyBuiler.AppendLine($"            context.CancellationToken.ThrowIfCancellationRequested();");
                            proxyBuiler.AppendLine($"            input.Add(item);");
                            proxyBuiler.AppendLine("        }");
                            proxyBuiler.AppendLine($"        var payload = JsonSerializer.Serialize(input, options: _jsonSerializerOptions);");
                            proxyBuiler.AppendLine($"        var httpRequest = new HttpRequestMessage(HttpMethod.Post, context.Method);");
                            proxyBuiler.AppendLine($"        httpRequest.Content = new StringContent(payload, System.Text.Encoding.UTF8, \"application/json\");");
                            RelayHeaders(proxyBuiler);
                            proxyBuiler.AppendLine($"        var result = await _httpClient.SendAsync(httpRequest, context.CancellationToken);");
                            proxyBuiler.AppendLine($"        result.EnsureSuccessStatusCode();");
                            proxyBuiler.AppendLine($"        var resultContent = await result.Content.ReadAsStringAsync(context.CancellationToken);");
                            proxyBuiler.AppendLine($"        return JsonSerializer.Deserialize<global::{((INamedTypeSymbol)methodSymbol.ReturnType).TypeArguments[0]}>(resultContent, _jsonSerializerOptions)!;");

                        }
                        else if (methodSymbol.Parameters.Length == 2)
                        {
                            //request-reply
                            proxyBuiler.AppendLine($"        var payload = JsonSerializer.Serialize({methodSymbol.Parameters[0].Name}, options: _jsonSerializerOptions);");
                            proxyBuiler.AppendLine($"        var httpRequest = new HttpRequestMessage(HttpMethod.Post, context.Method);");
                            proxyBuiler.AppendLine($"        httpRequest.Content = new StringContent(payload, System.Text.Encoding.UTF8, \"application/json\");");
                            RelayHeaders(proxyBuiler);
                            proxyBuiler.AppendLine($"        var result = await _httpClient.SendAsync(httpRequest, context.CancellationToken);");
                            proxyBuiler.AppendLine($"        result.EnsureSuccessStatusCode();");
                            proxyBuiler.AppendLine($"        var resultContent = await result.Content.ReadAsStringAsync(context.CancellationToken);");
                            proxyBuiler.AppendLine($"        return JsonSerializer.Deserialize<global::{((INamedTypeSymbol)methodSymbol.ReturnType).TypeArguments[0]}>(resultContent, _jsonSerializerOptions)!;");
                        }

                        if (methodSymbol.Parameters.Length == 3 && methodSymbol.Parameters[0].Name == "requestStream")
                        {
                            //duplex streaming
                            proxyBuiler.AppendLine($@"
        await foreach (var requestItem in  requestStream.ReadAllAsync(context.CancellationToken))
        {{
            context.CancellationToken.ThrowIfCancellationRequested();
            var payload = JsonSerializer.Serialize(requestItem, options: _jsonSerializerOptions);
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, context.Method);
            httpRequest.Content = new StringContent(payload, System.Text.Encoding.UTF8, ""application/json"");
            foreach (var requestHeader in context.RequestHeaders)
            {{
                httpRequest.Headers.Add(requestHeader.Key, requestHeader.Value);
            }}
            var result = await _httpClient.SendAsync(httpRequest, context.CancellationToken);
            result.EnsureSuccessStatusCode();
            result.Headers.TryGetValues(""GRPC-Action"", out var headers);         
            var actionHeader = headers?.FirstOrDefault();

            if (actionHeader  == ""ContinueRequestStream"")
            {{
                continue;
            }}
            
            var resultContent = await result.Content.ReadAsStringAsync(context.CancellationToken);
            foreach(var responseItem in JsonSerializer.Deserialize<global::{((INamedTypeSymbol)methodSymbol.Parameters[1].Type).TypeArguments[0]}[]>(resultContent, _jsonSerializerOptions)!)
            {{
                context.CancellationToken.ThrowIfCancellationRequested();
                await responseStream.WriteAsync(responseItem);
            }}
            
            if (actionHeader  == ""ResponseStreamFinished"")
            {{
                return;
            }}
        }}
");
                        }
                        else if (methodSymbol.Parameters.Length == 3)
                        {
                            //server streaming
                            proxyBuiler.AppendLine($"        var payload = JsonSerializer.Serialize({methodSymbol.Parameters[0].Name}, options: _jsonSerializerOptions);");
                            proxyBuiler.AppendLine($"        var httpRequest = new HttpRequestMessage(HttpMethod.Post, context.Method);");
                            proxyBuiler.AppendLine($"        httpRequest.Content = new StringContent(payload, System.Text.Encoding.UTF8, \"application/json\");");
                            RelayHeaders(proxyBuiler);
                            proxyBuiler.AppendLine($"        var result = await _httpClient.SendAsync(httpRequest, context.CancellationToken);");
                            proxyBuiler.AppendLine($"        result.EnsureSuccessStatusCode();");
                            proxyBuiler.AppendLine($"        var resultContent = await result.Content.ReadAsStringAsync(context.CancellationToken);");

                            proxyBuiler.AppendLine($"        foreach(var item in JsonSerializer.Deserialize<global::{((INamedTypeSymbol)methodSymbol.Parameters[1].Type).TypeArguments[0]}[]>(resultContent, _jsonSerializerOptions)!)");
                            proxyBuiler.AppendLine("        {");
                            proxyBuiler.AppendLine("            context.CancellationToken.ThrowIfCancellationRequested();");
                            proxyBuiler.AppendLine($"            await responseStream.WriteAsync(item);");
                            proxyBuiler.AppendLine("        }");
                        }



                        proxyBuiler.AppendLine("   }");
                        proxyBuiler.AppendLine();

                    }
                    proxyBuiler.AppendLine("}");
                }

                //context.AddSource($"{className}.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
            }
        }

        var sb1 = new StringBuilder();
        
        foreach (var service in services) {
            
            sb1.AppendLine($"       app.MapGrpcService<{service}>();");
        }

        sb1.AppendLine($"       System.Console.WriteLine(\"GRPC-Mock-Server generated for the following services:\");");
        foreach (var service in services)
        {
            sb1.AppendLine($"       System.Console.WriteLine(\"\t*\"+{service}.GetServiceName());");
        }


        var assembly = typeof(GrpcToRestProxyGenerator).Assembly;
        using (Stream stream = assembly.GetManifestResourceStream("GrpcTestKit.GrpcMockServerGenerator.GrpcMockServer.cs")!)
        using (StreamReader reader = new StreamReader(stream))
        {
            string result = reader.ReadToEnd();
            var mainContent = result.Replace("//REPLACE:RegisterProxy", sb1.ToString())
                .Replace("//REPLACE:ProxyDefinition", proxyBuiler.ToString())
                .Replace("/*MockServerNamespace*/", string.IsNullOrWhiteSpace(_mockServerNamespace) || _mockServerNamespace == "<global namespace>" ? "":$"namespace {_mockServerNamespace};")
                .Replace("/*MockServerName*/", _mockServerTypeName)
                ;
            return mainContent;
        }
    }
}