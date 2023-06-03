using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

public class ProxyBuilder
{
    private readonly string _mockServerTypeName;
    private readonly string _mockServerNamespace;
    StringBuilder proxyBuiler = new StringBuilder();
    List<string> services = new List<string>();

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
                var className = $"{typeSymbol.ContainingNamespace.ToString().Replace(".", "")}{serviceName}GrpcToRestProxy";
                services.Add(className);

                {
                    proxyBuiler.AppendLine("");
                    proxyBuiler.AppendLine($"public class {className} : {typeSymbol.ToDisplayString()}");
                    proxyBuiler.AppendLine("{");
                    proxyBuiler.AppendLine($@"

    public static IReadOnlyCollection<string> GetServices()
    {{
        var impl = new {className}(null!);
        var infoBinder = new InfoBinder();
        {typeSymbol.ContainingType.ToDisplayString()}.BindService(infoBinder, impl);
        return infoBinder.Services;
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
                            proxyBuiler.AppendLine("        throw new System.NotSupportedException();");
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
            sb1.AppendLine($"       System.Console.WriteLine(string.Join(\"\\r\\n\", {service}.GetServices().Select(x=> $\"\\t* {{x}} \")));");
        }
        


        using (Stream stream = typeof(GrpcToRestProxyGenerator).Assembly.GetManifestResourceStream("GrpcTestKit.GrpcMockServerGenerator.GrpcMockServer.cs")!)
        using (StreamReader reader = new StreamReader(stream))
        {
            string result = reader.ReadToEnd();
            var mainContent = result.Replace("//REPLACE:RegisterProxy", sb1.ToString())
                .Replace("//REPLACE:ProxyDefinition", proxyBuiler.ToString())
                .Replace("/*MockServerNamespace*/", _mockServerNamespace)
                .Replace("/*MockServerName*/", _mockServerTypeName)
                ;
            return mainContent;
        }
    }
}