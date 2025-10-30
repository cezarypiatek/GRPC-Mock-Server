using SmoothSailing;

namespace GrpcTestKit.TestConnectors.Kubernetes;

public class TestChartGrpcMockServerConnectorSettings
{
    public string? ProtoDirectory { get;  set; }
    public string DockerImage { get; set; } = "cezarypiatek/grpc-mock-server";
    public string ReleaseName { get; set; } = "grpcmockserverconnector";
    public int GrpcPort { get;  set; } = 5033;
    public int StubbingPort { get; set; } = 9095;
    public bool ExposeGrpcPortOnLocalhost { get; set; }
    public int ExposeGrpcPortOnLocalhostPort { get; set; }
    public bool ExposeStubbingPortOnLocalhost { get; set; }
    public int ExposeStubbingPortOnLocalhostPort { get; set; }
    public KubernetesContext? Context { get; set; }
    public Dictionary<string, string> EnvVariables { get; set; } = new Dictionary<string, string>();
}