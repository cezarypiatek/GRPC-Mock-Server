using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

[Generator]
#pragma warning disable RS1036
public class GrpcToRestProxyGenerator:IIncrementalGenerator
#pragma warning restore RS1036

{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var classDeclarations = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (s, _) =>
                {
                    //if (s is AttributeSyntax {Name:IdentifierNameSyntax{Identifier:{Text: "GrpcMockServerFor"}}, ArgumentList.Arguments.Count: > 0}  attribute)
                    //{
                    //    if(attribute.ArgumentList.Arguments[0].Expression is TypeOfExpressionSyntax {Type: {} typeSyntax})
                    //    {
                    //        return true;
                    //    }
                    //}

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
                transform: static (ctx, _) => ctx.Node)
            .Where(static m => m is not null);

        IncrementalValueProvider<(Compilation, ImmutableArray<SyntaxNode>)> compilationAndClasses = context.CompilationProvider.Combine(classDeclarations.Collect());
        context.RegisterSourceOutput(compilationAndClasses,
            static (spc, source) => Execute(source.Item1, source.Item2, spc));


    }
    

    private static void Execute(Compilation compilation, ImmutableArray<SyntaxNode> classes, SourceProductionContext context)
    {
        if (classes.IsDefaultOrEmpty)
        {
            // nothing to do yet
            return;
        }

        var proxyBuiler = new StringBuilder();
        var services = new List<string>();
        foreach (var el in classes.Distinct())
        {
            var semanticModel = compilation.GetSemanticModel(el.SyntaxTree);
            if (el is not ClassDeclarationSyntax serviceBaseClass)
            {
                //if (el is AttributeSyntax { Name: IdentifierNameSyntax { Identifier: { Text: "GrpcMockServerFor" } }, ArgumentList.Arguments.Count: > 0 } attribute)
                //{
                //    if (attribute.ArgumentList.Arguments[0].Expression is TypeOfExpressionSyntax { Type: { } typeSyntax })
                //    {
                //        var ll = semanticModel.GetSymbolInfo(typeSyntax);
                //        if (ll.Symbol is INamedTypeSymbol nt)
                //        {
                //            foreach (var member in nt.GetMembers())
                //            {
                //                if (member is IMethodSymbol method)
                //                {
                //                    var display = method.ToMinimalDisplayString(semanticModel,0, SymbolDisplayFormat.CSharpErrorMessageFormat);
                //                }
                //            }

                //            if (nt.ContainingSymbol is INamedTypeSymbol parent)
                //            {


                                
                //                var parentMebers = parent.GetMembers();
                //            }

                //            var members = nt.GetMembers();
                //        }
                //    }
                //}

                continue;
            }

           

            //var semanticModel = compilation.GetSemanticModel(serviceBaseClass.SyntaxTree);
            if (semanticModel.GetDeclaredSymbol(serviceBaseClass) is ITypeSymbol typeSymbol)
            {
                var serviceName = typeSymbol.Name.Substring(0, typeSymbol.Name.Length - 4);
                var className = $"{typeSymbol.ContainingNamespace.ToString().Replace(".","")}{ serviceName }GrpcToRestProxy";
                services.Add(className);
               
                {
                    proxyBuiler.AppendLine("");
                    proxyBuiler.AppendLine($"public class {className} : {typeSymbol.ToDisplayString()}");
                    proxyBuiler.AppendLine("{");
                    proxyBuiler.AppendLine("   readonly HttpClient _httpClient;");
                    proxyBuiler.AppendLine("   readonly JsonSerializerOptions _jsonSerializerOptions;");
                    proxyBuiler.AppendLine("");
                    proxyBuiler.AppendLine($"   public {className}(IHttpClientFactory factory)");
                    proxyBuiler.AppendLine("   {");
                    proxyBuiler.AppendLine($"      _httpClient = factory.CreateClient(\"WireMock\");");
                    proxyBuiler.AppendLine("      _jsonSerializerOptions = new JsonSerializerOptions();");
                    proxyBuiler.AppendLine("      _jsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.Never;");
                    proxyBuiler.AppendLine("      _jsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;");
                    proxyBuiler.AppendLine("      _jsonSerializerOptions.AddProtobufSupport();");
                    proxyBuiler.AppendLine("   }");
                    proxyBuiler.AppendLine("");

                    foreach (var methodSymbol in typeSymbol.GetMembers().OfType<IMethodSymbol>().Where(x=>x.IsVirtual))
                    {

                        proxyBuiler.AppendLine($"   public override async {methodSymbol.ReturnType} {methodSymbol.Name}({string.Join(", ", methodSymbol.Parameters.Select(x => $"{x.Type} {x.Name}"))})");
                        proxyBuiler.AppendLine("   {");



                        if (methodSymbol.Parameters.Length == 2 && methodSymbol.Parameters[0].Name == "requestStream")
                        {
                            //client stream
                            proxyBuiler.AppendLine($"        var input = new System.Collections.Generic.List<{((INamedTypeSymbol) methodSymbol.Parameters[0].Type).TypeArguments[0]}>();");
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
                            proxyBuiler.AppendLine($"        return JsonSerializer.Deserialize<global::{((INamedTypeSymbol) methodSymbol.ReturnType).TypeArguments[0]}>(resultContent, _jsonSerializerOptions)!;");

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
                            proxyBuiler.AppendLine($"        return JsonSerializer.Deserialize<global::{((INamedTypeSymbol) methodSymbol.ReturnType).TypeArguments[0]}>(resultContent, _jsonSerializerOptions)!;");
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

                            proxyBuiler.AppendLine($"        foreach(var item in JsonSerializer.Deserialize<global::{((INamedTypeSymbol) methodSymbol.Parameters[1].Type).TypeArguments[0]}[]>(resultContent, _jsonSerializerOptions)!)");
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
        sb1.AppendLine($"       System.Console.WriteLine(\"GRPC-Mock-Server generated for the following services:\");");
        foreach (var service in services)
        {
            sb1.AppendLine($"       System.Console.WriteLine(\"\t* {service}\");");
            sb1.AppendLine($"       app.MapGrpcService<{service}>();");
        }

      
        using (Stream stream =   typeof( GrpcToRestProxyGenerator).Assembly.GetManifestResourceStream("GrpcTestKit.GrpcMockServerGenerator.GrpcMockServer.cs")!)
        using (StreamReader reader = new StreamReader(stream))
        {
            string result = reader.ReadToEnd();
            var mainContent = result.Replace("//REPLACE:RegisterProxy", sb1.ToString())
                .Replace("//REPLACE:ProxyDefinition", proxyBuiler.ToString());
            context.AddSource($"GrpcMockServer.g.cs", SourceText.From(mainContent, Encoding.UTF8));
        }
    }

    private static void RelayHeaders(StringBuilder sb)
    {
        sb.AppendLine(@"        foreach (var requestHeader in context.RequestHeaders)
        {
            httpRequest.Headers.Add(requestHeader.Key, requestHeader.Value);
        }");
    }
}
