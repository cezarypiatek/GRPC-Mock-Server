namespace GrpcMockServer;

public static partial class GrpcToRestProxyExtensions
{
    public static void MapGrpcToRestProxies(this Microsoft.AspNetCore.Routing.IEndpointRouteBuilder builder)
    {
        MapAllProxies(builder);
    }

    static partial void MapAllProxies(Microsoft.AspNetCore.Routing.IEndpointRouteBuilder builder);
}