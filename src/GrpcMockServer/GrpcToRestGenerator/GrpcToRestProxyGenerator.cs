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

        var baseTypes = classes.OfType<ClassDeclarationSyntax>().Distinct().Select(el =>
        {
            var semanticModel = compilation.GetSemanticModel(el.SyntaxTree);
            return semanticModel.GetDeclaredSymbol(el);
        }).OfType<INamedTypeSymbol>().ToList();

        var b = new ProxyBuilder("GrpcTestKit", "GrpcMockServer");
        var output = b.Build(baseTypes);
        context.AddSource($"GrpcMockServer.g.cs", SourceText.From(output, Encoding.UTF8));
    }
}
