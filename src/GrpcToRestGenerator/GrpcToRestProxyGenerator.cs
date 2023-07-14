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

    public const string GeneratorAttribute = @"
using System;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class GrpcMockHelperForAttribute:Attribute
{
    public GrpcMockHelperForAttribute(Type serviceType)
    {
    }
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class GrpcMockServerForAttribute:Attribute
{
    public GrpcMockServerForAttribute(Type serviceType)
    {
    }
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class GrpcMockServerForAutoDiscoveredSourceServicesAttribute:Attribute
{    
}
";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(ctx => ctx.AddSource(
            "GrpcMockServerForAttribute.g.cs",
            SourceText.From(GeneratorAttribute, Encoding.UTF8)));

        var classDeclarations = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (s, _) =>
                {
                    if (s is ClassDeclarationSyntax c)
                    {
                        if (HasAttribute(c, "BindServiceMethod"))
                        {
                            return true;
                        }
                        else if (HasAttribute(c, "GrpcMockServerFor"))
                        {
                            return true;
                        }
                        else if (HasAttribute(c, "GrpcMockServerForAutoDiscoveredSourceServices"))
                        {
                            return true;
                        }
                        else if (HasAttribute(c, "GrpcMockHelperFor"))
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

        var autoDiscoveredServiceTypes = classes.OfType<ClassDeclarationSyntax>().Where(x=> HasAttribute(x, "BindServiceMethod")) .Distinct().Select(el =>
        {
            var semanticModel = compilation.GetSemanticModel(el.SyntaxTree);
            return semanticModel.GetDeclaredSymbol(el);
        }).OfType<INamedTypeSymbol>().ToList();

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
                    .OfType<INamedTypeSymbol>();

                if (HasAttribute(specific, "GrpcMockServerForAutoDiscoveredSourceServices"))
                {
#pragma warning disable RS1024
                    symbols = symbols.Concat(autoDiscoveredServiceTypes).Distinct();
#pragma warning restore RS1024
                }

                var b = new ProxyBuilder(generatorType.ContainingNamespace.ToDisplayString(), generatorType.Name);
                var output = b.Build(symbols.ToList());
                context.AddSource($"{generatorType.Name}.g.cs", SourceText.From(output, Encoding.UTF8));
            }
        }
        foreach (var mockingHelperClass in classes.OfType<ClassDeclarationSyntax>().Where(x => HasAttribute(x, "GrpcMockHelperFor")))
        {
            var semanticModel = compilation.GetSemanticModel(mockingHelperClass.SyntaxTree);

            var mockingHelperType = semanticModel.GetDeclaredSymbol(mockingHelperClass);
            if (mockingHelperType != null)
            {
                var serviceBaseSymbol = mockingHelperClass.AttributeLists.SelectMany(x => x.Attributes)
                    .Where(x => x?.Name.ToString().Contains("GrpcMockHelperFor") == true).Select(x =>
                        x.ArgumentList?.Arguments.FirstOrDefault()?.Expression as TypeOfExpressionSyntax).OfType<TypeOfExpressionSyntax>()
                    .Select(x => semanticModel.GetSymbolInfo(x.Type).Symbol)
                    .OfType<INamedTypeSymbol>();


                var b = new StubHelperBuilder(mockingHelperType.ContainingNamespace.ToDisplayString(), mockingHelperType.Name);
                var output = b.Build(serviceBaseSymbol.ToList());
                context.AddSource($"{mockingHelperType.Name}.g.cs", SourceText.From(output, Encoding.UTF8));
            }
        }
    }
}
