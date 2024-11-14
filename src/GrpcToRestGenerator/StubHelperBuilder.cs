using System.Text;
using Microsoft.CodeAnalysis;

public class StubHelperBuilder
{
    private readonly string _helperTypeName;
    private readonly string _helperNamespace;
    readonly StringBuilder _stubBuilder = new StringBuilder();


    public StubHelperBuilder(string helperNamespace, string helperTypeName)
    {
        _helperTypeName = helperTypeName;
        _helperNamespace = helperNamespace;
    }

    public string Build(IReadOnlyList<INamedTypeSymbol> proxyBaseClasses)
    {
        foreach (var typeSymbol in proxyBaseClasses)
        {
            {
                {
                    _stubBuilder.AppendLine("using System;");
                    _stubBuilder.AppendLine("using GrpcTestKit.TestConnectors;");
                    _stubBuilder.AppendLine("using System.Threading;");
                    _stubBuilder.AppendLine("using System.Threading.Tasks;");
                    _stubBuilder.AppendLine("using System.Linq;");
                    _stubBuilder.AppendLine("using System.Collections.Generic;");
                    if (string.IsNullOrWhiteSpace(_helperNamespace) == false)
                    {
                        _stubBuilder.AppendLine($"namespace {_helperNamespace};");
                    }

                    _stubBuilder.AppendLine(@$"


public partial class {_helperTypeName}
{{
    private readonly IGrpcMockClient _grpcMockClient;
    private readonly string _serviceName;

    public {_helperTypeName}(IGrpcMockClient grpcMockClient)
    {{
        _grpcMockClient = grpcMockClient;
        _serviceName = typeof({typeSymbol.ContainingType}).GetField(""__ServiceName"", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)?.GetValue(null) as string ?? throw new InvalidOperationException(""Missing info about service name"");
    }}

");               

                    foreach (var methodSymbol in typeSymbol.GetMembers().OfType<IMethodSymbol>().Where(x => x.IsVirtual))
                    {

                        if (methodSymbol.Parameters.Length == 2 && methodSymbol.Parameters[0].Name == "requestStream")
                        {
                            

                            _stubBuilder.AppendLine(@$"
public async Task<IAsyncDisposable> Mock{methodSymbol.Name}(IReadOnlyList<{((INamedTypeSymbol)methodSymbol.Parameters[0].Type).TypeArguments[0]}> request, {((INamedTypeSymbol)methodSymbol.ReturnType).TypeArguments[0]} response)
{{
    return await _grpcMockClient.MockClientStreaming
    (
        serviceName: _serviceName,
        methodName: ""{methodSymbol.Name}"",
        request:request,
        response: response
    );
}}
                        ");


                        }
                        else if (methodSymbol.Parameters.Length == 2)
                        {
                            //request-reply

                            _stubBuilder.AppendLine(@$"
public async Task<IAsyncDisposable> Mock{methodSymbol.Name}({methodSymbol.Parameters[0].Type} request, {((INamedTypeSymbol)methodSymbol.ReturnType).TypeArguments[0]} response)
{{
    return await _grpcMockClient.MockRequestReply
    (
        serviceName: _serviceName,
        methodName: ""{methodSymbol.Name}"",
        request:request,
        response: response
    );
}}
                        ");
                        }

                        if (methodSymbol.Parameters.Length == 3 && methodSymbol.Parameters[0].Name == "requestStream")
                        {
                            //duplex streaming
                            _stubBuilder.AppendLine(@$"
public async Task<IAsyncDisposable> Mock{methodSymbol.Name}(IReadOnlyList<MessageExchange<{((INamedTypeSymbol)methodSymbol.Parameters[0].Type).TypeArguments[0]},{((INamedTypeSymbol)methodSymbol.Parameters[1].Type).TypeArguments[0]}>> scenario)
{{
    return await _grpcMockClient.MockDuplexStreaming
    (
        serviceName: _serviceName,
        methodName: ""{methodSymbol.Name}"",
        scenario: scenario
    );
}}");
                        }
                        else if (methodSymbol.Parameters.Length == 3)
                        {
                            //server streaming
                            _stubBuilder.AppendLine(@$"
public async Task<IAsyncDisposable> Mock{methodSymbol.Name}({methodSymbol.Parameters[0].Type} request, IReadOnlyList<{((INamedTypeSymbol)methodSymbol.Parameters[1].Type).TypeArguments[0]}> response)
{{
    return await _grpcMockClient.MockServerStreaming
    (
        serviceName: _serviceName,
        methodName: ""{methodSymbol.Name}"",
        request:request,
        response: response
    );
}}");
                        }


                    }
                    _stubBuilder.AppendLine("}");
                }

            }
        }

        return _stubBuilder.ToString();
    }
}