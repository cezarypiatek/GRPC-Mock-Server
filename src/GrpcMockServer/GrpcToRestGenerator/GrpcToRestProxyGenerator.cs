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
                    if (s is ClassDeclarationSyntax c)
                    {
                        if (c.Modifiers.Count(m => m.Kind() is SyntaxKind.AbstractKeyword or SyntaxKind.PartialKeyword or SyntaxKind.PublicKeyword) == 3)
                        {
                            if (HasAttribute(c, "BindServiceMethod"))
                            {
                                return true;
                            }
                        }

                        if (HasAttribute(c, "GrpcMockServerFor"))
                        {
                            return true;
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

    private static bool HasAttribute(ClassDeclarationSyntax c, string attributeName)
    {
        if (c is {AttributeLists.Count: > 0})
        {
            return c.AttributeLists.SelectMany(x => x.Attributes).Any(x => x?.Name.ToString().Contains(attributeName) == true);
        }

        return false;
    }


    private static void Execute(Compilation compilation, ImmutableArray<SyntaxNode> classes, SourceProductionContext context)
    {
        if (classes.IsDefaultOrEmpty)
        {
            // nothing to do yet
            return;
        }

        var baseTypesForGlobalMock = classes.OfType<ClassDeclarationSyntax>().Where(x=> HasAttribute(x, "BindServiceMethod")) .Distinct().Select(el =>
        {
            var semanticModel = compilation.GetSemanticModel(el.SyntaxTree);
            return semanticModel.GetDeclaredSymbol(el);
        }).OfType<INamedTypeSymbol>().ToList();

        if (baseTypesForGlobalMock.Count > 0)
        {
            var b = new ProxyBuilder("GrpcTestKit", "GrpcMockServer");
            var output = b.Build(baseTypesForGlobalMock);
            context.AddSource($"GrpcMockServer.g.cs", SourceText.From(output, Encoding.UTF8));
        }

        foreach (var specific in classes.OfType<ClassDeclarationSyntax>().Where(x => HasAttribute(x,"GrpcMockServerFor")))
        {
            var semanticModel = compilation.GetSemanticModel(specific.SyntaxTree);

            var generatorType = semanticModel.GetDeclaredSymbol(specific);
            if (generatorType != null)
            {
                var symbols = specific.AttributeLists.SelectMany(x => x.Attributes)
                    .Where(x => x?.Name.ToString().Contains("GrpcMockServerFor") == true).Select(x =>
                        x.ArgumentList?.Arguments.FirstOrDefault()?.Expression as TypeOfExpressionSyntax).OfType<TypeOfExpressionSyntax>()
                    .Select(x => semanticModel.GetSymbolInfo(x.Type).Symbol)
                    .OfType<INamedTypeSymbol>()
                    .ToList();

                var b = new ProxyBuilder(generatorType.ContainingNamespace.ToDisplayString(), generatorType.Name);
                var output = b.Build(symbols);
                context.AddSource($"{generatorType.Name}.g.cs", SourceText.From(output, Encoding.UTF8));
            }
        }
    }
}
