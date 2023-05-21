using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

[Generator]
public class GrpcToRestProxyGenerator:IIncrementalGenerator

{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var classDeclarations = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (s, _) =>
                {
                    if (s is ClassDeclarationSyntax c)
                    {
                        if (c.Modifiers.Count(m => m.Kind() is SyntaxKind.AbstractKeyword or SyntaxKind.PartialKeyword or SyntaxKind.PublicKeyword) == 3)
                        {
                            if (c.AttributeLists.SelectMany(x => x.Attributes).Any(x => x.Name.ToString().Contains("BindServiceMethod")))
                            {
                                return true;
                            }
                        }
                    }

                    return false;
                },
                transform: static (ctx, _) => GetProxyClass(ctx))
            .Where(static m => m is not null);

        IncrementalValueProvider<(Compilation, ImmutableArray<ClassDeclarationSyntax>)> compilationAndClasses = context.CompilationProvider.Combine(classDeclarations.Collect());
        context.RegisterSourceOutput(compilationAndClasses,
            static (spc, source) => Execute(source.Item1, source.Item2, spc));


    }
    

    private static void Execute(Compilation compilation, ImmutableArray<ClassDeclarationSyntax> classes, SourceProductionContext context)
    {
        if (classes.IsDefaultOrEmpty)
        {
            // nothing to do yet
            return;
        }

        
        var services = new List<(string,string)>();
        foreach (var serviceBaseClass in classes.Distinct())
        {
            
            var semanticModel = compilation.GetSemanticModel(serviceBaseClass.SyntaxTree);
            if (semanticModel.GetDeclaredSymbol(serviceBaseClass) is ITypeSymbol typeSymbol)
            {
                var serviceName = typeSymbol.Name.Substring(0, typeSymbol.Name.Length - 4);
                var protoServiceName = serviceName;
                if (serviceBaseClass.Parent is ClassDeclarationSyntax parentTypeSyntax && parentTypeSyntax.Members.OfType<FieldDeclarationSyntax>().SelectMany(x=>x.Declaration.Variables).FirstOrDefault(x=>x.Identifier.ValueText == "__ServiceName") is {} serviceNameField)
                {
                    protoServiceName = serviceNameField.Initializer?.Value.ToFullString().Trim('"');
                }

                var newName = $"{ serviceName }GrpcToRestProxy";
                services.Add((newName, protoServiceName));
                var sb = new StringBuilder();
                {
                    sb.AppendLine("#pragma warning disable CS8981 ");
                    sb.AppendLine("#pragma warning disable CS1998 ");
                    sb.AppendLine("using grpc = global::Grpc.Core;");
                    sb.AppendLine("using Grpc.Core;");
                    sb.AppendLine("using System.Net.Http;");
                    sb.AppendLine("using System.Text.Json;");
                    sb.AppendLine("using System.Text.Json.Serialization;");
                    sb.AppendLine("");
                    sb.AppendLine($"namespace {compilation.AssemblyName};");
                    sb.AppendLine("");
                    sb.AppendLine($"public class {newName} : {typeSymbol.ToDisplayString()}");
                    sb.AppendLine("{");
                    sb.AppendLine("   readonly HttpClient _httpClient;");
                    sb.AppendLine("   readonly JsonSerializerOptions _jsonSerializerOptions;");
                    sb.AppendLine("");
                    sb.AppendLine($"   public {newName}(IHttpClientFactory factory)");
                    sb.AppendLine("   {");
                    //sb.AppendLine($"      _httpClient = factory.CreateClient(\"{serviceName}\");");
                    sb.AppendLine($"      _httpClient = factory.CreateClient(\"WireMock\");");
                    sb.AppendLine("      _jsonSerializerOptions = new JsonSerializerOptions();");
                    sb.AppendLine("      _jsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.Never;");
                    sb.AppendLine("      _jsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;");
                    sb.AppendLine("      _jsonSerializerOptions.AddProtobufSupport();");
                    sb.AppendLine("   }");
                    sb.AppendLine("");

                    foreach (var methodDeclarationSyntax in serviceBaseClass.Members.OfType<MethodDeclarationSyntax>())
                    {
                        if (semanticModel.GetDeclaredSymbol(methodDeclarationSyntax) is { } methodSymbol)
                        {
                            var m2 = methodDeclarationSyntax
                                .WithAttributeLists(SyntaxFactory.List<AttributeListSyntax>())
                                .WithModifiers(SyntaxFactory.TokenList(
                                    SyntaxFactory.Token(SyntaxKind.PublicKeyword),
                                    SyntaxFactory.Token(SyntaxKind.OverrideKeyword), SyntaxFactory.Token(SyntaxKind.AsyncKeyword)));


                            sb.AppendLine("   " + m2.WithBody(null).NormalizeWhitespace().ToFullString());
                            sb.AppendLine("   {");

                            

                            if (methodSymbol.Parameters.Length == 2 && methodSymbol.Parameters[0].Name == "requestStream")
                            {
                                //client stream
                                sb.AppendLine($"        var input = new System.Collections.Generic.List<{((INamedTypeSymbol)methodSymbol.Parameters[0].Type).TypeArguments[0].ToString()}>();");
                                sb.AppendLine($"        await foreach (var item in  requestStream.ReadAllAsync(context.CancellationToken))");
                                sb.AppendLine("        {");
                                sb.AppendLine($"            context.CancellationToken.ThrowIfCancellationRequested();");
                                sb.AppendLine($"            input.Add(item);");
                                sb.AppendLine("        }");
                                sb.AppendLine($"        var payload = JsonSerializer.Serialize(input, options: _jsonSerializerOptions);");
                                sb.AppendLine($"        var httpRequest = new HttpRequestMessage(HttpMethod.Post, \"/{protoServiceName}/{methodSymbol.Name}\");");
                                sb.AppendLine($"        httpRequest.Content = new StringContent(payload, System.Text.Encoding.UTF8, \"application/json\");");
                                RelayHeaders(sb);
                                sb.AppendLine($"        var result = await _httpClient.SendAsync(httpRequest, context.CancellationToken);");
                                sb.AppendLine($"        result.EnsureSuccessStatusCode();");
                                sb.AppendLine($"        var resultContent = await result.Content.ReadAsStringAsync(context.CancellationToken);");
                                sb.AppendLine($"        return JsonSerializer.Deserialize<global::{((INamedTypeSymbol)methodSymbol.ReturnType).TypeArguments[0].ToString()}>(resultContent, _jsonSerializerOptions)!;");

                            }
                            else if (methodSymbol.Parameters.Length == 2)
                            {
                                //request-reply
                                sb.AppendLine($"        var payload = JsonSerializer.Serialize({methodSymbol.Parameters[0].Name}, options: _jsonSerializerOptions);");
                                sb.AppendLine($"        var httpRequest = new HttpRequestMessage(HttpMethod.Post, \"/{protoServiceName}/{methodSymbol.Name}\");");
                                sb.AppendLine($"        httpRequest.Content = new StringContent(payload, System.Text.Encoding.UTF8, \"application/json\");");
                                RelayHeaders(sb);
                                sb.AppendLine($"        var result = await _httpClient.SendAsync(httpRequest, context.CancellationToken);");
                                sb.AppendLine($"        result.EnsureSuccessStatusCode();");
                                sb.AppendLine($"        var resultContent = await result.Content.ReadAsStringAsync(context.CancellationToken);");
                                sb.AppendLine($"        return JsonSerializer.Deserialize<global::{((INamedTypeSymbol)methodSymbol.ReturnType).TypeArguments[0].ToString()}>(resultContent, _jsonSerializerOptions)!;");
                            }

                            if (methodSymbol.Parameters.Length == 3 && methodSymbol.Parameters[0].Name == "requestStream")
                            {
                                //both side streaming
                                sb.AppendLine("        throw new System.NotSupportedException();");
                            }
                            else if (methodSymbol.Parameters.Length == 3)
                            {
                                //server streaming
                                sb.AppendLine($"        var payload = JsonSerializer.Serialize({methodSymbol.Parameters[0].Name}, options: _jsonSerializerOptions);");
                                sb.AppendLine($"        var httpRequest = new HttpRequestMessage(HttpMethod.Post, \"/{protoServiceName}/{methodSymbol.Name}\");");
                                sb.AppendLine($"        httpRequest.Content = new StringContent(payload, System.Text.Encoding.UTF8, \"application/json\");");
                                RelayHeaders(sb);
                                sb.AppendLine($"        var result = await _httpClient.SendAsync(httpRequest, context.CancellationToken);");
                                sb.AppendLine($"        result.EnsureSuccessStatusCode();");
                                sb.AppendLine($"        var resultContent = await result.Content.ReadAsStringAsync(context.CancellationToken);");

                                sb.AppendLine($"        foreach(var item in JsonSerializer.Deserialize<global::{((INamedTypeSymbol)methodSymbol.Parameters[1].Type).TypeArguments[0].ToString()}[]>(resultContent, _jsonSerializerOptions)!)");
                                sb.AppendLine("        {");
                                sb.AppendLine("            context.CancellationToken.ThrowIfCancellationRequested();");
                                sb.AppendLine($"            await responseStream.WriteAsync(item);");
                                sb.AppendLine("        }");
                            }

                           

                            sb.AppendLine("   }");
                            sb.AppendLine();
                        }
                       
                    }
                    sb.AppendLine("}");
                }

                context.AddSource($"{newName}.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
            }
        }

        var sb1 = new StringBuilder();
        sb1.AppendLine($"namespace {compilation.AssemblyName};");
        sb1.AppendLine("");
        sb1.AppendLine("public static partial class GrpcToRestProxyExtensions");
        sb1.AppendLine("{");
        sb1.AppendLine("    static partial void MapAllProxies(Microsoft.AspNetCore.Routing.IEndpointRouteBuilder builder)");
        sb1.AppendLine("    {");
        sb1.AppendLine("        System.Console.WriteLine(\"Registered GRPC-To-Rest proxies:\");");
        foreach (var service in services)
        {
            var (dotnetName, protoName) = service;
            sb1.Append($"        System.Console.WriteLine(\"\t* {protoName}\");");
            sb1.AppendLine($"        builder.MapGrpcService<{dotnetName}>();");
        }
        sb1.AppendLine("    }");
        sb1.AppendLine("}");

        context.AddSource($"GrpcToRestProxyExtensions.g.cs", SourceText.From(sb1.ToString(), Encoding.UTF8));
    }

    private static void RelayHeaders(StringBuilder sb)
    {
        sb.AppendLine(@"        foreach (var requestHeader in context.RequestHeaders)
        {
            httpRequest.Headers.Add(requestHeader.Key, requestHeader.Value);
        }");
    }


    private static ClassDeclarationSyntax GetProxyClass(GeneratorSyntaxContext ctx)
    {
        return (ClassDeclarationSyntax) ctx.Node;
    }
}
